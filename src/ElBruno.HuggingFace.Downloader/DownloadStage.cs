namespace ElBruno.HuggingFace;

/// <summary>
/// Represents the current stage of a download operation.
/// </summary>
public enum DownloadStage
{
    /// <summary>Checking which files need to be downloaded.</summary>
    Checking,

    /// <summary>Downloading files from Hugging Face.</summary>
    Downloading,

    /// <summary>Validating downloaded files.</summary>
    Validating,

    /// <summary>All files downloaded and validated successfully.</summary>
    Complete,

    /// <summary>The download operation failed.</summary>
    Failed
}
