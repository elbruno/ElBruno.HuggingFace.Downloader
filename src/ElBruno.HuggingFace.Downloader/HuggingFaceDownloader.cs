using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElBruno.HuggingFace;

/// <summary>
/// Downloads files from Hugging Face Hub repositories.
/// </summary>
public sealed class HuggingFaceDownloader : IDisposable
{
    private const string PartialFileSuffix = ".partial";
    private const string PartialMetadataSuffix = ".partial.json";
    private const string AtomicTempFileSuffix = ".tmp";
    private const string ResolvedCommitHeaderName = "X-Resolved-Revision";
    private const int FileStreamBufferSize = 81920;

    private static readonly JsonSerializerOptions MetadataJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly HuggingFaceDownloaderOptions _options;
    private readonly ILogger<HuggingFaceDownloader> _logger;

    /// <summary>
    /// Creates a new downloader with default options.
    /// </summary>
    public HuggingFaceDownloader()
        : this(new HuggingFaceDownloaderOptions())
    {
    }

    /// <summary>
    /// Creates a new downloader with the specified options.
    /// </summary>
    public HuggingFaceDownloader(HuggingFaceDownloaderOptions options, ILogger<HuggingFaceDownloader>? logger = null)
        : this(CreateHttpClient(options), ownsHttpClient: true, options, logger)
    {
    }

    /// <summary>
    /// Creates a new downloader using an externally managed <see cref="HttpClient"/>.
    /// </summary>
    public HuggingFaceDownloader(HttpClient httpClient, HuggingFaceDownloaderOptions? options = null, ILogger<HuggingFaceDownloader>? logger = null)
        : this(httpClient, ownsHttpClient: false, options ?? new HuggingFaceDownloaderOptions(), logger)
    {
    }

    private HuggingFaceDownloader(HttpClient httpClient, bool ownsHttpClient, HuggingFaceDownloaderOptions options, ILogger<HuggingFaceDownloader>? logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _ownsHttpClient = ownsHttpClient;
        _logger = logger ?? NullLogger<HuggingFaceDownloader>.Instance;
    }

    /// <summary>
    /// Returns the list of files from <paramref name="files"/> that do not exist in <paramref name="localDirectory"/>.
    /// </summary>
    public IReadOnlyList<string> GetMissingFiles(IEnumerable<string> files, string localDirectory)
    {
        return files
            .Where(f => !File.Exists(Path.Combine(localDirectory, f.Replace('/', Path.DirectorySeparatorChar))))
            .ToList();
    }

    /// <summary>
    /// Returns true if all specified <paramref name="files"/> exist in <paramref name="localDirectory"/>.
    /// </summary>
    public bool AreFilesAvailable(IEnumerable<string> files, string localDirectory)
    {
        return files.All(f => File.Exists(Path.Combine(localDirectory, f.Replace('/', Path.DirectorySeparatorChar))));
    }

    /// <summary>
    /// Resolves a branch or tag to an immutable commit SHA when the Hugging Face Hub exposes it.
    /// </summary>
    public async Task<string?> ResolveCommitShaAsync(
        string repoId,
        string filePath,
        string revision = "main",
        CancellationToken cancellationToken = default)
    {
        if (TryNormalizeCommitSha(revision, out var normalizedCommitSha))
            return normalizedCommitSha;

        var url = HuggingFaceUrlBuilder.GetFileUrl(repoId, filePath, revision);
        var remoteInfo = await TryGetRemoteFileInfoAsync(url, cancellationToken).ConfigureAwait(false);
        return NormalizeResolvedCommitSha(remoteInfo?.ResolvedCommitSha);
    }

