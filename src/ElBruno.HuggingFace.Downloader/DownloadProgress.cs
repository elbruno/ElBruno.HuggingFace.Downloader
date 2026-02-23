namespace ElBruno.HuggingFace;

/// <summary>
/// Reports progress during file downloads from Hugging Face.
/// </summary>
public sealed class DownloadProgress
{
    /// <summary>Current stage of the download process.</summary>
    public DownloadStage Stage { get; init; }

    /// <summary>Overall completion percentage (0–100).</summary>
    public double PercentComplete { get; init; }

    /// <summary>Total bytes downloaded so far across all files.</summary>
    public long BytesDownloaded { get; init; }

    /// <summary>Total bytes expected across all files (0 if unknown).</summary>
    public long TotalBytes { get; init; }

    /// <summary>Name of the file currently being downloaded, or null.</summary>
    public string? CurrentFile { get; init; }

    /// <summary>1-based index of the current file being downloaded.</summary>
    public int CurrentFileIndex { get; init; }

    /// <summary>Total number of files to download.</summary>
    public int TotalFileCount { get; init; }

    /// <summary>Optional human-readable message describing the current operation.</summary>
    public string? Message { get; init; }
}
