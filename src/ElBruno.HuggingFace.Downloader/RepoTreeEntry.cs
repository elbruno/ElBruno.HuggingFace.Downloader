namespace ElBruno.HuggingFace;

/// <summary>
/// Represents a single file or directory entry returned by the Hugging Face Hub tree API.
/// </summary>
/// <param name="Path">Path of the entry relative to the repository root.</param>
/// <param name="SizeBytes">Size of the entry in bytes, or <see langword="null"/> for directories.</param>
/// <param name="IsDirectory"><see langword="true"/> if the entry is a directory rather than a file.</param>
/// <param name="Oid">The git object ID (blob or tree SHA) for the entry, if provided by the API.</param>
public sealed record RepoTreeEntry(
    string Path,
    long? SizeBytes,
    bool IsDirectory,
    string? Oid);