    /// <summary>
    /// Downloads files described by the <see cref="DownloadRequest"/>. Files that already exist locally are skipped.
    /// </summary>
    public async Task DownloadFilesAsync(DownloadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.RepoId))
            throw new ArgumentException("RepoId cannot be null or empty.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.LocalDirectory))
            throw new ArgumentException("LocalDirectory cannot be null or empty.", nameof(request));

        Directory.CreateDirectory(request.LocalDirectory);
        request.ResolvedCommitSha = NormalizeResolvedCommitSha(request.ResolvedCommitSha);
        UpdateResolvedCommitSha(request, request.Revision);

        var allFiles = new List<(string path, bool required)>(request.RequiredFiles.Count + (request.OptionalFiles?.Count ?? 0));
        foreach (var f in request.RequiredFiles)
            allFiles.Add((f, true));
        if (request.OptionalFiles is not null)
            foreach (var f in request.OptionalFiles)
                allFiles.Add((f, false));

        var missingFiles = allFiles
            .Where(f => !File.Exists(Path.Combine(request.LocalDirectory, f.path.Replace('/', Path.DirectorySeparatorChar))))
            .ToList();

        if (request.ResolvedCommitSha is null
            && allFiles.Count > 0
            && (missingFiles.Count == 0
                || !string.IsNullOrWhiteSpace(request.ExpectedCommitSha)
                || HasResolutionMetadata(request.LocalDirectory)))
        {
            var resolvedCommitSha = await ResolveCommitShaAsync(
                request.RepoId,
                allFiles[0].path,
                request.Revision,
                cancellationToken).ConfigureAwait(false);
            UpdateResolvedCommitSha(request, resolvedCommitSha);
        }

        EnsureLocalDirectoryRevisionCompatibility(
            request.LocalDirectory,
            request.RepoId,
            request.Revision,
            request.ResolvedCommitSha);

        if (missingFiles.Count == 0)
        {
            await WriteDownloadResolutionMetadataAsync(request, cancellationToken).ConfigureAwait(false);
            request.Progress?.Report(new DownloadProgress
            {
                Stage = DownloadStage.Complete,
                PercentComplete = 100,
                Message = "All files already present."
            });
            _logger.LogDebug("All files already present in {Directory}", request.LocalDirectory);
            return;
        }

        long totalBytes = 0;
        long completedBytes = 0;
        long totalResumedBytes = 0;
        var fileSizes = new Dictionary<string, long>();
        var remoteFileInfos = new Dictionary<string, RemoteFileInfo?>();

        if (_options.ResolveFileSizesBeforeDownload)
        {
            request.Progress?.Report(new DownloadProgress
            {
                Stage = DownloadStage.Checking,
                Message = $"Checking {missingFiles.Count} files from {request.RepoId}..."
            });
        }

        foreach (var (path, _) in missingFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var localPath = Path.Combine(request.LocalDirectory, path.Replace('/', Path.DirectorySeparatorChar));
            var hasPartial = request.UseAtomicWrites
                && request.ResumePartialDownloads
                && File.Exists(GetPartialFilePath(localPath));

            if (!_options.ResolveFileSizesBeforeDownload && !hasPartial)
                continue;

            var url = HuggingFaceUrlBuilder.GetFileUrl(request.RepoId, path, request.Revision);
            var remoteInfo = await TryGetRemoteFileInfoAsync(url, cancellationToken).ConfigureAwait(false);
            UpdateResolvedCommitSha(request, remoteInfo?.ResolvedCommitSha);
            remoteFileInfos[path] = remoteInfo;

            if (remoteInfo?.ContentLength is > 0)
            {
                fileSizes[path] = remoteInfo.ContentLength.Value;
                totalBytes += remoteInfo.ContentLength.Value;
            }
        }

        for (int i = 0; i < missingFiles.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (filePath, required) = missingFiles[i];
            var localPath = Path.Combine(request.LocalDirectory, filePath.Replace('/', Path.DirectorySeparatorChar));
            var localDir = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(localDir))
                Directory.CreateDirectory(localDir);

            var url = HuggingFaceUrlBuilder.GetFileUrl(request.RepoId, filePath, request.Revision);
            var remoteInfo = remoteFileInfos.GetValueOrDefault(filePath);

            try
            {
                var downloadResult = await DownloadSingleFileAsync(
                    url,
                    localPath,
                    filePath,
                    request,
                    i,
                    missingFiles.Count,
                    fileSizes.GetValueOrDefault(filePath),
                    completedBytes,
                    totalResumedBytes,
                    totalBytes,
                    remoteInfo,
                    bundleValidation: null,
                    cancellationToken).ConfigureAwait(false);

                completedBytes += downloadResult.FileBytes;
                totalResumedBytes += downloadResult.ResumedBytes;
                _logger.LogInformation("Successfully downloaded {File}", filePath);
            }
            catch (HttpRequestException) when (!required)
            {
                _logger.LogWarning("Optional file {File} failed to download, skipping", filePath);
            }
            catch (InvalidOperationException) when (!required)
            {
                _logger.LogWarning("Optional file {File} failed validation, skipping", filePath);
            }
            catch (HttpRequestException ex) when (required)
            {
                var statusCode = ex.StatusCode;
                if (statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.Forbidden)
                {
                    throw new InvalidOperationException(
                        $"Access denied downloading '{filePath}'. " +
                        "The repository may be private or gated. Ensure HF_TOKEN is set with appropriate permissions.",
                        ex);
                }

                if (statusCode == HttpStatusCode.NotFound)
                {
                    throw new InvalidOperationException(
                        $"File '{filePath}' not found (404).",
                        ex);
                }

                throw new InvalidOperationException(
                    $"Failed to download required file '{filePath}': {ex.Message}",
                    ex);
            }
        }

        request.Progress?.Report(new DownloadProgress
        {
            Stage = DownloadStage.Validating,
            PercentComplete = 99,
            BytesDownloaded = completedBytes,
            TotalBytes = totalBytes,
            ResumedBytes = totalResumedBytes,
            Message = "Validating downloaded files..."
        });

        var validationErrors = new List<string>();
        foreach (var file in request.RequiredFiles)
        {
            var localPath = Path.Combine(request.LocalDirectory, file.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(localPath))
            {
                validationErrors.Add(file);
                continue;
            }

            if (fileSizes.TryGetValue(file, out var expectedSize))
            {
                var actualSize = new FileInfo(localPath).Length;
                if (actualSize != expectedSize)
                {
                    validationErrors.Add($"{file} (expected {expectedSize} bytes, got {actualSize} bytes)");
                }
            }
        }

        if (validationErrors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Download incomplete. Missing or invalid required files: {string.Join(", ", validationErrors)}");
        }

        var completionMessage = totalResumedBytes > 0
            ? $"All files downloaded and validated. Reused {ByteFormatHelper.FormatBytes(totalResumedBytes)} from partial downloads."
            : "All files downloaded and validated.";

        request.Progress?.Report(new DownloadProgress
        {
            Stage = DownloadStage.Complete,
            PercentComplete = 100,
            BytesDownloaded = completedBytes,
            TotalBytes = totalBytes,
            ResumedBytes = totalResumedBytes,
            Message = completionMessage
        });

        await WriteDownloadResolutionMetadataAsync(request, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Download complete for {RepoId} — {FileCount} files, {Bytes}, resumed {ResumedBytes}",
            request.RepoId,
            missingFiles.Count,
            ByteFormatHelper.FormatBytes(completedBytes),
            ByteFormatHelper.FormatBytes(totalResumedBytes));
    }

    /// <summary>
    /// Ensures every file described by <paramref name="manifest"/> is present and valid in <paramref name="localDirectory"/>.
    /// </summary>
    public async Task<ModelBundleResult> EnsureBundleAsync(
        ModelBundleManifest manifest,
        string localDirectory,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (string.IsNullOrWhiteSpace(localDirectory))
            throw new ArgumentException("LocalDirectory cannot be null or empty.", nameof(localDirectory));
        if (string.IsNullOrWhiteSpace(manifest.RepoId))
            throw new ArgumentException("RepoId cannot be null or empty.", nameof(manifest));
        if (manifest.Files is null || manifest.Files.Count == 0)
            throw new ArgumentException("Bundle manifests must include at least one file.", nameof(manifest));

        Directory.CreateDirectory(localDirectory);

        var normalizedFiles = manifest.Files
            .Select(file => NormalizeBundleFile(file, manifest.Revision))
            .ToList();

        var distinctRevisions = normalizedFiles
            .Select(file => file.Revision)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (distinctRevisions.Count != 1)
        {
            throw new InvalidOperationException(
                $"Manifest '{manifest.RepoId}' contains mixed revisions: {string.Join(", ", distinctRevisions)}.");
        }

        var resolvedRevision = distinctRevisions[0];
        var resolvedCommitSha = await ResolveCommitShaAsync(
            manifest.RepoId,
            normalizedFiles[0].Path,
            resolvedRevision,
            cancellationToken).ConfigureAwait(false);

        EnsureLocalDirectoryRevisionCompatibility(
            localDirectory,
            manifest.RepoId,
            resolvedRevision,
            resolvedCommitSha);

        progress?.Report(new DownloadProgress
        {
            Stage = DownloadStage.Checking,
            CurrentFileIndex = 0,
            TotalFileCount = normalizedFiles.Count,
            Message = $"Checking bundle manifest for {manifest.RepoId}..."
        });

        var filesToDownload = new List<NormalizedBundleFile>(normalizedFiles.Count);
        foreach (var file in normalizedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var localPath = GetLocalPath(localDirectory, file.Path);
            if (!File.Exists(localPath))
            {
                filesToDownload.Add(file);
                continue;
            }

            var localValidation = await ValidateLocalBundleFileAsync(localPath, file, cancellationToken).ConfigureAwait(false);
            if (localValidation.IsValid)
                continue;

            _logger.LogInformation("Existing bundle file {File} failed integrity validation and will be refreshed", file.Path);
            DeleteIfExists(localPath);
            CleanupResumeArtifacts(localPath);
            filesToDownload.Add(file);
        }

        long totalBytes = 0;
        long completedBytes = 0;
        var remoteFileInfos = new Dictionary<string, RemoteFileInfo?>(StringComparer.Ordinal);

        foreach (var file in filesToDownload)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var url = HuggingFaceUrlBuilder.GetFileUrl(manifest.RepoId, file.Path, file.Revision);
            var remoteInfo = await TryGetRemoteFileInfoAsync(url, cancellationToken).ConfigureAwait(false);
            resolvedCommitSha = MergeResolvedCommitSha(
                resolvedCommitSha,
                NormalizeResolvedCommitSha(remoteInfo?.ResolvedCommitSha),
                manifest.RepoId,
                resolvedRevision);
            remoteFileInfos[file.Path] = remoteInfo;

            if (file.Size is > 0)
            {
                totalBytes += file.Size.Value;
                continue;
            }

            if (remoteInfo?.ContentLength is > 0)
                totalBytes += remoteInfo.ContentLength.Value;
        }

        for (int i = 0; i < filesToDownload.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var file = filesToDownload[i];
            var localPath = GetLocalPath(localDirectory, file.Path);
            var localDir = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(localDir))
                Directory.CreateDirectory(localDir);

            var url = HuggingFaceUrlBuilder.GetFileUrl(manifest.RepoId, file.Path, file.Revision);
            var request = new DownloadRequest
            {
                RepoId = manifest.RepoId,
                LocalDirectory = localDirectory,
                RequiredFiles = [file.Path],
                Revision = file.Revision,
                ResolvedCommitSha = resolvedCommitSha,
                Progress = progress
            };

            try
            {
                var downloadResult = await DownloadSingleFileAsync(
                    url,
                    localPath,
                    file.Path,
                    request,
                    i,
                    filesToDownload.Count,
                    file.Size ?? 0,
                    completedBytes,
                    previouslyResumedBytes: 0,
                    totalBytes,
                    remoteFileInfos.GetValueOrDefault(file.Path),
                    new BundleFileValidation(file.Size, file.Sha256),
                    cancellationToken).ConfigureAwait(false);

                completedBytes += downloadResult.FileBytes;
            }
            catch (HttpRequestException ex) when (!file.Required)
            {
                _logger.LogWarning("Optional bundle file {File} failed to download, skipping", file.Path);
                _logger.LogDebug(ex, "Optional bundle file download failed");
            }
            catch (InvalidOperationException ex) when (!file.Required)
            {
                _logger.LogWarning("Optional bundle file {File} failed integrity validation, skipping", file.Path);
                _logger.LogDebug(ex, "Optional bundle file validation failed");
            }
            catch (HttpRequestException ex) when (file.Required)
            {
                var statusCode = ex.StatusCode;
                if (statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.Forbidden)
                {
                    throw new InvalidOperationException(
                        $"Access denied downloading '{file.Path}'. The repository may be private or gated. Ensure HF_TOKEN is set with appropriate permissions.",
                        ex);
                }

                if (statusCode == HttpStatusCode.NotFound)
                    throw new InvalidOperationException($"File '{file.Path}' not found (404).", ex);

                throw new InvalidOperationException(
                    $"Failed to download required file '{file.Path}': {ex.Message}",
                    ex);
            }
        }

        progress?.Report(new DownloadProgress
        {
            Stage = DownloadStage.Validating,
            PercentComplete = filesToDownload.Count == 0 ? 100 : 99,
            BytesDownloaded = completedBytes,
            TotalBytes = totalBytes,
            CurrentFileIndex = normalizedFiles.Count,
            TotalFileCount = normalizedFiles.Count,
            Message = "Validating bundle files..."
        });

        var downloadedPaths = filesToDownload
            .Select(file => file.Path)
            .ToHashSet(StringComparer.Ordinal);
        var resolvedFiles = new List<ResolvedModelBundleFile>(normalizedFiles.Count);
        var requiredValidationErrors = new List<string>();

        foreach (var file in normalizedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var localPath = GetLocalPath(localDirectory, file.Path);
            if (!File.Exists(localPath))
            {
                if (file.Required)
                    requiredValidationErrors.Add(file.Path);

                resolvedFiles.Add(new ResolvedModelBundleFile
                {
                    Path = file.Path,
                    Revision = file.Revision,
                    Required = file.Required,
                    Exists = false,
                    DownloadedThisRun = false
                });
                continue;
            }

            var validation = await ValidateLocalBundleFileAsync(localPath, file, cancellationToken).ConfigureAwait(false);
            if (!validation.IsValid)
            {
                DeleteIfExists(localPath);
                CleanupResumeArtifacts(localPath);

                if (file.Required)
                    requiredValidationErrors.Add(validation.ErrorMessage ?? file.Path);

                resolvedFiles.Add(new ResolvedModelBundleFile
                {
                    Path = file.Path,
                    Revision = file.Revision,
                    Required = file.Required,
                    Exists = false,
                    DownloadedThisRun = downloadedPaths.Contains(file.Path)
                });
                continue;
            }

            resolvedFiles.Add(new ResolvedModelBundleFile
            {
                Path = file.Path,
                Revision = file.Revision,
                Required = file.Required,
                Exists = true,
                Size = validation.Size,
                Sha256 = validation.Sha256,
                DownloadedThisRun = downloadedPaths.Contains(file.Path)
            });
        }

        if (requiredValidationErrors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Bundle validation failed. Missing or invalid required files: {string.Join(", ", requiredValidationErrors)}");
        }

        var resolvedManifest = new ResolvedModelBundleManifest
        {
            RepoId = manifest.RepoId,
            Revision = resolvedRevision,
            ResolvedCommitSha = resolvedCommitSha,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Files = resolvedFiles
        };

        var resolvedManifestPath = Path.Combine(localDirectory, HuggingFaceMetadataFileNames.ResolvedBundleManifest);
        await ModelBundleManifestJson.SaveResolvedAsync(resolvedManifest, resolvedManifestPath, cancellationToken).ConfigureAwait(false);

        return new ModelBundleResult
        {
            LocalDirectory = localDirectory,
            ResolvedManifestPath = resolvedManifestPath,
            ResolvedManifest = resolvedManifest,
            DownloadedFileCount = resolvedFiles.Count(file => file.DownloadedThisRun && file.Exists),
            ReusedFileCount = resolvedFiles.Count(file => !file.DownloadedThisRun && file.Exists),
            MissingOptionalFileCount = resolvedFiles.Count(file => !file.Required && !file.Exists)
        };
    }

    private async Task<FileDownloadResult> DownloadSingleFileAsync(
        string url,
        string localPath,
        string filePath,
        DownloadRequest request,
        int fileIndex,
        int totalFiles,
        long expectedFileSize,
        long previouslyCompletedBytes,
        long previouslyResumedBytes,
        long totalBytes,
        RemoteFileInfo? remoteInfo,
        BundleFileValidation? bundleValidation,
        CancellationToken cancellationToken)
    {
        var downloadState = PrepareDownloadState(localPath, request, remoteInfo);
        if (downloadState.CompletedWithoutNetwork)
        {
            request.Progress?.Report(new DownloadProgress
            {
                Stage = DownloadStage.Downloading,
                PercentComplete = totalBytes > 0 ? (double)(previouslyCompletedBytes + downloadState.ResumedBytes) / totalBytes * 100 : 0,
                BytesDownloaded = previouslyCompletedBytes + downloadState.ResumedBytes,
                TotalBytes = totalBytes,
                ResumedBytes = previouslyResumedBytes + downloadState.ResumedBytes,
                CurrentFile = filePath,
                CurrentFileIndex = fileIndex + 1,
                TotalFileCount = totalFiles,
                Message = $"[{fileIndex + 1}/{totalFiles}] Recovered completed partial download for {filePath}."
            });

            return new FileDownloadResult(downloadState.ResumedBytes, downloadState.ResumedBytes);
        }

        while (true)
        {
            if (downloadState.ResumedBytes > 0)
            {
                request.Progress?.Report(new DownloadProgress
                {
                    Stage = DownloadStage.Downloading,
                    PercentComplete = totalBytes > 0 ? (double)(previouslyCompletedBytes + downloadState.ResumedBytes) / totalBytes * 100 : 0,
                    BytesDownloaded = previouslyCompletedBytes + downloadState.ResumedBytes,
                    TotalBytes = totalBytes,
                    ResumedBytes = previouslyResumedBytes + downloadState.ResumedBytes,
                    CurrentFile = filePath,
                    CurrentFileIndex = fileIndex + 1,
                    TotalFileCount = totalFiles,
                    Message = $"[{fileIndex + 1}/{totalFiles}] Resuming {filePath} from {ByteFormatHelper.FormatBytes(downloadState.ResumedBytes)}..."
                });
            }
            else
            {
                request.Progress?.Report(new DownloadProgress
                {
                    Stage = DownloadStage.Downloading,
                    PercentComplete = totalBytes > 0 ? (double)previouslyCompletedBytes / totalBytes * 100 : 0,
                    BytesDownloaded = previouslyCompletedBytes,
                    TotalBytes = totalBytes,
                    ResumedBytes = previouslyResumedBytes,
                    CurrentFile = filePath,
                    CurrentFileIndex = fileIndex + 1,
                    TotalFileCount = totalFiles,
                    Message = $"[{fileIndex + 1}/{totalFiles}] Downloading {filePath}..."
                });
            }

            using var requestMessage = CreateDownloadRequest(url, downloadState.ResumedBytes, downloadState.EntityTag);
            using var response = await _httpClient.SendAsync(
                requestMessage,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            if (downloadState.ResumedBytes > 0 && response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                var remoteLength = response.Content.Headers.ContentRange?.Length ?? downloadState.TotalBytes;
                if (remoteLength is > 0 && remoteLength.Value == downloadState.ResumedBytes)
                {
                    FinalizeCompletedDownload(downloadState.WritePath, localPath, downloadState.MetadataPath, useAtomicWrites: true);
                    return new FileDownloadResult(downloadState.ResumedBytes, downloadState.ResumedBytes);
                }

                _logger.LogInformation("Server rejected resume for {File}; restarting full download", filePath);
                CleanupDownloadArtifacts(downloadState.WritePath, downloadState.MetadataPath, request.UseAtomicWrites, keepPartial: false);
                downloadState = CreateFreshDownloadState(localPath, request);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Failed to download file. Status: {response.StatusCode}, URL: {url}",
                    inner: null,
                    statusCode: response.StatusCode);
            }

            UpdateResolvedCommitSha(request, TryGetResolvedCommitSha(response.Headers));

            if (downloadState.ResumedBytes > 0)
            {
                var responseEntityTag = response.Headers.ETag?.ToString();
                if (response.StatusCode == HttpStatusCode.OK
                    || (responseEntityTag is not null
                        && downloadState.EntityTag is not null
                        && !string.Equals(responseEntityTag, downloadState.EntityTag, StringComparison.Ordinal)))
                {
                    _logger.LogInformation("Resume could not continue safely for {File}; restarting full download", filePath);
                    CleanupDownloadArtifacts(downloadState.WritePath, downloadState.MetadataPath, request.UseAtomicWrites, keepPartial: false);
                    downloadState = CreateFreshDownloadState(localPath, request);
                    continue;
                }
            }

            var resolvedEntityTag = response.Headers.ETag?.ToString() ?? remoteInfo?.EntityTag ?? downloadState.EntityTag;
            var resolvedTotalBytes = ResolveExpectedFileSize(response, expectedFileSize, remoteInfo, downloadState.TotalBytes);
            if (resolvedTotalBytes > 0)
            {
                expectedFileSize = resolvedTotalBytes;
            }

            if (downloadState.AllowResume)
            {
                PersistPartialMetadata(downloadState.MetadataPath!, new PartialDownloadMetadata
                {
                    Revision = request.Revision,
                    ResolvedCommitSha = request.ResolvedCommitSha,
                    EntityTag = resolvedEntityTag,
                    TotalBytes = resolvedTotalBytes > 0 ? resolvedTotalBytes : null
                });
            }

            long downloadedThisAttempt = 0;

            try
            {
                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var fileStream = new FileStream(
                    downloadState.WritePath,
                    downloadState.ResumedBytes > 0 ? FileMode.Append : FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: FileStreamBufferSize,
                    useAsync: true);

                int bytesRead;
                var buffer = new byte[FileStreamBufferSize];
                while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    downloadedThisAttempt += bytesRead;

                    var availableBytes = downloadState.ResumedBytes + downloadedThisAttempt;
                    request.Progress?.Report(new DownloadProgress
                    {
                        Stage = DownloadStage.Downloading,
                        PercentComplete = totalBytes > 0 ? (double)(previouslyCompletedBytes + availableBytes) / totalBytes * 100 : 0,
                        BytesDownloaded = previouslyCompletedBytes + availableBytes,
                        TotalBytes = totalBytes,
                        ResumedBytes = previouslyResumedBytes + downloadState.ResumedBytes,
                        CurrentFile = filePath,
                        CurrentFileIndex = fileIndex + 1,
                        TotalFileCount = totalFiles,
                        Message = BuildProgressMessage(fileIndex, totalFiles, filePath, availableBytes, resolvedTotalBytes, downloadState.ResumedBytes)
                    });
                }

                await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception) when (downloadState.AllowResume && GetFileLength(downloadState.WritePath) > 0)
            {
                _logger.LogInformation("Preserving partial download for {File} at {Path}", filePath, downloadState.WritePath);
                throw;
            }
            catch
            {
                CleanupDownloadArtifacts(downloadState.WritePath, downloadState.MetadataPath, request.UseAtomicWrites, keepPartial: false);
                throw;
            }

            var finalFileBytes = downloadState.ResumedBytes + downloadedThisAttempt;
            if (resolvedTotalBytes > 0 && finalFileBytes != resolvedTotalBytes)
            {
                throw new InvalidOperationException(
                    $"Downloaded size mismatch for '{filePath}'. Expected {resolvedTotalBytes} bytes but found {finalFileBytes} bytes.");
            }

            if (bundleValidation is not null)
            {
                var bundleValidationResult = await ValidateDownloadedBundleFileAsync(
                    downloadState.WritePath,
                    filePath,
                    bundleValidation,
                    cancellationToken).ConfigureAwait(false);

                if (!bundleValidationResult.IsValid)
                {
                    CleanupDownloadArtifacts(downloadState.WritePath, downloadState.MetadataPath, request.UseAtomicWrites, keepPartial: false);
                    throw new InvalidOperationException(
                        bundleValidationResult.ErrorMessage ?? $"Bundle validation failed for '{filePath}'.");
                }
            }

            FinalizeCompletedDownload(downloadState.WritePath, localPath, downloadState.MetadataPath, request.UseAtomicWrites);
            return new FileDownloadResult(finalFileBytes, downloadState.ResumedBytes);
        }
    }

    private async Task<RemoteFileInfo?> TryGetRemoteFileInfoAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
            using var headResponse = await _httpClient.SendAsync(
                headRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            if (!headResponse.IsSuccessStatusCode)
                return null;

            return new RemoteFileInfo(
                headResponse.Content.Headers.ContentLength,
                headResponse.Headers.ETag?.ToString(),
                headResponse.Headers.AcceptRanges.Any(value => string.Equals(value, "bytes", StringComparison.OrdinalIgnoreCase)),
                TryGetResolvedCommitSha(headResponse.Headers));
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private static DownloadState PrepareDownloadState(string localPath, DownloadRequest request, RemoteFileInfo? remoteInfo)
    {
        if (!request.UseAtomicWrites)
        {
            CleanupDownloadArtifacts(localPath, metadataPath: null, useAtomicWrites: false, keepPartial: false);
            CleanupResumeArtifacts(localPath);
            return new DownloadState(localPath, null, false, 0, null, null, false);
        }

        if (!request.ResumePartialDownloads)
        {
            CleanupResumeArtifacts(localPath);
            return new DownloadState(GetAtomicTempPath(localPath), null, false, 0, null, null, false);
        }

        var partialPath = GetPartialFilePath(localPath);
        var metadataPath = GetPartialMetadataPath(localPath);

        if (!File.Exists(partialPath))
        {
            DeleteIfExists(metadataPath);
            return new DownloadState(partialPath, metadataPath, true, 0, remoteInfo?.EntityTag, remoteInfo?.ContentLength, false);
        }

        var metadata = TryReadPartialMetadata(metadataPath);
        if (metadata is null)
        {
            CleanupResumeArtifacts(localPath);
            return new DownloadState(partialPath, metadataPath, true, 0, remoteInfo?.EntityTag, remoteInfo?.ContentLength, false);
        }

        var partialLength = GetFileLength(partialPath);
        if (metadata.Revision != request.Revision)
        {
            CleanupResumeArtifacts(localPath);
            return new DownloadState(partialPath, metadataPath, true, 0, remoteInfo?.EntityTag, remoteInfo?.ContentLength, false);
        }

        if (!string.IsNullOrEmpty(metadata.ResolvedCommitSha)
            && !string.IsNullOrEmpty(request.ResolvedCommitSha)
            && !string.Equals(metadata.ResolvedCommitSha, request.ResolvedCommitSha, StringComparison.Ordinal))
        {
            CleanupResumeArtifacts(localPath);
            return new DownloadState(partialPath, metadataPath, true, 0, remoteInfo?.EntityTag, remoteInfo?.ContentLength, false);
        }

        if (metadata.TotalBytes is > 0 && partialLength > metadata.TotalBytes.Value)
        {
            CleanupResumeArtifacts(localPath);
            return new DownloadState(partialPath, metadataPath, true, 0, remoteInfo?.EntityTag, remoteInfo?.ContentLength, false);
        }

        if (remoteInfo?.EntityTag is not null
            && metadata.EntityTag is not null
            && !string.Equals(remoteInfo.EntityTag, metadata.EntityTag, StringComparison.Ordinal))
        {
            CleanupResumeArtifacts(localPath);
            return new DownloadState(partialPath, metadataPath, true, 0, remoteInfo.EntityTag, remoteInfo.ContentLength, false);
        }

        if (remoteInfo?.ContentLength is > 0
            && metadata.TotalBytes is > 0
            && remoteInfo.ContentLength.Value != metadata.TotalBytes.Value)
        {
            CleanupResumeArtifacts(localPath);
            return new DownloadState(partialPath, metadataPath, true, 0, remoteInfo.EntityTag, remoteInfo.ContentLength, false);
        }

        var totalBytes = remoteInfo?.ContentLength ?? metadata.TotalBytes;
        if (totalBytes is > 0 && partialLength == totalBytes.Value)
        {
            FinalizeCompletedDownload(partialPath, localPath, metadataPath, useAtomicWrites: true);
            return new DownloadState(partialPath, metadataPath, true, partialLength, metadata.EntityTag ?? remoteInfo?.EntityTag, totalBytes, true);
        }

        return new DownloadState(
            partialPath,
            metadataPath,
            true,
            partialLength,
            metadata.EntityTag ?? remoteInfo?.EntityTag,
            totalBytes,
            false);
    }

    private static DownloadState CreateFreshDownloadState(string localPath, DownloadRequest request)
    {
        return request.UseAtomicWrites && request.ResumePartialDownloads
            ? new DownloadState(GetPartialFilePath(localPath), GetPartialMetadataPath(localPath), true, 0, null, null, false)
            : request.UseAtomicWrites
                ? new DownloadState(GetAtomicTempPath(localPath), null, false, 0, null, null, false)
                : new DownloadState(localPath, null, false, 0, null, null, false);
    }

    private static HttpRequestMessage CreateDownloadRequest(string url, long resumedBytes, string? entityTag)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (resumedBytes > 0)
        {
            request.Headers.Range = new RangeHeaderValue(resumedBytes, null);
            if (!string.IsNullOrWhiteSpace(entityTag))
                request.Headers.TryAddWithoutValidation("If-Range", entityTag);
        }

        return request;
    }

    private static long ResolveExpectedFileSize(
        HttpResponseMessage response,
        long expectedFileSize,
        RemoteFileInfo? remoteInfo,
        long? preparedTotalBytes)
    {
        if (response.Content.Headers.ContentRange?.Length is { } contentRangeLength)
            return contentRangeLength;
        if (response.StatusCode == HttpStatusCode.OK && response.Content.Headers.ContentLength is { } contentLength)
            return contentLength;
        if (remoteInfo?.ContentLength is { } remoteLength)
            return remoteLength;
        if (preparedTotalBytes is { } preparedLength)
            return preparedLength;

        return expectedFileSize;
    }

    private static string BuildProgressMessage(int fileIndex, int totalFiles, string filePath, long availableBytes, long totalBytes, long resumedBytes)
    {
        var prefix = $"[{fileIndex + 1}/{totalFiles}] {filePath} — ";
        var progress = $"{ByteFormatHelper.FormatBytes(availableBytes)}/{ByteFormatHelper.FormatBytes(totalBytes)}";
        if (resumedBytes <= 0)
            return prefix + progress;

        return prefix + $"resumed {ByteFormatHelper.FormatBytes(resumedBytes)}, {progress}";
    }

    private static string? TryGetResolvedCommitSha(HttpHeaders headers)
    {
        return headers.TryGetValues(ResolvedCommitHeaderName, out var values)
            ? NormalizeResolvedCommitSha(values.FirstOrDefault())
            : null;
    }

    private static void UpdateResolvedCommitSha(DownloadRequest request, string? candidateCommitSha)
    {
        var normalizedCandidate = NormalizeResolvedCommitSha(candidateCommitSha);
        if (!string.IsNullOrEmpty(normalizedCandidate))
        {
            request.ResolvedCommitSha = MergeResolvedCommitSha(
                request.ResolvedCommitSha,
                normalizedCandidate,
                request.RepoId,
                request.Revision);
        }

        ValidateExpectedCommitSha(request.ExpectedCommitSha, request.ResolvedCommitSha);
    }

    private static string? MergeResolvedCommitSha(
        string? existingCommitSha,
        string? candidateCommitSha,
        string repoId,
        string revision)
    {
        if (string.IsNullOrEmpty(candidateCommitSha))
            return existingCommitSha;

        if (string.IsNullOrEmpty(existingCommitSha))
            return candidateCommitSha;

        if (string.Equals(existingCommitSha, candidateCommitSha, StringComparison.Ordinal))
            return existingCommitSha;

        throw new InvalidOperationException(
            $"Revision '{revision}' for repository '{repoId}' resolved to multiple commits ({existingCommitSha} and {candidateCommitSha}).");
    }

    private static void ValidateExpectedCommitSha(string? expectedCommitSha, string? resolvedCommitSha)
    {
        var normalizedExpectedCommitSha = NormalizeExpectedCommitSha(expectedCommitSha);
        if (normalizedExpectedCommitSha is null || string.IsNullOrEmpty(resolvedCommitSha))
            return;

        if (!string.Equals(normalizedExpectedCommitSha, resolvedCommitSha, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Revision resolved to commit '{resolvedCommitSha}', but '{normalizedExpectedCommitSha}' was expected.");
        }
    }

    private static string? NormalizeExpectedCommitSha(string? commitSha)
    {
        if (string.IsNullOrWhiteSpace(commitSha))
            return null;

        var normalized = commitSha.Trim().ToLowerInvariant();
        if (normalized.Length != 40 || normalized.Any(ch => !Uri.IsHexDigit(ch)))
        {
            throw new ArgumentException(
                $"Invalid commit SHA '{commitSha}'. Expected a 40-character hexadecimal Git commit.",
                nameof(DownloadRequest.ExpectedCommitSha));
        }

        return normalized;
    }

    private static string? NormalizeResolvedCommitSha(string? commitSha)
    {
        if (string.IsNullOrWhiteSpace(commitSha))
            return null;

        return TryNormalizeCommitSha(commitSha, out var normalizedCommitSha)
            ? normalizedCommitSha
            : null;
    }

    private static bool TryNormalizeCommitSha(string? commitSha, out string? normalizedCommitSha)
    {
        normalizedCommitSha = null;
        if (string.IsNullOrWhiteSpace(commitSha))
            return false;

        var candidate = commitSha.Trim().ToLowerInvariant();
        if (candidate.Length != 40 || candidate.Any(ch => !Uri.IsHexDigit(ch)))
            return false;

        normalizedCommitSha = candidate;
        return true;
    }

    private static void EnsureLocalDirectoryRevisionCompatibility(
        string localDirectory,
        string repoId,
        string requestedRevision,
        string? resolvedCommitSha)
    {
        if (!Directory.Exists(localDirectory) || !DirectoryHasModelContent(localDirectory))
            return;

        var downloadMetadata = TryReadDownloadResolutionMetadata(localDirectory);
        if (downloadMetadata is not null)
        {
            EnsureCompatibleExistingMetadata(
                repoId,
                requestedRevision,
                resolvedCommitSha,
                downloadMetadata.RepoId,
                downloadMetadata.RequestedRevision,
                downloadMetadata.ResolvedCommitSha,
                localDirectory);
        }

        var resolvedBundleManifest = TryReadResolvedBundleManifest(localDirectory);
        if (resolvedBundleManifest is not null)
        {
            EnsureCompatibleExistingMetadata(
                repoId,
                requestedRevision,
                resolvedCommitSha,
                resolvedBundleManifest.RepoId,
                resolvedBundleManifest.Revision,
                resolvedBundleManifest.ResolvedCommitSha,
                localDirectory);
        }
    }

    private static void EnsureCompatibleExistingMetadata(
        string repoId,
        string requestedRevision,
        string? resolvedCommitSha,
        string existingRepoId,
        string existingRequestedRevision,
        string? existingResolvedCommitSha,
        string localDirectory)
    {
        if (!string.Equals(existingRepoId, repoId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Local directory '{localDirectory}' already contains files pinned to repository '{existingRepoId}'. Use a different directory for '{repoId}'.");
        }

        if (!string.IsNullOrEmpty(resolvedCommitSha) && !string.IsNullOrEmpty(existingResolvedCommitSha))
        {
            if (!string.Equals(existingResolvedCommitSha, resolvedCommitSha, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Local directory '{localDirectory}' is pinned to commit '{existingResolvedCommitSha}', but '{requestedRevision}' resolved to '{resolvedCommitSha}'. Use a different directory or clear the existing files.");
            }

            return;
        }

        if (!string.Equals(existingRequestedRevision, requestedRevision, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Local directory '{localDirectory}' already contains files for revision '{existingRequestedRevision}'. Use a different directory for revision '{requestedRevision}'.");
        }
    }

    private static bool DirectoryHasModelContent(string localDirectory)
    {
        return Directory.EnumerateFiles(localDirectory, "*", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Any(name =>
                !string.Equals(name, HuggingFaceMetadataFileNames.DownloadResolutionMetadata, StringComparison.Ordinal)
                && !string.Equals(name, HuggingFaceMetadataFileNames.ResolvedBundleManifest, StringComparison.Ordinal));
    }

    private static bool HasResolutionMetadata(string localDirectory)
    {
        return File.Exists(Path.Combine(localDirectory, HuggingFaceMetadataFileNames.DownloadResolutionMetadata))
            || File.Exists(Path.Combine(localDirectory, HuggingFaceMetadataFileNames.ResolvedBundleManifest));
    }

    private static DownloadResolutionMetadata? TryReadDownloadResolutionMetadata(string localDirectory)
    {
        var metadataPath = Path.Combine(localDirectory, HuggingFaceMetadataFileNames.DownloadResolutionMetadata);
        if (!File.Exists(metadataPath))
            return null;

        try
        {
            var json = File.ReadAllText(metadataPath);
            return JsonSerializer.Deserialize<DownloadResolutionMetadata>(json, MetadataJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static ResolvedModelBundleManifest? TryReadResolvedBundleManifest(string localDirectory)
    {
        var metadataPath = Path.Combine(localDirectory, HuggingFaceMetadataFileNames.ResolvedBundleManifest);
        if (!File.Exists(metadataPath))
            return null;

        try
        {
            var json = File.ReadAllText(metadataPath);
            return JsonSerializer.Deserialize<ResolvedModelBundleManifest>(json, MetadataJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static async Task WriteDownloadResolutionMetadataAsync(
        DownloadRequest request,
        CancellationToken cancellationToken)
    {
        var metadata = new DownloadResolutionMetadata
        {
            RepoId = request.RepoId,
            RequestedRevision = request.Revision,
            ResolvedCommitSha = request.ResolvedCommitSha,
            GeneratedAtUtc = DateTimeOffset.UtcNow
        };

        var metadataPath = Path.Combine(request.LocalDirectory, HuggingFaceMetadataFileNames.DownloadResolutionMetadata);
        var json = JsonSerializer.Serialize(metadata, MetadataJsonOptions);
        await File.WriteAllTextAsync(metadataPath, json, cancellationToken).ConfigureAwait(false);
    }

    private static NormalizedBundleFile NormalizeBundleFile(ModelBundleFile file, string manifestRevision)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (string.IsNullOrWhiteSpace(file.Path))
            throw new ArgumentException("Manifest file paths cannot be null or empty.", nameof(file));

        var normalizedPath = file.Path.Replace('\\', '/');
        var normalizedRevision = string.IsNullOrWhiteSpace(file.Revision) ? manifestRevision : file.Revision!;

        if (string.IsNullOrWhiteSpace(normalizedRevision))
            throw new ArgumentException($"Manifest file '{normalizedPath}' did not resolve to a revision.", nameof(file));

        return new NormalizedBundleFile(
            normalizedPath,
            file.Required,
            file.Size,
            NormalizeSha256(file.Sha256),
            normalizedRevision);
    }

    private static string NormalizeSha256(string? sha256)
    {
        if (string.IsNullOrWhiteSpace(sha256))
            return string.Empty;

        var normalized = sha256.Trim().ToLowerInvariant();
        if (normalized.Length != 64 || normalized.Any(ch => !Uri.IsHexDigit(ch)))
            throw new ArgumentException($"Invalid SHA-256 hash '{sha256}'.", nameof(sha256));

        return normalized;
    }

    private static string GetLocalPath(string localDirectory, string relativePath)
        => Path.Combine(localDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static async Task<BundleFileValidationResult> ValidateLocalBundleFileAsync(
        string localPath,
        NormalizedBundleFile file,
        CancellationToken cancellationToken)
    {
        var actualSize = GetFileLength(localPath);
        if (file.Size is > 0 && actualSize != file.Size.Value)
        {
            return new BundleFileValidationResult(
                false,
                actualSize,
                null,
                $"{file.Path} (expected {file.Size.Value} bytes, got {actualSize} bytes)");
        }

        if (string.IsNullOrEmpty(file.Sha256))
            return new BundleFileValidationResult(true, actualSize, null, null);

        var actualSha256 = await ComputeSha256Async(localPath, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(actualSha256, file.Sha256, StringComparison.Ordinal))
        {
            return new BundleFileValidationResult(
                false,
                actualSize,
                actualSha256,
                $"{file.Path} (expected SHA-256 {file.Sha256}, got {actualSha256})");
        }

        return new BundleFileValidationResult(true, actualSize, actualSha256, null);
    }

    private static async Task<BundleFileValidationResult> ValidateDownloadedBundleFileAsync(
        string path,
        string filePath,
        BundleFileValidation validation,
        CancellationToken cancellationToken)
    {
        var actualSize = GetFileLength(path);
        if (validation.ExpectedSize is > 0 && actualSize != validation.ExpectedSize.Value)
        {
            return new BundleFileValidationResult(
                false,
                actualSize,
                null,
                $"Downloaded size mismatch for '{filePath}'. Expected {validation.ExpectedSize.Value} bytes but found {actualSize} bytes.");
        }

        if (string.IsNullOrEmpty(validation.ExpectedSha256))
            return new BundleFileValidationResult(true, actualSize, null, null);

        var actualSha256 = await ComputeSha256Async(path, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(actualSha256, validation.ExpectedSha256, StringComparison.Ordinal))
        {
            return new BundleFileValidationResult(
                false,
                actualSize,
                actualSha256,
                $"Downloaded SHA-256 mismatch for '{filePath}'. Expected {validation.ExpectedSha256} but found {actualSha256}.");
        }

        return new BundleFileValidationResult(true, actualSize, actualSha256, null);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: FileStreamBufferSize,
            useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static PartialDownloadMetadata? TryReadPartialMetadata(string metadataPath)
    {
        if (!File.Exists(metadataPath))
            return null;

        try
        {
            var json = File.ReadAllText(metadataPath);
            return JsonSerializer.Deserialize<PartialDownloadMetadata>(json);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static void PersistPartialMetadata(string metadataPath, PartialDownloadMetadata metadata)
    {
        var json = JsonSerializer.Serialize(metadata);
        File.WriteAllText(metadataPath, json);
    }

    private static void FinalizeCompletedDownload(string writePath, string localPath, string? metadataPath, bool useAtomicWrites)
    {
        if (useAtomicWrites)
            File.Move(writePath, localPath, overwrite: true);

        if (!string.IsNullOrEmpty(metadataPath))
            DeleteIfExists(metadataPath);
    }

    private static void CleanupDownloadArtifacts(string writePath, string? metadataPath, bool useAtomicWrites, bool keepPartial)
    {
        if (!keepPartial || !useAtomicWrites)
            DeleteIfExists(writePath);

        if (!keepPartial && !string.IsNullOrEmpty(metadataPath))
            DeleteIfExists(metadataPath);
    }

    private static void CleanupResumeArtifacts(string localPath)
    {
        DeleteIfExists(GetPartialFilePath(localPath));
        DeleteIfExists(GetPartialMetadataPath(localPath));
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private static long GetFileLength(string path)
    {
        return File.Exists(path) ? new FileInfo(path).Length : 0;
    }

    private static string GetAtomicTempPath(string localPath) => localPath + AtomicTempFileSuffix;

    private static string GetPartialFilePath(string localPath) => localPath + PartialFileSuffix;

    private static string GetPartialMetadataPath(string localPath) => localPath + PartialMetadataSuffix;

    private static HttpClient CreateHttpClient(HuggingFaceDownloaderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var client = new HttpClient { Timeout = options.Timeout };

        var token = options.ResolveToken();
        if (!string.IsNullOrEmpty(token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var userAgent = options.UserAgent ?? "ElBruno.HuggingFace.Downloader/1.0";
        client.DefaultRequestHeaders.UserAgent.Clear();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);

        return client;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }

    private sealed record RemoteFileInfo(long? ContentLength, string? EntityTag, bool SupportsRanges, string? ResolvedCommitSha);

    private sealed record BundleFileValidation(long? ExpectedSize, string? ExpectedSha256);

    private sealed record BundleFileValidationResult(bool IsValid, long Size, string? Sha256, string? ErrorMessage);

    private sealed record NormalizedBundleFile(
        string Path,
        bool Required,
        long? Size,
        string Sha256,
        string Revision);

    private sealed record DownloadState(
        string WritePath,
        string? MetadataPath,
        bool AllowResume,
        long ResumedBytes,
        string? EntityTag,
        long? TotalBytes,
        bool CompletedWithoutNetwork);

    private sealed record FileDownloadResult(long FileBytes, long ResumedBytes);

    private sealed class PartialDownloadMetadata
    {
        public required string Revision { get; init; }

        public string? ResolvedCommitSha { get; init; }

        public string? EntityTag { get; init; }

        public long? TotalBytes { get; init; }
    }
}
