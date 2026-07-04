namespace ElBruno.HuggingFace;

/// <summary>
/// Captures the resolved state of a bundle file after validation.
/// </summary>
public sealed record class ResolvedModelBundleFile
{
    /// <summary>
    /// Repository-relative file path.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Revision used for this file.
    /// </summary>
    public required string Revision { get; init; }

    /// <summary>
    /// Indicates whether the file is required by the manifest.
    /// </summary>
    public required bool Required { get; init; }

    /// <summary>
    /// Indicates whether the file exists locally after the bundle operation completes.
    /// </summary>
    public required bool Exists { get; init; }

    /// <summary>
    /// Resolved local file size in bytes when the file exists.
    /// </summary>
    public long? Size { get; init; }

    /// <summary>
    /// Resolved SHA-256 hash encoded as lowercase hexadecimal when the file exists.
    /// </summary>
    public string? Sha256 { get; init; }

    /// <summary>
    /// Indicates whether the file was downloaded during the current bundle operation.
    /// </summary>
    public bool DownloadedThisRun { get; init; }
}
