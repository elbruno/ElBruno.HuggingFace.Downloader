using Xunit;
using ElBruno.HuggingFace.Cli.Models;
using ElBruno.HuggingFace.Cli.Services;

namespace ElBruno.HuggingFace.Cli.Tests;

/// <summary>
/// Tests for <see cref="ConfigManager"/> — persistent config load/save/reset and value logic.
/// Disk-touching tests back up and restore the real config file via IDisposable.
/// </summary>
[Collection("ConfigFile")]
public sealed class ConfigManagerTests : IDisposable
{
    private readonly string _configPath;
    private readonly string? _originalContent;
    private readonly bool _originalExisted;

    public ConfigManagerTests()
    {
        _configPath = ConfigManager.GetConfigPath();
        _originalExisted = File.Exists(_configPath);
        _originalContent = _originalExisted ? File.ReadAllText(_configPath) : null;

        // Start each test with a clean slate
        if (_originalExisted)
            File.Delete(_configPath);
    }

    public void Dispose()
    {
        // Restore original config state
        if (_originalExisted && _originalContent is not null)
        {
            var dir = Path.GetDirectoryName(_configPath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(_configPath, _originalContent);
        }
        else if (File.Exists(_configPath))
        {
            File.Delete(_configPath);
        }
    }

    // ── Load ────────────────────────────────────────────────────────

    [Fact]
    public void Load_NoConfigFile_ReturnsDefaults()
    {
        var manager = new ConfigManager();

        var config = manager.Load();

        Assert.Null(config.CacheDirectory);
        Assert.Null(config.DefaultToken);
        Assert.Equal("main", config.DefaultRevision);
        Assert.False(config.NoProgress);
    }

    // ── Save ────────────────────────────────────────────────────────

    [Fact]
    public void Save_CreatesConfigFileAndDirectory()
    {
        var dir = Path.GetDirectoryName(_configPath)!;
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);

        var manager = new ConfigManager();
        var config = new CliConfig { DefaultRevision = "test-branch" };

        manager.Save(config);

        Assert.True(File.Exists(_configPath));
    }

    // ── Load roundtrip ──────────────────────────────────────────────

    [Fact]
    public void Load_RoundtripsSavedValues()
    {
        var manager = new ConfigManager();
        var original = new CliConfig
        {
            CacheDirectory = @"C:\my\cache",
            DefaultToken = "hf_test_token_123",
            DefaultRevision = "dev",
            NoProgress = true,
        };

        manager.Save(original);
        var loaded = manager.Load();

        Assert.Equal(original.CacheDirectory, loaded.CacheDirectory);
        Assert.Equal(original.DefaultToken, loaded.DefaultToken);
        Assert.Equal(original.DefaultRevision, loaded.DefaultRevision);
        Assert.Equal(original.NoProgress, loaded.NoProgress);
    }

    // ── SetValue ────────────────────────────────────────────────────

    [Fact]
    public void SetValue_CacheDir_SetsValue()
    {
        var manager = new ConfigManager();
        var config = new CliConfig();

        var result = manager.SetValue(config, "cache-dir", @"D:\models");

        Assert.True(result);
        Assert.Equal(@"D:\models", config.CacheDirectory);
    }

    [Fact]
    public void SetValue_DefaultToken_SetsValue()
    {
        var manager = new ConfigManager();
        var config = new CliConfig();

        var result = manager.SetValue(config, "default-token", "hf_abc123");

        Assert.True(result);
        Assert.Equal("hf_abc123", config.DefaultToken);
    }

    [Fact]
    public void SetValue_DefaultRevision_SetsValue()
    {
        var manager = new ConfigManager();
        var config = new CliConfig();

        var result = manager.SetValue(config, "default-revision", "v2.0");

        Assert.True(result);
        Assert.Equal("v2.0", config.DefaultRevision);
    }

    [Fact]
    public void SetValue_NoProgress_SetsTrue()
    {
        var manager = new ConfigManager();
        var config = new CliConfig();

        var result = manager.SetValue(config, "no-progress", "true");

        Assert.True(result);
        Assert.True(config.NoProgress);
    }

