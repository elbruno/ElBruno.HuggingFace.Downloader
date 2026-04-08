using Xunit;
using ElBruno.HuggingFace.Cli.Models;

namespace ElBruno.HuggingFace.Cli.Tests;

/// <summary>
/// Tests for <see cref="CliConfig"/> — known keys and default values.
/// </summary>
public sealed class CliConfigTests
{
    [Fact]
    public void KnownKeys_ContainsExpectedKeys()
    {
        var keys = CliConfig.KnownKeys;

        Assert.Contains("cache-dir", keys.Keys);
        Assert.Contains("default-token", keys.Keys);
        Assert.Contains("default-revision", keys.Keys);
        Assert.Contains("no-progress", keys.Keys);
    }

    [Fact]
    public void KnownKeys_HasExactlyFourEntries()
    {
        Assert.Equal(4, CliConfig.KnownKeys.Count);
    }

    [Fact]
    public void KnownKeys_AllHaveNonEmptyDescriptions()
    {
        foreach (var (key, description) in CliConfig.KnownKeys)
        {
            Assert.False(string.IsNullOrWhiteSpace(description), $"Key '{key}' has empty description");
        }
    }

    [Fact]
    public void DefaultValues_CacheDirectoryIsNull()
    {
        var config = new CliConfig();

        Assert.Null(config.CacheDirectory);
    }

    [Fact]
    public void DefaultValues_DefaultTokenIsNull()
    {
        var config = new CliConfig();

        Assert.Null(config.DefaultToken);
    }

    [Fact]
    public void DefaultValues_DefaultRevisionIsMain()
    {
        var config = new CliConfig();

        Assert.Equal("main", config.DefaultRevision);
    }

    [Fact]
    public void DefaultValues_NoProgressIsFalse()
    {
        var config = new CliConfig();

        Assert.False(config.NoProgress);
    }
}
