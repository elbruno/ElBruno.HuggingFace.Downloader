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
    [InlineData("refs/heads/main")]
    [InlineData("refs/pr/42")]
    public void GetFileUrl_RevisionWithSlash_ReturnsExpectedUrl(string revision)
    {
        var url = HuggingFaceUrlBuilder.GetFileUrl("org/repo", "file.txt", revision);
        Assert.Equal($"https://huggingface.co/org/repo/resolve/{revision}/file.txt", url);
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

    [Theory]
    [InlineData("/main")]
    [InlineData("main/")]
    public void GetFileUrl_RevisionStartsOrEndsWithSlash_ThrowsArgumentException(string revision)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            HuggingFaceUrlBuilder.GetFileUrl("org/repo", "file.txt", revision));
        Assert.Equal("revision", ex.ParamName);
    }

    // --- GetTreeUrl tests ---

    [Fact]
    public void GetTreeUrl_DefaultsToModelsRecursive()
    {
        var url = HuggingFaceUrlBuilder.GetTreeUrl("org/repo");
        Assert.Equal("https://huggingface.co/api/models/org/repo/tree/main?recursive=true", url);
    }

    [Theory]
    [InlineData(RepoType.Model, "models")]
    [InlineData(RepoType.Dataset, "datasets")]
    [InlineData(RepoType.Space, "spaces")]
    public void GetTreeUrl_UsesCorrectRepoTypeSegment(RepoType repoType, string expectedSegment)
    {
        var url = HuggingFaceUrlBuilder.GetTreeUrl("org/repo", repoType);
        Assert.Contains($"/api/{expectedSegment}/org/repo/tree/main", url);
    }

    [Fact]
    public void GetTreeUrl_WithSubPath_AppendsPath()
    {
        var url = HuggingFaceUrlBuilder.GetTreeUrl("org/repo", path: "onnx");
        Assert.Contains("/tree/main/onnx", url);
    }

    [Fact]
    public void GetTreeUrl_WithCursor_AppendsCursorQuery()
    {
        var url = HuggingFaceUrlBuilder.GetTreeUrl("org/repo", cursor: "abc123");
        Assert.Contains("cursor=abc123", url);
    }

    [Fact]
    public void GetTreeUrl_NonRecursive_OmitsRecursiveQuery()
    {
        var url = HuggingFaceUrlBuilder.GetTreeUrl("org/repo", recursive: false);
        Assert.DoesNotContain("recursive=", url);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GetTreeUrl_NullOrEmptyRepoId_ThrowsArgumentException(string? repoId)
    {
        var ex = Assert.Throws<ArgumentException>(() => HuggingFaceUrlBuilder.GetTreeUrl(repoId!));
        Assert.Equal("repoId", ex.ParamName);
    }
}
