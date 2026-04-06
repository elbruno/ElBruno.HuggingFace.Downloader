using ElBruno.HuggingFace;
using Xunit;

namespace ElBruno.HuggingFace.Downloader.Tests;

public class HuggingFaceUrlBuilderTests
{
    // --- Happy-path tests ---

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

    // --- RepoId validation tests ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetFileUrl_NullOrEmptyRepoId_ThrowsArgumentException(string? repoId)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            HuggingFaceUrlBuilder.GetFileUrl(repoId!, "file.txt"));
        Assert.Equal("repoId", ex.ParamName);
    }

    [Theory]
    [InlineData("noslash")]
    [InlineData("/startsslash")]
    [InlineData("endslash/")]
    public void GetFileUrl_MalformedRepoId_ThrowsArgumentException(string repoId)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            HuggingFaceUrlBuilder.GetFileUrl(repoId, "file.txt"));
        Assert.Equal("repoId", ex.ParamName);
    }

    [Theory]
    [InlineData("owner/../evil")]
    [InlineData("../evil/repo")]
    [InlineData("owner/repo/..")]
    public void GetFileUrl_RepoIdWithPathTraversal_ThrowsArgumentException(string repoId)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            HuggingFaceUrlBuilder.GetFileUrl(repoId, "file.txt"));
        Assert.Equal("repoId", ex.ParamName);
    }

    [Fact]
    public void GetFileUrl_RepoIdWithBackslash_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            HuggingFaceUrlBuilder.GetFileUrl("owner\\repo", "file.txt"));
        Assert.Equal("repoId", ex.ParamName);
    }

    // --- FilePath validation tests ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetFileUrl_NullOrEmptyFilePath_ThrowsArgumentException(string? filePath)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            HuggingFaceUrlBuilder.GetFileUrl("org/repo", filePath!));
        Assert.Equal("filePath", ex.ParamName);
    }

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("folder/../secret.txt")]
    public void GetFileUrl_FilePathWithPathTraversal_ThrowsArgumentException(string filePath)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            HuggingFaceUrlBuilder.GetFileUrl("org/repo", filePath));
        Assert.Equal("filePath", ex.ParamName);
    }

    [Fact]
    public void GetFileUrl_FilePathWithBackslash_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            HuggingFaceUrlBuilder.GetFileUrl("org/repo", "folder\\file.txt"));
        Assert.Equal("filePath", ex.ParamName);
    }

    [Fact]
    public void GetFileUrl_FilePathStartsWithSlash_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            HuggingFaceUrlBuilder.GetFileUrl("org/repo", "/etc/passwd"));
        Assert.Equal("filePath", ex.ParamName);
    }

    // --- Revision validation tests ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetFileUrl_NullOrEmptyRevision_ThrowsArgumentException(string? revision)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            HuggingFaceUrlBuilder.GetFileUrl("org/repo", "file.txt", revision!));
        Assert.Equal("revision", ex.ParamName);
    }

    [Theory]
    [InlineData("branch/name")]
    [InlineData("refs/heads/main")]
    public void GetFileUrl_RevisionWithSlash_ThrowsArgumentException(string revision)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            HuggingFaceUrlBuilder.GetFileUrl("org/repo", "file.txt", revision));
        Assert.Equal("revision", ex.ParamName);
    }

    [Theory]
    [InlineData("../main")]
    [InlineData("v1..0")]
    public void GetFileUrl_RevisionWithPathTraversal_ThrowsArgumentException(string revision)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            HuggingFaceUrlBuilder.GetFileUrl("org/repo", "file.txt", revision));
        Assert.Equal("revision", ex.ParamName);
    }

    [Fact]
    public void GetFileUrl_RevisionWithBackslash_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            HuggingFaceUrlBuilder.GetFileUrl("org/repo", "file.txt", "branch\\name"));
        Assert.Equal("revision", ex.ParamName);
    }
}
