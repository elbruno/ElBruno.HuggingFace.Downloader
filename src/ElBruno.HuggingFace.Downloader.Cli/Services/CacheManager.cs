using ElBruno.HuggingFace;
using ElBruno.HuggingFace.Cli.Models;

namespace ElBruno.HuggingFace.Cli.Services;

/// <summary>
/// Scans and manages the local cache directory for downloaded Hugging Face models.
/// Convention: each immediate subdirectory of the cache root represents one model.
/// </summary>
public sealed class CacheManager
{
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

        return models;
    }

    /// <summary>
    /// Returns detailed metadata for a specific cached model, including per-file information.
    /// </summary>
    public CachedModel? GetModelDetails(string cacheDir, string repoId)
    {
        var modelPath = ResolveModelPath(cacheDir, repoId);
        if (modelPath is null || !Directory.Exists(modelPath))
            return null;

        return BuildCachedModel(modelPath, includeFiles: true);
    }

    /// <summary>
    /// Deletes all files and subdirectories for the specified model.
    /// </summary>
    /// <returns><c>true</c> if the model directory was found and deleted; otherwise <c>false</c>.</returns>
    public bool DeleteModel(string cacheDir, string repoId)
    {
        var modelPath = ResolveModelPath(cacheDir, repoId);
        if (modelPath is null || !Directory.Exists(modelPath))
            return false;

        Directory.Delete(modelPath, recursive: true);
        return true;
    }

    /// <summary>
    /// Deletes a single file from a cached model directory.
    /// </summary>
    /// <returns><c>true</c> if the file was found and deleted; otherwise <c>false</c>.</returns>
    public bool DeleteFile(string cacheDir, string repoId, string filePath)
    {
        var modelPath = ResolveModelPath(cacheDir, repoId);
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

    /// <summary>
    /// Resolves a repo ID to a directory path inside the cache, checking both the sanitized
    /// name and a direct match.
    /// </summary>
    private static string? ResolveModelPath(string cacheDir, string repoId)
    {
        if (!Directory.Exists(cacheDir))
            return null;

        // Try sanitized name first (e.g. "microsoft/phi-2" → "microsoft_phi-2")
        var sanitized = DefaultPathHelper.SanitizeModelName(repoId);
        var sanitizedPath = Path.Combine(cacheDir, sanitized);
        if (Directory.Exists(sanitizedPath))
            return sanitizedPath;

        // Fall back to direct name match (in case the dir was named without sanitization)
        var directPath = Path.Combine(cacheDir, repoId);
        if (Directory.Exists(directPath))
            return directPath;

        return null;
    }

    private static CachedModel? BuildCachedModel(string modelDir, bool includeFiles)
    {
        var dirInfo = new DirectoryInfo(modelDir);
        var allFiles = dirInfo.GetFiles("*", SearchOption.AllDirectories);

        if (allFiles.Length == 0 && !includeFiles)
        {
            // Still report empty directories
            return new CachedModel
            {
                Name = dirInfo.Name,
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
            FullPath = dirInfo.FullName,
            TotalSize = totalSize,
            FileCount = allFiles.Length,
            LastModified = lastModified,
            Files = includeFiles ? fileInfos : [],
        };
    }
}
