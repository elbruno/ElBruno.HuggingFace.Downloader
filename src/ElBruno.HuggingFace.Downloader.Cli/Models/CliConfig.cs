using System.Text.Json.Serialization;

namespace ElBruno.HuggingFace.Cli.Models;

/// <summary>
/// Persistent configuration for the hfdownload CLI tool.
/// Stored as JSON in a platform-appropriate config directory.
/// </summary>
public sealed class CliConfig
{
    /// <summary>Default cache directory override. When <c>null</c>, uses the platform default.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CacheDirectory { get; set; }

    /// <summary>Default Hugging Face auth token. When <c>null</c>, falls back to HF_TOKEN env var.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DefaultToken { get; set; }

    /// <summary>Default Git revision for downloads.</summary>
    public string DefaultRevision { get; set; } = "main";

    /// <summary>Whether to suppress progress bars by default.</summary>
    public bool NoProgress { get; set; }

    /// <summary>Known configuration keys and their descriptions.</summary>
    public static IReadOnlyDictionary<string, string> KnownKeys { get; } = new Dictionary<string, string>
    {
        ["cache-dir"] = "Default cache directory (overrides platform default)",
        ["default-token"] = "Default Hugging Face auth token",
        ["default-revision"] = "Default Git revision for downloads (default: main)",
        ["no-progress"] = "Suppress progress bars by default (true/false)",
    };
}
