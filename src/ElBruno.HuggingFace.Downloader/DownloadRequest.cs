namespace ElBruno.HuggingFace;

/// <summary>
/// Describes a set of files to download from a Hugging Face repository.
/// </summary>
public sealed class DownloadRequest
{
    /// <summary>
    /// The Hugging Face repository ID (e.g., "sentence-transformers/all-MiniLM-L6-v2").
    /// </summary>
    public required string RepoId { get; init; }

    /// <summary>
    /// Local directory where downloaded files will be stored.
    /// </summary>
    public required string LocalDirectory { get; init; }

    /// <summary>
    /// Files that must be downloaded successfully. A failure to download any of these throws an exception.
    /// Paths are relative to the repository root (e.g., "onnx/model.onnx").
    /// </summary>
    public required IReadOnlyList<string> RequiredFiles { get; init; }

    /// <summary>
    /// Files that are downloaded on a best-effort basis. Failures are silently ignored.
    /// </summary>
    public IReadOnlyList<string>? OptionalFiles { get; init; }

    /// <summary>
    /// Git revision (branch, tag, or commit SHA) to download from. Defaults to "main".
    /// </summary>
    public string Revision { get; init; } = "main";

    /// <summary>
    /// Optional progress reporter for download status updates.
    /// </summary>
    public IProgress<DownloadProgress>? Progress { get; init; }

    /// <summary>
    /// When true, files are written to a temp path first and renamed on completion to avoid partial downloads.
    /// Defaults to true.
    /// </summary>
    public bool UseAtomicWrites { get; init; } = true;
}
