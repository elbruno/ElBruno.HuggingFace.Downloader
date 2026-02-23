using ElBruno.HuggingFace;
using Xunit;

namespace ElBruno.HuggingFace.Downloader.Tests;

public class HuggingFaceUrlBuilderTests
{
    [Fact]
    public void GetFileUrl_DefaultRevision_ReturnsMainUrl()
    {
        var url = HuggingFaceUrlBuilder.GetFileUrl("org/repo", "onnx/model.onnx");
        Assert.Equal("https://huggingface.co/org/repo/resolve/main/onnx/model.onnx", url);
    }

    [Fact]
    public void GetFileUrl_CustomRevision_ReturnsRevisionUrl()
    {
        var url = HuggingFaceUrlBuilder.GetFileUrl("org/repo", "file.json", "v1.0");
        Assert.Equal("https://huggingface.co/org/repo/resolve/v1.0/file.json", url);
    }

    [Fact]
    public void GetFileUrl_NestedPath_PreservesSlashes()
    {
        var url = HuggingFaceUrlBuilder.GetFileUrl("user/model", "voices/en/metadata.json");
        Assert.Equal("https://huggingface.co/user/model/resolve/main/voices/en/metadata.json", url);
    }
}