    [Fact]
    public void SetValue_NoProgress_SetsFalse()
    {
        var manager = new ConfigManager();
        var config = new CliConfig { NoProgress = true };

        var result = manager.SetValue(config, "no-progress", "false");

        Assert.True(result);
        Assert.False(config.NoProgress);
    }

    [Fact]
    public void SetValue_UnknownKey_ReturnsFalse()
    {
        var manager = new ConfigManager();
        var config = new CliConfig();

        var result = manager.SetValue(config, "nonexistent-key", "value");

        Assert.False(result);
    }

    [Fact]
    public void SetValue_NoProgress_InvalidBoolean_ReturnsFalse()
    {
        var manager = new ConfigManager();
        var config = new CliConfig();

        var result = manager.SetValue(config, "no-progress", "not-a-bool");

        Assert.False(result);
    }

    [Fact]
    public void SetValue_CacheDir_EmptyString_SetsNull()
    {
        var manager = new ConfigManager();
        var config = new CliConfig { CacheDirectory = @"C:\old" };

        var result = manager.SetValue(config, "cache-dir", "");

        Assert.True(result);
        Assert.Null(config.CacheDirectory);
    }

    [Fact]
    public void SetValue_DefaultToken_EmptyString_SetsNull()
    {
        var manager = new ConfigManager();
        var config = new CliConfig { DefaultToken = "old-token" };

        var result = manager.SetValue(config, "default-token", "");

        Assert.True(result);
        Assert.Null(config.DefaultToken);
    }

    [Fact]
    public void SetValue_DefaultRevision_EmptyString_SetsMain()
    {
        var manager = new ConfigManager();
        var config = new CliConfig { DefaultRevision = "dev" };

        var result = manager.SetValue(config, "default-revision", "");

        Assert.True(result);
        Assert.Equal("main", config.DefaultRevision);
    }

    // ── GetValue ────────────────────────────────────────────────────

    [Fact]
    public void GetValue_TokenSet_MasksValue()
    {
        var config = new CliConfig { DefaultToken = "hf_secret_token" };

        var result = ConfigManager.GetValue(config, "default-token");

        Assert.Equal("****", result);
    }

    [Fact]
    public void GetValue_TokenNull_ReturnsNotSet()
    {
        var config = new CliConfig { DefaultToken = null };

        var result = ConfigManager.GetValue(config, "default-token");

        Assert.Equal("(not set)", result);
    }

    [Fact]
    public void GetValue_CacheDirNull_ReturnsPlatformDefault()
    {
        var config = new CliConfig { CacheDirectory = null };

        var result = ConfigManager.GetValue(config, "cache-dir");

        Assert.Equal("(platform default)", result);
    }

    [Fact]
    public void GetValue_CacheDirSet_ReturnsActualPath()
    {
        var config = new CliConfig { CacheDirectory = @"C:\custom\cache" };

        var result = ConfigManager.GetValue(config, "cache-dir");

        Assert.Equal(@"C:\custom\cache", result);
    }

    [Fact]
    public void GetValue_DefaultRevision_ReturnsValue()
    {
        var config = new CliConfig { DefaultRevision = "v3" };

        var result = ConfigManager.GetValue(config, "default-revision");

        Assert.Equal("v3", result);
    }

    [Fact]
    public void GetValue_NoProgress_ReturnsBoolString()
    {
        var config = new CliConfig { NoProgress = true };

        var result = ConfigManager.GetValue(config, "no-progress");

        Assert.Equal("true", result);
    }

    [Fact]
    public void GetValue_UnknownKey_ReturnsNull()
    {
        var config = new CliConfig();

        var result = ConfigManager.GetValue(config, "totally-fake-key");

        Assert.Null(result);
    }

    // ── Reset ───────────────────────────────────────────────────────

    [Fact]
    public void Reset_ConfigFileExists_DeletesAndReturnsTrue()
    {
        var manager = new ConfigManager();
        manager.Save(new CliConfig { DefaultRevision = "disposable" });
        Assert.True(File.Exists(_configPath));

        var result = manager.Reset();

        Assert.True(result);
        Assert.False(File.Exists(_configPath));
    }

    [Fact]
    public void Reset_NoConfigFile_ReturnsFalse()
    {
        var manager = new ConfigManager();

        var result = manager.Reset();

        Assert.False(result);
    }
}
