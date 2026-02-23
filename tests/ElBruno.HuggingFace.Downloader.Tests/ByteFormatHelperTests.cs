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
}
