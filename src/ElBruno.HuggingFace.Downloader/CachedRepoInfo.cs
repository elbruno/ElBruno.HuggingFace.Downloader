namespace ElBruno.HuggingFace;

/// <summary>
/// Represents aggregate metadata for a cached Hugging Face repository.
/// </summary>
/// <param name="LocalDirectory">Absolute path of the local cached repository directory.</param>
/// <param name="TotalSizeBytes">Total size of all files in the cached repository, in bytes.</param>
/// <param name="LastModified">Most recent modification timestamp found in the repository directory tree.</param>
public sealed record CachedRepoInfo(
    string LocalDirectory,
    long TotalSizeBytes,
    DateTimeOffset LastModified);
