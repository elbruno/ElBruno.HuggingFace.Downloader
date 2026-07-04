using System.Net;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace ElBruno.HuggingFace.Downloader.Tests;

public sealed class ModelBundleTests : IDisposable
{
    private readonly string _tempDir;

    public ModelBundleTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"hf_bundle_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void ModelBundleManifestJson_RoundTrips()
    {
        var manifest = new ModelBundleManifest
        {
            RepoId = "demo/repo",
            Revision = "refs/pr/42",
            Files =
            [
                new ModelBundleFile
                {
                    Path = "onnx/model.onnx",
                    Size = 128,
                    Sha256 = new string('a', 64),
                    Required = true
                },
                new ModelBundleFile
                {
                    Path = "tokenizer.json",
                    Required = false,
                    Revision = "refs/pr/42"
                }
            ]
        };

        var json = ModelBundleManifestJson.Serialize(manifest);
        var roundTrip = ModelBundleManifestJson.Deserialize(json);

        Assert.Equal(manifest.RepoId, roundTrip.RepoId);
        Assert.Equal(manifest.Revision, roundTrip.Revision);
        Assert.Equal(2, roundTrip.Files.Count);
        Assert.Equal("onnx/model.onnx", roundTrip.Files[0].Path);
        Assert.Equal(128, roundTrip.Files[0].Size);
        Assert.Equal(new string('a', 64), roundTrip.Files[0].Sha256);
        Assert.True(roundTrip.Files[0].Required);
        Assert.Equal("tokenizer.json", roundTrip.Files[1].Path);
        Assert.False(roundTrip.Files[1].Required);
        Assert.Equal("refs/pr/42", roundTrip.Files[1].Revision);
    }

    [Fact]
    public async Task EnsureBundleAsync_MixedRevisions_Throws()
    {
        using var downloader = new HuggingFaceDownloader();

        var manifest = new ModelBundleManifest
        {
            RepoId = "demo/repo",
            Revision = "main",
            Files =
            [
                new ModelBundleFile { Path = "model.bin" },
                new ModelBundleFile { Path = "tokenizer.json", Revision = "dev" }
            ]
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            downloader.EnsureBundleAsync(manifest, _tempDir));

        Assert.Contains("mixed revisions", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnsureBundleAsync_CorruptExistingFile_RedownloadsAndWritesResolvedManifest()
    {
        const string filePath = "onnx/model.onnx";
        const string content = "fresh model bytes";
        var contentBytes = Encoding.UTF8.GetBytes(content);
        var expectedSha = ComputeSha256(contentBytes);
        var localPath = Path.Combine(_tempDir, "onnx", "model.onnx");
        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
        await File.WriteAllTextAsync(localPath, "corrupt");

        var handler = new MockHttpMessageHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Head)
                return Task.FromResult(CreateHeadResponse(contentBytes.Length, "\"etag\""));

            return Task.FromResult(CreateFileResponse(contentBytes, "\"etag\""));
        });

        using var httpClient = new HttpClient(handler);
        using var downloader = new HuggingFaceDownloader(httpClient, new HuggingFaceDownloaderOptions());

        var manifest = new ModelBundleManifest
        {
            RepoId = "demo/repo",
            Revision = "main",
            Files =
            [
                new ModelBundleFile
                {
                    Path = filePath,
                    Size = contentBytes.Length,
                    Sha256 = expectedSha
                }
            ]
        };

        var result = await downloader.EnsureBundleAsync(manifest, _tempDir);

        Assert.Equal(content, await File.ReadAllTextAsync(localPath));
        Assert.Equal(1, result.DownloadedFileCount);
        Assert.True(File.Exists(result.ResolvedManifestPath));
        Assert.Single(result.ResolvedManifest.Files);
        Assert.True(result.ResolvedManifest.Files[0].Exists);
        Assert.Equal(expectedSha, result.ResolvedManifest.Files[0].Sha256);
    }

    [Fact]
    public async Task EnsureBundleAsync_MissingOptionalFile_IsAllowed()
    {
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Head)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        using var httpClient = new HttpClient(handler);
        using var downloader = new HuggingFaceDownloader(httpClient, new HuggingFaceDownloaderOptions());

        var manifest = new ModelBundleManifest
        {
            RepoId = "demo/repo",
            Revision = "main",
            Files =
            [
                new ModelBundleFile
                {
                    Path = "optional/config.json",
                    Required = false
                }
            ]
        };

        var result = await downloader.EnsureBundleAsync(manifest, _tempDir);

        Assert.Equal(1, result.MissingOptionalFileCount);
        Assert.Single(result.ResolvedManifest.Files);
        Assert.False(result.ResolvedManifest.Files[0].Exists);
    }

    [Fact]
    public async Task EnsureBundleAsync_MissingRequiredFile_Fails()
    {
        var handler = new MockHttpMessageHandler((request, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        using var httpClient = new HttpClient(handler);
        using var downloader = new HuggingFaceDownloader(httpClient, new HuggingFaceDownloaderOptions());

        var manifest = new ModelBundleManifest
        {
            RepoId = "demo/repo",
            Revision = "main",
            Files =
            [
                new ModelBundleFile
                {
                    Path = "required/model.onnx",
                    Required = true
                }
            ]
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            downloader.EnsureBundleAsync(manifest, _tempDir));

        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeSha256(byte[] content)
        => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static HttpResponseMessage CreateHeadResponse(int contentLength, string entityTag)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([])
        };
        response.Content.Headers.ContentLength = contentLength;
        response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue(entityTag);
        response.Headers.AcceptRanges.Add("bytes");
        return response;
    }

    private static HttpResponseMessage CreateFileResponse(byte[] content, string entityTag)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(content)
        };
        response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue(entityTag);
        return response;
    }

    private sealed class MockHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }
}
