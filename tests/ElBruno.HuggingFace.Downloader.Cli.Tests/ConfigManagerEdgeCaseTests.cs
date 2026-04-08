using Xunit;
using ElBruno.HuggingFace.Cli.Models;
using ElBruno.HuggingFace.Cli.Services;

namespace ElBruno.HuggingFace.Cli.Tests;

/// <summary>
/// Edge case tests for <see cref="ConfigManager"/> — corruption, case-insensitivity, rapid cycles.
/// </summary>
[Collection("ConfigFile")]
public sealed class ConfigManagerEdgeCaseTests : IDisposable
{
    private readonly string _configPath;
    private readonly string? _originalContent;
    private readonly bool _originalExisted;

    public ConfigManagerEdgeCaseTests()
    {
        _configPath = ConfigManager.GetConfigPath();
        _originalExisted = File.Exists(_configPath);
        _originalContent = _originalExisted ? File.ReadAllText(_configPath) : null;

        if (_originalExisted)
            File.Delete(_configPath);
    }

    public void Dispose()
    {
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

    // ── Corrupted config file ───────────────────────────────────────

    [Fact]
    public void Load_CorruptedJsonConfig_ReturnsDefaults()
    {
        var dir = Path.GetDirectoryName(_configPath)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.WriteAllText(_configPath, "NOT VALID JSON {{{");

        var manager = new ConfigManager();
        var config = manager.Load();

        Assert.Null(config.CacheDirectory);
        Assert.Null(config.DefaultToken);
        Assert.Equal("main", config.DefaultRevision);
        Assert.False(config.NoProgress);
    }

    // ── SetValue case insensitivity ─────────────────────────────────

    [Theory]
    [InlineData("Cache-Dir")]
    [InlineData("CACHE-DIR")]
    [InlineData("cache-dir")]
    [InlineData("Cache-dir")]
    public void SetValue_CaseInsensitiveKey_Works(string key)
    {
        var manager = new ConfigManager();
        var config = new CliConfig();

        var result = manager.SetValue(config, key, @"C:\test");

        Assert.True(result);
        Assert.Equal(@"C:\test", config.CacheDirectory);
    }

    [Theory]
    [InlineData("Default-Token")]
    [InlineData("DEFAULT-TOKEN")]
    public void SetValue_DefaultToken_CaseInsensitive_Works(string key)
    {
        var manager = new ConfigManager();
        var config = new CliConfig();

        var result = manager.SetValue(config, key, "my-token");

        Assert.True(result);
        Assert.Equal("my-token", config.DefaultToken);
    }

    // ── GetConfigDirectory returns platform-appropriate path ────────

    [Fact]
    public void GetConfigDirectory_ReturnsNonEmptyPath()
    {
        var dir = ConfigManager.GetConfigDirectory();

        Assert.False(string.IsNullOrWhiteSpace(dir));
        Assert.Contains("hfdownload", dir);
    }

    // ── Save overwrites existing config file ────────────────────────

    [Fact]
    public void Save_Overwrites_ExistingConfigFile()
    {
        var manager = new ConfigManager();
        var first = new CliConfig { DefaultRevision = "first" };
        manager.Save(first);

        var second = new CliConfig { DefaultRevision = "second" };
        manager.Save(second);

        var loaded = manager.Load();
        Assert.Equal("second", loaded.DefaultRevision);
    }

    // ── Rapid save/load cycles ──────────────────────────────────────

    [Fact]
    public void RapidSaveLoadCycles_DoNotCorruptData()
    {
        var manager = new ConfigManager();

        for (int i = 0; i < 20; i++)
        {
            var config = new CliConfig
            {
                DefaultRevision = $"rev-{i}",
                CacheDirectory = $@"C:\cache-{i}",
                NoProgress = i % 2 == 0,
            };
            manager.Save(config);

            var loaded = manager.Load();
            Assert.Equal($"rev-{i}", loaded.DefaultRevision);
            Assert.Equal($@"C:\cache-{i}", loaded.CacheDirectory);
            Assert.Equal(i % 2 == 0, loaded.NoProgress);
        }
    }

    // ── Config with all null/empty optional values roundtrips ───────

    [Fact]
    public void Config_AllNullOptionalValues_RoundtripsCorrectly()
    {
        var manager = new ConfigManager();
        var config = new CliConfig
        {
            CacheDirectory = null,
            DefaultToken = null,
            DefaultRevision = "main",
            NoProgress = false,
        };

        manager.Save(config);
        var loaded = manager.Load();

        Assert.Null(loaded.CacheDirectory);
        Assert.Null(loaded.DefaultToken);
        Assert.Equal("main", loaded.DefaultRevision);
        Assert.False(loaded.NoProgress);
    }

    // ── SetValue with whitespace-only strings ───────────────────────

    [Fact]
    public void SetValue_CacheDir_WhitespaceOnly_SetsNull()
    {
        var manager = new ConfigManager();
        var config = new CliConfig { CacheDirectory = @"C:\old" };

        var result = manager.SetValue(config, "cache-dir", "   ");

        Assert.True(result);
        Assert.Null(config.CacheDirectory);
    }

    [Fact]
    public void SetValue_DefaultToken_WhitespaceOnly_SetsNull()
    {
        var manager = new ConfigManager();
        var config = new CliConfig { DefaultToken = "old-token" };

        var result = manager.SetValue(config, "default-token", "  \t  ");

        Assert.True(result);
        Assert.Null(config.DefaultToken);
    }
}
