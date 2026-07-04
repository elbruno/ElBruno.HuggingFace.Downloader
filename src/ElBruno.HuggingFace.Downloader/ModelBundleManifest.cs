namespace ElBruno.HuggingFace;

/// <summary>
/// Describes a manifest-driven bundle of files to ensure from a single Hugging Face repository revision.
/// </summary>
public sealed record class ModelBundleManifest
{
    /// <summary>
    /// The Hugging Face repository ID (for example, <c>sentence-transformers/all-MiniLM-L6-v2</c>).
    /// </summary>
    public required string RepoId { get; init; }

    /// <summary>
    /// Default Git revision (branch, tag, or commit SHA) for bundle files.
    /// </summary>
    public string Revision { get; init; } = "main";

    /// <summary>
    /// Files included in the bundle.
    /// </summary>
    public required IReadOnlyList<ModelBundleFile> Files { get; init; }
}
