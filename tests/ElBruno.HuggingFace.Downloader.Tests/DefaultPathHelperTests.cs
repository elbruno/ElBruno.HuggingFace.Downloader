using ElBruno.HuggingFace;
using Xunit;

namespace ElBruno.HuggingFace.Downloader.Tests;

public class DefaultPathHelperTests
{
    [Fact]
    public void GetDefaultCacheDirectory_ReturnsNonEmptyPath()
    {
        var path = DefaultPathHelper.GetDefaultCacheDirectory("TestApp");
        Assert.False(string.IsNullOrWhiteSpace(path));
        Assert.Contains("TestApp", path);
        Assert.EndsWith("models", path);
    }

    [Theory]
    [InlineData("org/model", "org_model")]
    [InlineData("user\\model", "user_model")]
    [InlineData("name:with*special?chars", "name_with_special_chars")]
    [InlineData("clean-name", "clean-name")]
    public void SanitizeModelName_ReplacesInvalidChars(string input, string expected)
    {
        Assert.Equal(expected, DefaultPathHelper.SanitizeModelName(input));
    }

    // --- Edge case tests ---

    [Fact]
    public void SanitizeModelName_NullInput_ThrowsNullReferenceException()
    {
        // Source does not guard against null; chained Replace calls throw NRE.
        Assert.Throws<NullReferenceException>(() =>
            DefaultPathHelper.SanitizeModelName(null!));
    }

    [Fact]
    public void SanitizeModelName_EmptyInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, DefaultPathHelper.SanitizeModelName(string.Empty));
    }

    [Fact]
    public void GetDefaultCacheDirectory_NullAppName_ThrowsArgumentNullException()
    {
        // Source does not guard null explicitly; Path.Combine rejects null.
        Assert.Throws<ArgumentNullException>(() =>
            DefaultPathHelper.GetDefaultCacheDirectory(null!));
    }

    [Fact]
    public void GetDefaultCacheDirectory_EmptyAppName_ReturnsPathEndingWithModels()
    {
        // Source does not validate empty appName; Path.Combine produces a valid
        // but potentially unexpected path. Document actual behavior.
        var path = DefaultPathHelper.GetDefaultCacheDirectory(string.Empty);
        Assert.False(string.IsNullOrWhiteSpace(path));
        Assert.EndsWith("models", path);
    }
}
