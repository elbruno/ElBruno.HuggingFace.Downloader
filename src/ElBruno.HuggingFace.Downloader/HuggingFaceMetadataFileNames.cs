namespace ElBruno.HuggingFace;

/// <summary>
/// Well-known metadata files written by the downloader.
/// </summary>
public static class HuggingFaceMetadataFileNames
{
    /// <summary>
    /// Metadata written for direct file downloads.
    /// </summary>
    public const string DownloadResolutionMetadata = ".hf.download.resolved.json";

    /// <summary>
    /// Metadata written for manifest-driven bundle downloads.
    /// </summary>
    public const string ResolvedBundleManifest = ".hf.bundle.resolved.json";
}
