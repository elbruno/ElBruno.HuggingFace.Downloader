namespace ElBruno.HuggingFace;

/// <summary>
/// Records the fully resolved state of a model bundle after validation.
/// </summary>
public sealed record class ResolvedModelBundleManifest
{
    /// <summary>
    /// The Hugging Face repository ID for the bundle.
    /// </summary>
    public required string RepoId { get; init; }

    /// <summary>
    /// The single validated revision shared by all files in the bundle.
    /// </summary>
    public required string Revision { get; init; }

    /// <summary>
    /// UTC timestamp when the resolved manifest was written.
    /// </summary>
    public required DateTimeOffset GeneratedAtUtc { get; init; }

    /// <summary>
    /// Resolved file entries.
    /// </summary>
    public required IReadOnlyList<ResolvedModelBundleFile> Files { get; init; }
}
