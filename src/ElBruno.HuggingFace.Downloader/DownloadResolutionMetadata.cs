namespace ElBruno.HuggingFace;

/// <summary>
/// Stores the resolved revision metadata written alongside downloaded files.
/// </summary>
public sealed record class DownloadResolutionMetadata
{
    /// <summary>
    /// The Hugging Face repository ID for the downloaded files.
    /// </summary>
    public required string RepoId { get; init; }

    /// <summary>
    /// The requested branch, tag, or commit SHA used for the download.
    /// </summary>
    public required string RequestedRevision { get; init; }

    /// <summary>
    /// The immutable commit SHA resolved from <see cref="RequestedRevision"/> when available.
    /// </summary>
    public string? ResolvedCommitSha { get; init; }

    /// <summary>
    /// UTC timestamp when the metadata was written.
    /// </summary>
    public required DateTimeOffset GeneratedAtUtc { get; init; }
}
