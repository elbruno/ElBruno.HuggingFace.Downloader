using ElBruno.HuggingFace;
using Xunit;

namespace ElBruno.HuggingFace.Downloader.Tests;

public class ByteFormatHelperTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1.0 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1048576, "1.0 MB")]
    [InlineData(1572864, "1.5 MB")]
    [InlineData(1073741824, "1.00 GB")]
    public void FormatBytes_ReturnsExpectedString(long input, string expected)
    {
        Assert.Equal(expected, ByteFormatHelper.FormatBytes(input));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-1024)]
    [InlineData(-1048576)]
    public void FormatBytes_NegativeValue_DoesNotThrow(long input)
    {
        // Negative values should not crash; the result format is implementation-defined
        var result = ByteFormatHelper.FormatBytes(input);
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void FormatBytes_Zero_ReturnsZeroBytes()
    {
        Assert.Equal("0 B", ByteFormatHelper.FormatBytes(0));
    }

    [Fact]
    public void FormatBytes_LargeValue_FormatsAsGB()
    {
        // 5 GB = 5,368,709,120 bytes
        var result = ByteFormatHelper.FormatBytes(5_368_709_120);
        Assert.Contains("GB", result);
        Assert.Contains("5.00", result);
    }

    [Fact]
    public void FormatBytes_ExactlyOneKB_FormatsAsKB()
    {
        Assert.Equal("1.0 KB", ByteFormatHelper.FormatBytes(1024));
    }

    [Fact]
    public void FormatBytes_ExactlyOneMB_FormatsAsMB()
    {
        Assert.Equal("1.0 MB", ByteFormatHelper.FormatBytes(1_048_576));
    }
}
