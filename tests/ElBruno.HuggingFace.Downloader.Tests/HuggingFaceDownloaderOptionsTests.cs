using ElBruno.HuggingFace;
using Xunit;

namespace ElBruno.HuggingFace.Downloader.Tests;

/// <summary>
/// Validation-focused tests for <see cref="HuggingFaceDownloaderOptions"/>.
/// Complements the happy-path defaults tests in HuggingFaceDownloaderTests.cs.
/// </summary>
public class HuggingFaceDownloaderOptionsValidationTests
{
    [Fact]
    public void Timeout_SetToZero_ThrowsArgumentOutOfRangeException()
    {
        var options = new HuggingFaceDownloaderOptions();

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            options.Timeout = TimeSpan.Zero);
        Assert.Equal("Timeout", ex.ParamName);
    }

    [Fact]
    public void Timeout_SetToNegative_ThrowsArgumentOutOfRangeException()
    {
        var options = new HuggingFaceDownloaderOptions();

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            options.Timeout = TimeSpan.FromMinutes(-5));
        Assert.Equal("Timeout", ex.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-1000)]
    public void Timeout_SetToNegativeTicks_ThrowsArgumentOutOfRangeException(long ticks)
    {
        var options = new HuggingFaceDownloaderOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            options.Timeout = TimeSpan.FromTicks(ticks));
    }

    [Fact]
    public void Timeout_SetToSmallPositive_Succeeds()
    {
        var options = new HuggingFaceDownloaderOptions
        {
            Timeout = TimeSpan.FromMilliseconds(1)
        };

        Assert.Equal(TimeSpan.FromMilliseconds(1), options.Timeout);
    }
}
