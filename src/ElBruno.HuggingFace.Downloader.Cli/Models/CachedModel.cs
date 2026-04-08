namespace ElBruno.HuggingFace.Cli.Models;

/// <summary>
/// Represents metadata about a single file within a cached model directory.
/// </summary>
public sealed class CachedFileInfo
{
    /// <summary>Path of the file relative to the model directory.</summary>
    public required string RelativePath { get; init; }

    /// <summary>File size in bytes.</summary>
    public required long Size { get; init; }

    /// <summary>Last modification timestamp of the file.</summary>
    public required DateTime LastModified { get; init; }
}

/// <summary>
/// Represents a cached model directory with aggregate metadata and optional file details.
/// </summary>
public sealed class CachedModel
{
    /// <summary>Directory name (sanitized repo ID).</summary>
    public required string Name { get; init; }

    /// <summary>Full path to the model's cache directory.</summary>
    public required string FullPath { get; init; }

    /// <summary>Total size of all files in bytes.</summary>
    public required long TotalSize { get; init; }

    /// <summary>Number of files in the model directory.</summary>
    public required int FileCount { get; init; }

    /// <summary>Most recent modification timestamp among all files.</summary>
    public required DateTime LastModified { get; init; }

    /// <summary>Detailed list of individual files (populated by GetModelDetails).</summary>
    public IReadOnlyList<CachedFileInfo> Files { get; init; } = [];
}
