namespace ElBruno.HuggingFace;

/// <summary>
/// Describes the outcome of ensuring a model bundle locally.
/// </summary>
public sealed record class ModelBundleResult
{
    /// <summary>
    /// Local directory where the bundle was ensured.
    /// </summary>
    public required string LocalDirectory { get; init; }

    /// <summary>
    /// Path to the written resolved manifest file.
    /// </summary>
    public required string ResolvedManifestPath { get; init; }

    /// <summary>
    /// Resolved manifest content written to disk.
    /// </summary>
    public required ResolvedModelBundleManifest ResolvedManifest { get; init; }

    /// <summary>
    /// Number of files downloaded during this run.
    /// </summary>
    public int DownloadedFileCount { get; init; }

    /// <summary>
    /// Number of files that were already valid locally and reused.
    /// </summary>
    public int ReusedFileCount { get; init; }

    /// <summary>
    /// Number of optional files that remained unavailable after the run.
    /// </summary>
    public int MissingOptionalFileCount { get; init; }
}
