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
}
