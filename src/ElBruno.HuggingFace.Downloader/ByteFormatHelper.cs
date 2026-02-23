namespace ElBruno.HuggingFace;

/// <summary>
/// Utility methods for formatting byte sizes.
/// </summary>
public static class ByteFormatHelper
{
    /// <summary>
    /// Formats a byte count into a human-readable string (e.g., "1.5 MB").
    /// </summary>
    public static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}
