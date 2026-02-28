using System.Net.Http.Headers;

namespace ElBruno.HuggingFace;

/// <summary>
/// Configuration options for <see cref="HuggingFaceDownloader"/>.
/// </summary>
public sealed class HuggingFaceDownloaderOptions
{
    private TimeSpan _timeout = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Hugging Face authentication token. When null, the downloader reads
    /// the <c>HF_TOKEN</c> environment variable automatically.
    /// </summary>
    public string? AuthToken { get; set; }

    /// <summary>
    /// HTTP request timeout. Defaults to 30 minutes. Must be greater than zero.
    /// </summary>
    public TimeSpan Timeout
    {
        get => _timeout;
        set
        {
            if (value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(Timeout), "Timeout must be greater than zero.");
            _timeout = value;
        }
    }

    /// <summary>
    /// When true, the downloader issues HEAD requests before downloading
    /// to resolve total size for accurate progress reporting. Defaults to true.
    /// </summary>
    public bool ResolveFileSizesBeforeDownload { get; set; } = true;

    /// <summary>
    /// Optional User-Agent string sent with HTTP requests.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Resolves the authentication token, falling back to the HF_TOKEN environment variable.
    /// </summary>
    internal string? ResolveToken()
    {
        return AuthToken ?? Environment.GetEnvironmentVariable("HF_TOKEN");
    }
}
