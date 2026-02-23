using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElBruno.HuggingFace;

/// <summary>
/// Downloads files from Hugging Face Hub repositories.
/// </summary>
public sealed class HuggingFaceDownloader : IDisposable
{
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
        _ownsHttpClient = ownsHttpClient;
        _options = options;
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

        // Build combined file list with required/optional flag
        var allFiles = new List<(string path, bool required)>();
        foreach (var f in request.RequiredFiles)
            allFiles.Add((f, true));
        if (request.OptionalFiles is not null)
            foreach (var f in request.OptionalFiles)
                allFiles.Add((f, false));

        // Filter to only missing files
        var missingFiles = allFiles
            .Where(f => !File.Exists(Path.Combine(request.LocalDirectory, f.path.Replace('/', Path.DirectorySeparatorChar))))
            .ToList();

        if (missingFiles.Count == 0)
        {
            request.Progress?.Report(new DownloadProgress
            {
                Stage = DownloadStage.Complete,
                PercentComplete = 100,
                Message = "All files already present."
            });
            _logger.LogDebug("All files already present in {Directory}", request.LocalDirectory);
            return;
        }

        // Resolve total size via HEAD requests
        long totalBytes = 0;
        var fileSizes = new Dictionary<string, long>();

        if (_options.ResolveFileSizesBeforeDownload)
        {
            request.Progress?.Report(new DownloadProgress
            {
                Stage = DownloadStage.Checking,
                Message = $"Checking {missingFiles.Count} files from {request.RepoId}..."
            });

            foreach (var (path, _) in missingFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var url = HuggingFaceUrlBuilder.GetFileUrl(request.RepoId, path, request.Revision);
                try
                {
                    using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
                    using var headResponse = await _httpClient.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                    if (headResponse.IsSuccessStatusCode && headResponse.Content.Headers.ContentLength is > 0)
                    {
                        var size = headResponse.Content.Headers.ContentLength.Value;
                        fileSizes[path] = size;
                        totalBytes += size;
                    }
                }
                catch
                {
                    // HEAD failed — continue without size info
                }
            }
        }

        // Download each file
        long downloadedBytes = 0;

        for (int i = 0; i < missingFiles.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (filePath, required) = missingFiles[i];
            var localPath = Path.Combine(request.LocalDirectory, filePath.Replace('/', Path.DirectorySeparatorChar));
            var localDir = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(localDir))
                Directory.CreateDirectory(localDir);

            var url = HuggingFaceUrlBuilder.GetFileUrl(request.RepoId, filePath, request.Revision);

            request.Progress?.Report(new DownloadProgress
            {
                Stage = DownloadStage.Downloading,
                PercentComplete = totalBytes > 0 ? (double)downloadedBytes / totalBytes * 100 : 0,
                BytesDownloaded = downloadedBytes,
                TotalBytes = totalBytes,
                CurrentFile = filePath,
                CurrentFileIndex = i + 1,
                TotalFileCount = missingFiles.Count,
                Message = $"[{i + 1}/{missingFiles.Count}] Downloading {filePath}..."
            });

            _logger.LogInformation("Downloading {File} from {Url}", filePath, url);

            try
            {
                var fileDownloadedBytes = await DownloadSingleFileAsync(
                    url, localPath, filePath, request, i, missingFiles.Count,
                    fileSizes.GetValueOrDefault(filePath), downloadedBytes, totalBytes,
                    cancellationToken).ConfigureAwait(false);

                downloadedBytes += fileDownloadedBytes;
                _logger.LogInformation("Successfully downloaded {File}", filePath);
            }
            catch (HttpRequestException) when (!required)
            {
                _logger.LogWarning("Optional file {File} failed to download, skipping", filePath);
                continue;
            }
            catch (HttpRequestException ex) when (required)
            {
                var statusCode = ex.StatusCode;
                if (statusCode == System.Net.HttpStatusCode.Unauthorized || statusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    throw new InvalidOperationException(
                        $"Access denied downloading '{filePath}' from https://huggingface.co/{request.RepoId}. " +
                        "The repository may be private or gated. Set the HF_TOKEN environment variable or pass AuthToken in options.",
                        ex);
                }

                if (statusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new InvalidOperationException(
                        $"File '{filePath}' not found (404) at https://huggingface.co/{request.RepoId}.",
                        ex);
                }

                throw new InvalidOperationException(
                    $"Failed to download required file '{filePath}' from https://huggingface.co/{request.RepoId}: {ex.Message}",
                    ex);
            }
        }

        // Validate required files
        request.Progress?.Report(new DownloadProgress
        {
            Stage = DownloadStage.Validating,
            PercentComplete = 99,
            BytesDownloaded = downloadedBytes,
            TotalBytes = totalBytes,
            Message = "Validating downloaded files..."
        });

        var stillMissing = request.RequiredFiles
            .Where(f => !File.Exists(Path.Combine(request.LocalDirectory, f.Replace('/', Path.DirectorySeparatorChar))))
            .ToList();

        if (stillMissing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Download incomplete. Missing required files: {string.Join(", ", stillMissing)}");
        }

        request.Progress?.Report(new DownloadProgress
        {
            Stage = DownloadStage.Complete,
            PercentComplete = 100,
            BytesDownloaded = downloadedBytes,
            TotalBytes = totalBytes,
            Message = "All files downloaded and validated."
        });

        _logger.LogInformation("Download complete for {RepoId} — {FileCount} files, {Bytes}",
            request.RepoId, missingFiles.Count, ByteFormatHelper.FormatBytes(downloadedBytes));
    }

    private async Task<long> DownloadSingleFileAsync(
        string url, string localPath, string filePath,
        DownloadRequest request, int fileIndex, int totalFiles,
        long expectedFileSize, long previouslyDownloaded, long totalBytes,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Failed to download file. Status: {response.StatusCode}, URL: {url}",
                inner: null,
                statusCode: response.StatusCode);
        }

        var fileSize = response.Content.Headers.ContentLength ?? expectedFileSize;
        long fileDownloaded = 0;

        var writePath = request.UseAtomicWrites ? localPath + ".tmp" : localPath;

        try
        {
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var fileStream = new FileStream(writePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            var buffer = new byte[81920];
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                fileDownloaded += bytesRead;

                request.Progress?.Report(new DownloadProgress
                {
                    Stage = DownloadStage.Downloading,
                    PercentComplete = totalBytes > 0 ? (double)(previouslyDownloaded + fileDownloaded) / totalBytes * 100 : 0,
                    BytesDownloaded = previouslyDownloaded + fileDownloaded,
                    TotalBytes = totalBytes,
                    CurrentFile = filePath,
                    CurrentFileIndex = fileIndex + 1,
                    TotalFileCount = totalFiles,
                    Message = $"[{fileIndex + 1}/{totalFiles}] {filePath} — {ByteFormatHelper.FormatBytes(fileDownloaded)}/{ByteFormatHelper.FormatBytes(fileSize)}"
                });
            }

            await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (File.Exists(writePath))
            {
                try { File.Delete(writePath); } catch { /* cleanup best-effort */ }
            }
            throw;
        }

        if (request.UseAtomicWrites)
        {
            File.Move(writePath, localPath, overwrite: true);
        }

        return fileDownloaded;
    }

    private static HttpClient CreateHttpClient(HuggingFaceDownloaderOptions options)
    {
        var client = new HttpClient { Timeout = options.Timeout };

        var token = options.ResolveToken();
        if (!string.IsNullOrEmpty(token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (!string.IsNullOrEmpty(options.UserAgent))
            client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
        else
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ElBruno.HuggingFace.Downloader/1.0");

        return client;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }
}
