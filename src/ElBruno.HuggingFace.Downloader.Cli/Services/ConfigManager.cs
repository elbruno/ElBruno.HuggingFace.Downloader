using System.Text.Json;
using ElBruno.HuggingFace.Cli.Models;

namespace ElBruno.HuggingFace.Cli.Services;

/// <summary>
/// Manages persistent CLI configuration stored as a JSON file in the
/// platform-appropriate config directory.
/// </summary>
public sealed class ConfigManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Gets the directory where the config file is stored.
    /// Windows: %APPDATA%\hfdownload, Linux/macOS: ~/.config/hfdownload
    /// </summary>
    public static string GetConfigDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "hfdownload");
        }

        // Linux/macOS — respect XDG_CONFIG_HOME if set
        var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrEmpty(xdgConfig))
        {
            return Path.Combine(xdgConfig, "hfdownload");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "hfdownload");
    }

    /// <summary>Gets the full path to the config file.</summary>
    public static string GetConfigPath() => Path.Combine(GetConfigDirectory(), "config.json");

    /// <summary>Loads the current configuration, returning defaults if no config file exists.</summary>
    public CliConfig Load()
    {
        var path = GetConfigPath();
        if (!File.Exists(path))
            return new CliConfig();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<CliConfig>(json) ?? new CliConfig();
        }
        catch (JsonException)
        {
            return new CliConfig();
        }
    }

    /// <summary>Saves the configuration to disk, creating the directory if needed.</summary>
    public void Save(CliConfig config)
    {
        var path = GetConfigPath();
        var dir = Path.GetDirectoryName(path)!;

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>Resets the configuration by deleting the config file.</summary>
    /// <returns><c>true</c> if the file existed and was deleted.</returns>
    public bool Reset()
    {
        var path = GetConfigPath();
        if (!File.Exists(path))
            return false;

        File.Delete(path);
        return true;
    }

    /// <summary>
    /// Sets a single configuration value by key.
    /// Returns <c>true</c> if the key was recognized and set; <c>false</c> otherwise.
    /// </summary>
    public bool SetValue(CliConfig config, string key, string value)
    {
        switch (key.ToLowerInvariant())
        {
            case "cache-dir":
                config.CacheDirectory = string.IsNullOrWhiteSpace(value) ? null : value;
                return true;

            case "default-token":
                config.DefaultToken = string.IsNullOrWhiteSpace(value) ? null : value;
                return true;

            case "default-revision":
                config.DefaultRevision = string.IsNullOrWhiteSpace(value) ? "main" : value;
                return true;

            case "no-progress":
                if (bool.TryParse(value, out var noProgress))
                {
                    config.NoProgress = noProgress;
                    return true;
                }
                return false;

            default:
                return false;
        }
    }

    /// <summary>
    /// Gets a single configuration value by key, or <c>null</c> if unrecognized.
    /// </summary>
    public static string? GetValue(CliConfig config, string key)
    {
        return key.ToLowerInvariant() switch
        {
            "cache-dir" => config.CacheDirectory ?? "(platform default)",
            "default-token" => config.DefaultToken is not null ? "****" : "(not set)",
            "default-revision" => config.DefaultRevision,
            "no-progress" => config.NoProgress.ToString().ToLowerInvariant(),
            _ => null,
        };
    }
}
