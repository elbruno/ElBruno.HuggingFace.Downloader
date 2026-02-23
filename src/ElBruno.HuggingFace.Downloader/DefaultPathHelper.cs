namespace ElBruno.HuggingFace;

/// <summary>
/// Helpers for determining default model cache directories per platform.
/// </summary>
public static class DefaultPathHelper
{
    /// <summary>
    /// Returns a default cache directory for the given application name.
    /// Windows: %LOCALAPPDATA%/{appName}/models,
    /// Linux/macOS: ~/.local/share/{appName}/models.
    /// </summary>
    /// <param name="appName">Application or library name (e.g., "ElBruno.QwenTTS").</param>
    public static string GetDefaultCacheDirectory(string appName)
    {
        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localAppData))
            {
                localAppData = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "AppData", "Local");
            }
            return Path.Combine(localAppData, appName, "models");
        }

        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrEmpty(dataHome))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(home))
            {
                home = Environment.GetEnvironmentVariable("HOME") ?? "/tmp";
            }
            dataHome = Path.Combine(home, ".local", "share");
        }
        return Path.Combine(dataHome, appName, "models");
    }

    /// <summary>
    /// Sanitizes a model name for use as a directory name by replacing invalid path characters.
    /// </summary>
    public static string SanitizeModelName(string modelName)
    {
        return modelName
            .Replace('/', '_')
            .Replace('\\', '_')
            .Replace(':', '_')
            .Replace('*', '_')
            .Replace('?', '_')
            .Replace('"', '_')
            .Replace('<', '_')
            .Replace('>', '_')
            .Replace('|', '_');
    }
}
