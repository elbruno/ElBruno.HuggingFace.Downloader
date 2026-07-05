using System.Text.Json;
using ElBruno.HuggingFace;
using ElBruno.HuggingFace.Cli.Models;

namespace ElBruno.HuggingFace.Cli.Services;

/// <summary>
/// Scans and manages the local cache directory for downloaded Hugging Face models.
/// Convention: each immediate subdirectory of the cache root represents one cached revision.
/// </summary>
public sealed class CacheManager
{
    private static readonly JsonSerializerOptions MetadataJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Returns aggregate metadata for every cached model in the cache directory.
    /// </summary>
    public IReadOnlyList<CachedModel> GetCachedModels(string cacheDir)
    {
        if (!Directory.Exists(cacheDir))
            return [];

        var models = new List<CachedModel>();

        foreach (var dir in Directory.EnumerateDirectories(cacheDir))
        {
            var info = BuildCachedModel(dir, includeFiles: false);
            if (info is not null)
                models.Add(info);
        }

        return models
            .OrderBy(model => model.RepoId ?? model.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(model => model.RequestedRevision ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Returns detailed metadata for a specific cached model, including per-file information.
    /// </summary>
    public CachedModel? GetModelDetails(string cacheDir, string repoId, string? revision = null)
    {
        var modelPath = ResolveModelPath(cacheDir, repoId, revision);
        if (modelPath is null || !Directory.Exists(modelPath))
            return null;

        return BuildCachedModel(modelPath, includeFiles: true);
    }

    /// <summary>
    /// Returns the cache directory path for a specific repo/revision when it exists locally.
    /// </summary>
    public string? FindModelPath(string cacheDir, string repoId, string? revision = null)
    {
        return ResolveModelPath(cacheDir, repoId, revision);
    }

    /// <summary>
    /// Builds the default cache directory path for a resolved repo revision.
    /// </summary>
    public static string GetDefaultModelDirectory(string cacheDir, string repoId, string resolvedRevision)
    {
        return Path.Combine(cacheDir, ComposeCacheDirectoryName(repoId, resolvedRevision));
    }

    /// <summary>
    /// Builds the directory name used for one cached repo revision.
    /// </summary>
    public static string ComposeCacheDirectoryName(string repoId, string resolvedRevision)
    {
        return $"{DefaultPathHelper.SanitizeModelName(repoId)}__{DefaultPathHelper.SanitizeModelName(resolvedRevision)}";
    }

    /// <summary>
    /// Deletes all files and subdirectories for the specified model.
    /// </summary>
    /// <returns><c>true</c> if at least one matching model directory was deleted; otherwise <c>false</c>.</returns>
    public bool DeleteModel(string cacheDir, string repoId, string? revision = null)
    {
        var modelPaths = ResolveModelPaths(cacheDir, repoId, revision);
        if (modelPaths.Count == 0)
            return false;

        foreach (var modelPath in modelPaths)
            Directory.Delete(modelPath, recursive: true);

        return true;
    }

    /// <summary>
    /// Deletes a single file from a cached model directory.
    /// </summary>
    /// <returns><c>true</c> if the file was found and deleted; otherwise <c>false</c>.</returns>
    public bool DeleteFile(string cacheDir, string repoId, string filePath, string? revision = null)
    {
        var modelPath = ResolveModelPath(cacheDir, repoId, revision);
        if (modelPath is null || !Directory.Exists(modelPath))
            return false;

        var fullPath = Path.GetFullPath(Path.Combine(modelPath, filePath));

        // Prevent path traversal
        if (!fullPath.StartsWith(Path.GetFullPath(modelPath), StringComparison.OrdinalIgnoreCase))
            return false;

        if (!File.Exists(fullPath))
            return false;

        File.Delete(fullPath);
        return true;
    }

    /// <summary>
    /// Deletes the entire cache directory contents.
    /// </summary>
    /// <returns>The number of model directories deleted.</returns>
    public int PurgeAll(string cacheDir)
    {
        if (!Directory.Exists(cacheDir))
            return 0;

        var dirs = Directory.GetDirectories(cacheDir);
        foreach (var dir in dirs)
        {
            Directory.Delete(dir, recursive: true);
        }

        // Also delete any loose files at the root level
        foreach (var file in Directory.GetFiles(cacheDir))
        {
            File.Delete(file);
        }

        return dirs.Length;
    }

    private static string? ResolveModelPath(string cacheDir, string repoId, string? revision)
    {
        return ResolveModelPaths(cacheDir, repoId, revision)
            .FirstOrDefault();
    }

    private static IReadOnlyList<string> ResolveModelPaths(string cacheDir, string repoId, string? revision)
    {
        if (!Directory.Exists(cacheDir))
            return [];

        var normalizedRequestedRevision = NormalizeRevision(revision);
        var normalizedRequestedCommitSha = NormalizeCommitSha(revision);
        var sanitizedRepoId = DefaultPathHelper.SanitizeModelName(repoId);

        return Directory.EnumerateDirectories(cacheDir)
            .Select(path => new CacheEntry(path, ReadResolutionMetadata(path)))
            .Where(entry => MatchesRepo(entry, repoId, sanitizedRepoId))
            .Where(entry => MatchesRevision(entry, normalizedRequestedRevision, normalizedRequestedCommitSha))
            .OrderByDescending(entry => new DirectoryInfo(entry.Path).LastWriteTimeUtc)
            .Select(entry => entry.Path)
            .ToList();
    }

    private static bool MatchesRepo(CacheEntry entry, string repoId, string sanitizedRepoId)
    {
        if (string.Equals(entry.Metadata?.RepoId, repoId, StringComparison.Ordinal))
            return true;

        var directoryName = Path.GetFileName(entry.Path);
        return string.Equals(directoryName, repoId, StringComparison.Ordinal)
            || string.Equals(directoryName, sanitizedRepoId, StringComparison.Ordinal)
            || directoryName.StartsWith($"{sanitizedRepoId}__", StringComparison.Ordinal);
    }

    private static bool MatchesRevision(
        CacheEntry entry,
        string? normalizedRequestedRevision,
        string? normalizedRequestedCommitSha)
    {
        if (normalizedRequestedRevision is null && normalizedRequestedCommitSha is null)
            return true;

        if (entry.Metadata is null)
            return false;

        return string.Equals(entry.Metadata.RequestedRevision, normalizedRequestedRevision, StringComparison.Ordinal)
            || (!string.IsNullOrEmpty(normalizedRequestedCommitSha)
                && string.Equals(entry.Metadata.ResolvedCommitSha, normalizedRequestedCommitSha, StringComparison.Ordinal));
    }

    private static CachedModel? BuildCachedModel(string modelDir, bool includeFiles)
    {
        var dirInfo = new DirectoryInfo(modelDir);
        var metadata = ReadResolutionMetadata(modelDir);
        var allFiles = dirInfo.GetFiles("*", SearchOption.AllDirectories)
            .Where(file => !IsMetadataFile(Path.GetFileName(file.FullName)))
            .ToArray();

        if (allFiles.Length == 0 && !includeFiles)
        {
            return new CachedModel
            {
                Name = dirInfo.Name,
                RepoId = metadata?.RepoId,
                RequestedRevision = metadata?.RequestedRevision,
                ResolvedCommitSha = metadata?.ResolvedCommitSha,
                FullPath = dirInfo.FullName,
                TotalSize = 0,
                FileCount = 0,
                LastModified = dirInfo.LastWriteTime,
                Files = [],
            };
        }

        long totalSize = 0;
        var lastModified = dirInfo.LastWriteTime;
        var fileInfos = new List<CachedFileInfo>(allFiles.Length);

        foreach (var file in allFiles)
        {
            totalSize += file.Length;
            if (file.LastWriteTime > lastModified)
                lastModified = file.LastWriteTime;

            if (includeFiles)
            {
                fileInfos.Add(new CachedFileInfo
                {
                    RelativePath = Path.GetRelativePath(dirInfo.FullName, file.FullName),
                    Size = file.Length,
                    LastModified = file.LastWriteTime,
                });
            }
        }

        return new CachedModel
        {
            Name = dirInfo.Name,
            RepoId = metadata?.RepoId,
            RequestedRevision = metadata?.RequestedRevision,
            ResolvedCommitSha = metadata?.ResolvedCommitSha,
            FullPath = dirInfo.FullName,
            TotalSize = totalSize,
            FileCount = allFiles.Length,
            LastModified = lastModified,
            Files = includeFiles ? fileInfos : [],
        };
    }

    private static CacheResolutionMetadata? ReadResolutionMetadata(string modelDir)
    {
        var downloadMetadataPath = Path.Combine(modelDir, HuggingFaceMetadataFileNames.DownloadResolutionMetadata);
        if (File.Exists(downloadMetadataPath))
        {
            try
            {
                var json = File.ReadAllText(downloadMetadataPath);
                var metadata = JsonSerializer.Deserialize<DownloadResolutionMetadata>(json, MetadataJsonOptions);
                if (metadata is not null)
                {
                    return new CacheResolutionMetadata(
                        metadata.RepoId,
                        NormalizeRevision(metadata.RequestedRevision),
                        NormalizeCommitSha(metadata.ResolvedCommitSha));
                }
            }
            catch (IOException)
            {
            }
            catch (JsonException)
            {
            }
        }

        var bundleMetadataPath = Path.Combine(modelDir, HuggingFaceMetadataFileNames.ResolvedBundleManifest);
        if (!File.Exists(bundleMetadataPath))
            return null;

        try
        {
            var json = File.ReadAllText(bundleMetadataPath);
            var manifest = JsonSerializer.Deserialize<ResolvedModelBundleManifest>(json, MetadataJsonOptions);
            return manifest is null
                ? null
                : new CacheResolutionMetadata(
                    manifest.RepoId,
                    NormalizeRevision(manifest.Revision),
                    NormalizeCommitSha(manifest.ResolvedCommitSha));
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? NormalizeRevision(string? revision)
    {
        return string.IsNullOrWhiteSpace(revision) ? null : revision.Trim();
    }

    private static string? NormalizeCommitSha(string? revision)
    {
        if (string.IsNullOrWhiteSpace(revision))
            return null;

        var normalized = revision.Trim().ToLowerInvariant();
        if (normalized.Length != 40 || normalized.Any(ch => !Uri.IsHexDigit(ch)))
            return null;

        return normalized;
    }

    private static bool IsMetadataFile(string fileName)
    {
        return string.Equals(fileName, HuggingFaceMetadataFileNames.DownloadResolutionMetadata, StringComparison.Ordinal)
            || string.Equals(fileName, HuggingFaceMetadataFileNames.ResolvedBundleManifest, StringComparison.Ordinal);
    }

    private sealed record CacheEntry(string Path, CacheResolutionMetadata? Metadata);

    private sealed record CacheResolutionMetadata(string RepoId, string? RequestedRevision, string? ResolvedCommitSha);
}
