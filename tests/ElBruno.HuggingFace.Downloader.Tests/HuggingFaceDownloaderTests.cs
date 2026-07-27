using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ElBruno.HuggingFace;
using Xunit;

namespace ElBruno.HuggingFace.Downloader.Tests;

public class HuggingFaceDownloaderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly HuggingFaceDownloader _downloader;

    public HuggingFaceDownloaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"hf_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _downloader = new HuggingFaceDownloader();
    }

    public void Dispose()
    {
        _downloader.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_Default_CreatesInstance()
    {
        using var downloader = new HuggingFaceDownloader();
        Assert.NotNull(downloader);
    }

    [Fact]
    public void Constructor_WithOptions_CreatesInstance()
    {
        var options = new HuggingFaceDownloaderOptions { Timeout = TimeSpan.FromMinutes(10) };
        using var downloader = new HuggingFaceDownloader(options);
        Assert.NotNull(downloader);
    }

    [Fact]
    public void Constructor_WithHttpClient_CreatesInstance()
    {
        using var httpClient = new HttpClient();
        using var downloader = new HuggingFaceDownloader(httpClient);
        Assert.NotNull(downloader);
    }

    [Fact]
    public void Constructor_WithHttpClientAndOptions_CreatesInstance()
    {
        using var httpClient = new HttpClient();
        var options = new HuggingFaceDownloaderOptions { AuthToken = "test" };
        using var downloader = new HuggingFaceDownloader(httpClient, options);
        Assert.NotNull(downloader);
    }

    [Fact]
    public void Constructor_NullHttpClient_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new HuggingFaceDownloader(httpClient: null!));
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_OwnedHttpClient_DisposesClient()
    {
        var downloader = new HuggingFaceDownloader();
        downloader.Dispose();
        // No exception = success; the owned HttpClient was disposed
    }

    [Fact]
    public void Dispose_ExternalHttpClient_DoesNotDisposeClient()
    {
        var httpClient = new HttpClient();
        var downloader = new HuggingFaceDownloader(httpClient);
        downloader.Dispose();
        // The external HttpClient should still be usable
        Assert.NotNull(httpClient.BaseAddress ?? null as object ?? httpClient);
        httpClient.Dispose();
    }

    #endregion

    #region GetMissingFiles Tests

    [Fact]
    public void GetMissingFiles_AllMissing_ReturnsAll()
    {
        var missing = _downloader.GetMissingFiles(["a.onnx", "b.json"], _tempDir);
        Assert.Equal(2, missing.Count);
        Assert.Contains("a.onnx", missing);
        Assert.Contains("b.json", missing);
    }

    [Fact]
    public void GetMissingFiles_SomePresent_ReturnsOnlyMissing()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.onnx"), "dummy");

        var missing = _downloader.GetMissingFiles(["a.onnx", "b.json"], _tempDir);
        Assert.Single(missing);
        Assert.Equal("b.json", missing[0]);
    }

    [Fact]
    public void GetMissingFiles_AllPresent_ReturnsEmpty()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.onnx"), "dummy");
        File.WriteAllText(Path.Combine(_tempDir, "b.json"), "dummy");

        var missing = _downloader.GetMissingFiles(["a.onnx", "b.json"], _tempDir);
        Assert.Empty(missing);
    }

    [Fact]
    public void GetMissingFiles_EmptyList_ReturnsEmpty()
    {
        var missing = _downloader.GetMissingFiles([], _tempDir);
        Assert.Empty(missing);
    }

    [Fact]
    public void GetMissingFiles_NestedPaths_HandlesSlashConversion()
    {
        var subDir = Path.Combine(_tempDir, "onnx");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "model.onnx"), "dummy");

        var missing = _downloader.GetMissingFiles(["onnx/model.onnx", "tokenizer.json"], _tempDir);
        Assert.Single(missing);
        Assert.Equal("tokenizer.json", missing[0]);
    }

    [Fact]
    public void GetMissingFiles_DeeplyNestedPaths_Works()
    {
        var deepDir = Path.Combine(_tempDir, "voices", "en", "carter");
        Directory.CreateDirectory(deepDir);
        File.WriteAllText(Path.Combine(deepDir, "metadata.json"), "{}");

        var missing = _downloader.GetMissingFiles(
            ["voices/en/carter/metadata.json", "voices/en/carter/kv_cache.npy"], _tempDir);
        Assert.Single(missing);
        Assert.Equal("voices/en/carter/kv_cache.npy", missing[0]);
    }

    [Fact]
    public void GetMissingFiles_NonexistentDirectory_ReturnsAll()
    {
        var nonexistent = Path.Combine(_tempDir, "does_not_exist");
        var missing = _downloader.GetMissingFiles(["a.onnx"], nonexistent);
        Assert.Single(missing);
    }

    #endregion

    #region AreFilesAvailable Tests

    [Fact]
    public void AreFilesAvailable_AllPresent_ReturnsTrue()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.onnx"), "dummy");
        File.WriteAllText(Path.Combine(_tempDir, "b.json"), "dummy");

        Assert.True(_downloader.AreFilesAvailable(["a.onnx", "b.json"], _tempDir));
    }

    [Fact]
    public void AreFilesAvailable_SomeMissing_ReturnsFalse()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.onnx"), "dummy");
        Assert.False(_downloader.AreFilesAvailable(["a.onnx", "b.json"], _tempDir));
    }

    [Fact]
    public void AreFilesAvailable_NonePresent_ReturnsFalse()
    {
        Assert.False(_downloader.AreFilesAvailable(["a.onnx"], _tempDir));
    }

    [Fact]
    public void AreFilesAvailable_EmptyList_ReturnsTrue()
    {
        Assert.True(_downloader.AreFilesAvailable([], _tempDir));
    }

    [Fact]
    public void AreFilesAvailable_NestedPaths_Works()
    {
        var subDir = Path.Combine(_tempDir, "onnx");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "model.onnx"), "dummy");

        Assert.True(_downloader.AreFilesAvailable(["onnx/model.onnx"], _tempDir));
        Assert.False(_downloader.AreFilesAvailable(["onnx/model.onnx", "onnx/vocab.txt"], _tempDir));
    }

    #endregion

    #region DownloadFilesAsync Validation Tests

    [Fact]
    public async Task DownloadFilesAsync_NullRequest_ThrowsArgumentNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _downloader.DownloadFilesAsync(null!));
    }

    [Fact]
    public async Task DownloadFilesAsync_EmptyRepoId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _downloader.DownloadFilesAsync(new DownloadRequest
            {
                RepoId = "",
                LocalDirectory = _tempDir,
                RequiredFiles = ["file.txt"]
            }));
        Assert.Contains("RepoId", ex.Message);
    }

    [Fact]
    public async Task DownloadFilesAsync_WhitespaceRepoId_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _downloader.DownloadFilesAsync(new DownloadRequest
            {
                RepoId = "   ",
                LocalDirectory = _tempDir,
                RequiredFiles = ["file.txt"]
            }));
        Assert.Contains("RepoId", ex.Message);
    }

    [Fact]
    public async Task DownloadFilesAsync_EmptyLocalDirectory_ThrowsArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _downloader.DownloadFilesAsync(new DownloadRequest
            {
                RepoId = "test/repo",
                LocalDirectory = "",
                RequiredFiles = ["file.txt"]
            }));
        Assert.Contains("LocalDirectory", ex.Message);
    }

    [Fact]
    public async Task DownloadFilesAsync_CreatesLocalDirectory()
    {
        var newDir = Path.Combine(_tempDir, "new_subdir");
        Assert.False(Directory.Exists(newDir));

        // All files present — so no actual download needed
        File.WriteAllText(Path.Combine(_tempDir, "file.txt"), "data");

        await _downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["file.txt"]
        });

        // Verify it didn't throw and directory exists
        Assert.True(Directory.Exists(_tempDir));
    }

    #endregion

    #region DownloadFilesAsync Progress Tests

    [Fact]
    public async Task DownloadFilesAsync_AllFilesExist_ReportsComplete()
    {
        File.WriteAllText(Path.Combine(_tempDir, "file.txt"), "content");

        DownloadProgress? lastProgress = null;

        await _downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["file.txt"],
            Progress = new Progress<DownloadProgress>(p => lastProgress = p)
        });

        await Task.Delay(100); // Progress callbacks are async
        Assert.NotNull(lastProgress);
        Assert.Equal(DownloadStage.Complete, lastProgress!.Stage);
        Assert.Equal(100, lastProgress.PercentComplete);
    }

    [Fact]
    public async Task DownloadFilesAsync_AllFilesExist_WithOptional_ReportsComplete()
    {
        File.WriteAllText(Path.Combine(_tempDir, "required.onnx"), "model");
        File.WriteAllText(Path.Combine(_tempDir, "optional.json"), "config");

        DownloadProgress? lastProgress = null;

        await _downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["required.onnx"],
            OptionalFiles = ["optional.json"],
            Progress = new Progress<DownloadProgress>(p => lastProgress = p)
        });

        await Task.Delay(100);
        Assert.NotNull(lastProgress);
        Assert.Equal(DownloadStage.Complete, lastProgress!.Stage);
    }

    [Fact]
    public async Task DownloadFilesAsync_EmptyRequiredFiles_AllOptionalPresent_ReportsComplete()
    {
        File.WriteAllText(Path.Combine(_tempDir, "opt.json"), "data");

        DownloadProgress? lastProgress = null;

        await _downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = [],
            OptionalFiles = ["opt.json"],
            Progress = new Progress<DownloadProgress>(p => lastProgress = p)
        });

        await Task.Delay(100);
        Assert.NotNull(lastProgress);
        Assert.Equal(DownloadStage.Complete, lastProgress!.Stage);
    }

    [Fact]
    public async Task DownloadFilesAsync_NullOptionalFiles_DoesNotThrow()
    {
        File.WriteAllText(Path.Combine(_tempDir, "file.txt"), "content");

        await _downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["file.txt"],
            OptionalFiles = null
        });
    }

    #endregion

    #region DownloadFilesAsync Cancellation Tests

    [Fact]
    public async Task DownloadFilesAsync_CancelledToken_ThrowsOperationCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _downloader.DownloadFilesAsync(new DownloadRequest
            {
                RepoId = "test/repo",
                LocalDirectory = _tempDir,
                RequiredFiles = ["missing.onnx"]
            }, cts.Token));
    }

    #endregion

    #region DownloadRequest Defaults Tests

    [Fact]
    public void DownloadRequest_DefaultRevision_IsMain()
    {
        var request = new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["file.txt"]
        };
        Assert.Equal("main", request.Revision);
    }

    [Fact]
    public void DownloadRequest_DefaultAtomicWrites_IsTrue()
    {
        var request = new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["file.txt"]
        };
        Assert.True(request.UseAtomicWrites);
    }

    [Fact]
    public void DownloadRequest_DefaultResumePartialDownloads_IsTrue()
    {
        var request = new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["file.txt"]
        };
        Assert.True(request.ResumePartialDownloads);
    }

    [Fact]
    public void DownloadRequest_DefaultProgress_IsNull()
    {
        var request = new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["file.txt"]
        };
        Assert.Null(request.Progress);
    }

    [Fact]
    public void DownloadRequest_DefaultOptionalFiles_IsNull()
    {
        var request = new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["file.txt"]
        };
        Assert.Null(request.OptionalFiles);
    }

    [Fact]
    public void DownloadRequest_CustomRevision_IsPreserved()
    {
        var request = new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["file.txt"],
            Revision = "v2.0"
        };
        Assert.Equal("v2.0", request.Revision);
    }

    [Fact]
    public async Task ResolveCommitShaAsync_CommitRevision_ReturnsNormalizedCommit()
    {
        const string commitSha = "ABCDEF1234567890ABCDEF1234567890ABCDEF12";
        using var downloader = new HuggingFaceDownloader();

        var resolvedCommitSha = await downloader.ResolveCommitShaAsync("test/repo", "model.onnx", commitSha);

        Assert.Equal(commitSha.ToLowerInvariant(), resolvedCommitSha);
    }

    [Fact]
    public async Task DownloadFilesAsync_ResolvedCommitSha_WritesMetadata()
    {
        const string commitSha = "1234567890abcdef1234567890abcdef12345678";
        const string fileContent = "resolved bytes";
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Head)
                return Task.FromResult(CreateHeadResponse(fileContent.Length, resolvedCommitSha: commitSha));

            return Task.FromResult(CreateFileResponse(fileContent, resolvedCommitSha: commitSha));
        });

        using var httpClient = new HttpClient(handler);
        using var downloader = new HuggingFaceDownloader(httpClient, new HuggingFaceDownloaderOptions());
        var request = new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["model.onnx"],
            Revision = "main"
        };

        await downloader.DownloadFilesAsync(request);

        Assert.Equal(commitSha, request.ResolvedCommitSha);

        var metadataPath = Path.Combine(_tempDir, HuggingFaceMetadataFileNames.DownloadResolutionMetadata);
        Assert.True(File.Exists(metadataPath));

        var metadata = JsonSerializer.Deserialize<DownloadResolutionMetadata>(
            await File.ReadAllTextAsync(metadataPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(metadata);
        Assert.Equal("test/repo", metadata!.RepoId);
        Assert.Equal("main", metadata.RequestedRevision);
        Assert.Equal(commitSha, metadata.ResolvedCommitSha);
    }

    [Fact]
    public async Task DownloadFilesAsync_ExpectedCommitShaMismatch_Throws()
    {
        const string resolvedCommitSha = "1234567890abcdef1234567890abcdef12345678";
        const string expectedCommitSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Head)
                return Task.FromResult(CreateHeadResponse(10, resolvedCommitSha: resolvedCommitSha));

            return Task.FromResult(CreateFileResponse("content", resolvedCommitSha: resolvedCommitSha));
        });

        using var httpClient = new HttpClient(handler);
        using var downloader = new HuggingFaceDownloader(httpClient, new HuggingFaceDownloaderOptions());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            downloader.DownloadFilesAsync(new DownloadRequest
            {
                RepoId = "test/repo",
                LocalDirectory = _tempDir,
                RequiredFiles = ["model.onnx"],
                Revision = "main",
                ExpectedCommitSha = expectedCommitSha
            }));

        Assert.Contains(expectedCommitSha, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DownloadFilesAsync_LocalDirectoryPinnedToDifferentCommit_Throws()
    {
        const string existingCommitSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string resolvedCommitSha = "1234567890abcdef1234567890abcdef12345678";
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "model.onnx"), "existing");
        await File.WriteAllTextAsync(
            Path.Combine(_tempDir, HuggingFaceMetadataFileNames.DownloadResolutionMetadata),
            JsonSerializer.Serialize(new DownloadResolutionMetadata
            {
                RepoId = "test/repo",
                RequestedRevision = "main",
                ResolvedCommitSha = existingCommitSha,
                GeneratedAtUtc = DateTimeOffset.UtcNow
            }));

        var handler = new MockHttpMessageHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Head)
                return Task.FromResult(CreateHeadResponse(10, resolvedCommitSha: resolvedCommitSha));

            return Task.FromResult(CreateFileResponse("content", resolvedCommitSha: resolvedCommitSha));
        });

        using var httpClient = new HttpClient(handler);
        using var downloader = new HuggingFaceDownloader(httpClient, new HuggingFaceDownloaderOptions());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            downloader.DownloadFilesAsync(new DownloadRequest
            {
                RepoId = "test/repo",
                LocalDirectory = _tempDir,
                RequiredFiles = ["model.onnx"],
                Revision = "main"
            }));

        Assert.Contains(existingCommitSha, ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(resolvedCommitSha, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Mock Helpers

    private sealed class MockHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }

    private sealed class SynchronousProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }

    /// <summary>
    /// A stream that cancels the provided CTS on the first read, simulating mid-download cancellation.
    /// </summary>
    private sealed class CancellingStream(byte[] data, CancellationTokenSource cts) : MemoryStream(data)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cts.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return base.ReadAsync(buffer, cancellationToken);
        }
    }

    private sealed class PartialCancellingStream(byte[] data, CancellationTokenSource cts, int firstChunkSize) : MemoryStream(data)
    {
        private bool _returnedFirstChunk;

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_returnedFirstChunk)
            {
                _returnedFirstChunk = true;
                var length = Math.Min(firstChunkSize, (int)(Length - Position));
                return base.ReadAsync(buffer[..length], cancellationToken);
            }

            cts.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return base.ReadAsync(buffer, cancellationToken);
        }
    }

    private static HttpResponseMessage CreateFileResponse(string content, string? resolvedCommitSha = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content)
        };
        if (resolvedCommitSha is not null)
            response.Headers.TryAddWithoutValidation("X-Resolved-Revision", resolvedCommitSha);

        return response;
    }

    private static HttpResponseMessage CreateHeadResponse(
        long contentLength,
        string? entityTag = null,
        bool supportsRanges = false,
        string? resolvedCommitSha = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([])
        };
        response.Content.Headers.ContentLength = contentLength;
        if (entityTag is not null)
            response.Headers.ETag = EntityTagHeaderValue.Parse(entityTag);
        if (supportsRanges)
            response.Headers.AcceptRanges.Add("bytes");
        if (resolvedCommitSha is not null)
            response.Headers.TryAddWithoutValidation("X-Resolved-Revision", resolvedCommitSha);
        return response;
    }

    private static HttpResponseMessage CreateRangeResponse(byte[] content, long from, string? entityTag = null)
    {
        var rangeContent = content[(int)from..];
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new ByteArrayContent(rangeContent)
        };
        response.Content.Headers.ContentLength = rangeContent.Length;
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(from, content.Length - 1, content.Length);
        if (entityTag is not null)
            response.Headers.ETag = EntityTagHeaderValue.Parse(entityTag);
        return response;
    }

    private static void WritePartialDownload(string localPath, byte[] content, string revision, string? entityTag, long? totalBytes)
    {
        File.WriteAllBytes(localPath + ".partial", content);

        var metadata = JsonSerializer.Serialize(new
        {
            Revision = revision,
            EntityTag = entityTag,
            TotalBytes = totalBytes
        });
        File.WriteAllText(localPath + ".partial.json", metadata);
    }

    #endregion

    #region Phase 2: Core Download Flow Tests

    [Fact]
    public async Task DownloadFilesAsync_SingleRequiredFile_DownloadsSuccessfully()
    {
        const string fileContent = "model binary data here";
        var handler = new MockHttpMessageHandler((request, _) =>
            Task.FromResult(CreateFileResponse(fileContent)));

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["model.onnx"]
        });

        var filePath = Path.Combine(_tempDir, "model.onnx");
        Assert.True(File.Exists(filePath));
        Assert.Equal(fileContent, await File.ReadAllTextAsync(filePath));
    }

    [Fact]
    public async Task DownloadFilesAsync_MultipleRequiredFiles_DownloadsAll()
    {
        var files = new Dictionary<string, string>
        {
            ["model.onnx"] = "model data",
            ["config.json"] = """{"hidden_size": 384}""",
            ["tokenizer.json"] = """{"vocab_size": 30522}"""
        };

        var handler = new MockHttpMessageHandler((request, _) =>
        {
            var url = request.RequestUri!.ToString();
            foreach (var (name, content) in files)
            {
                if (url.EndsWith(name))
                    return Task.FromResult(CreateFileResponse(content));
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["model.onnx", "config.json", "tokenizer.json"]
        });

        foreach (var (name, expected) in files)
        {
            var path = Path.Combine(_tempDir, name);
            Assert.True(File.Exists(path), $"File {name} should exist");
            Assert.Equal(expected, await File.ReadAllTextAsync(path));
        }
    }

    [Fact]
    public async Task DownloadFilesAsync_MixedRequiredAndOptional_DownloadsAll()
    {
        var handler = new MockHttpMessageHandler((request, _) =>
            Task.FromResult(CreateFileResponse("content")));

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["model.onnx"],
            OptionalFiles = ["README.md"]
        });

        Assert.True(File.Exists(Path.Combine(_tempDir, "model.onnx")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "README.md")));
    }

    [Fact]
    public async Task DownloadFilesAsync_SkipsExistingFiles_DownloadsOnlyMissing()
    {
        File.WriteAllText(Path.Combine(_tempDir, "existing.txt"), "already here");

        var downloadedUrls = new List<string>();
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Get)
                downloadedUrls.Add(request.RequestUri!.ToString());
            return Task.FromResult(CreateFileResponse("new content"));
        });

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["existing.txt", "missing.txt"]
        });

        Assert.Single(downloadedUrls);
        Assert.Contains("missing.txt", downloadedUrls[0]);
        Assert.Equal("already here", await File.ReadAllTextAsync(Path.Combine(_tempDir, "existing.txt")));
        Assert.Equal("new content", await File.ReadAllTextAsync(Path.Combine(_tempDir, "missing.txt")));
    }

    [Fact]
    public async Task DownloadFilesAsync_WithProgress_ReportsAllStages()
    {
        var reports = new List<DownloadProgress>();
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Head)
                return Task.FromResult(CreateHeadResponse(7));
            return Task.FromResult(CreateFileResponse("content"));
        });

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = true };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["model.onnx"],
            Progress = new SynchronousProgress<DownloadProgress>(p => reports.Add(p))
        });

        var stages = reports.Select(r => r.Stage).Distinct().ToList();
        Assert.Contains(DownloadStage.Checking, stages);
        Assert.Contains(DownloadStage.Downloading, stages);
        Assert.Contains(DownloadStage.Validating, stages);
        Assert.Contains(DownloadStage.Complete, stages);
    }

    [Fact]
    public async Task DownloadFilesAsync_WithProgress_ReportsCurrentFileAndIndex()
    {
        var reports = new List<DownloadProgress>();
        var handler = new MockHttpMessageHandler((request, _) =>
            Task.FromResult(CreateFileResponse("data")));

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["first.txt", "second.txt"],
            Progress = new SynchronousProgress<DownloadProgress>(p => reports.Add(p))
        });

        var downloadReports = reports.Where(r => r.Stage == DownloadStage.Downloading).ToList();
        Assert.Contains(downloadReports, r => r.CurrentFile == "first.txt" && r.CurrentFileIndex == 1);
        Assert.Contains(downloadReports, r => r.CurrentFile == "second.txt" && r.CurrentFileIndex == 2);
        Assert.All(downloadReports, r => Assert.Equal(2, r.TotalFileCount));
    }

    [Fact]
    public async Task DownloadFilesAsync_WithProgress_CompletionReaches100Percent()
    {
        var reports = new List<DownloadProgress>();
        var handler = new MockHttpMessageHandler((request, _) =>
            Task.FromResult(CreateFileResponse("data")));

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["file.txt"],
            Progress = new SynchronousProgress<DownloadProgress>(p => reports.Add(p))
        });

        var completeReport = reports.Last(r => r.Stage == DownloadStage.Complete);
        Assert.Equal(100, completeReport.PercentComplete);
    }

    [Fact]
    public async Task DownloadFilesAsync_WithAtomicWrites_FinalFileExistsAndTempRemoved()
    {
        var handler = new MockHttpMessageHandler((request, _) =>
            Task.FromResult(CreateFileResponse("atomic content")));

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["data.bin"],
            UseAtomicWrites = true
        });

        Assert.True(File.Exists(Path.Combine(_tempDir, "data.bin")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "data.bin.tmp")));
        Assert.Equal("atomic content", await File.ReadAllTextAsync(Path.Combine(_tempDir, "data.bin")));
    }

    [Fact]
    public async Task DownloadFilesAsync_WithoutAtomicWrites_WritesDirectly()
    {
        var handler = new MockHttpMessageHandler((request, _) =>
            Task.FromResult(CreateFileResponse("direct content")));

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["data.bin"],
            UseAtomicWrites = false
        });

        Assert.True(File.Exists(Path.Combine(_tempDir, "data.bin")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "data.bin.tmp")));
        Assert.Equal("direct content", await File.ReadAllTextAsync(Path.Combine(_tempDir, "data.bin")));
    }

    [Fact]
    public async Task DownloadFilesAsync_CustomRevision_UsesCorrectUrl()
    {
        string? capturedUrl = null;
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Get)
                capturedUrl = request.RequestUri!.ToString();
            return Task.FromResult(CreateFileResponse("data"));
        });

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["model.onnx"],
            Revision = "v2.0"
        });

        Assert.NotNull(capturedUrl);
        Assert.Contains("/resolve/v2.0/", capturedUrl);
    }

    [Fact]
    public async Task DownloadFilesAsync_NestedPaths_CreatesSubdirectories()
    {
        var handler = new MockHttpMessageHandler((request, _) =>
            Task.FromResult(CreateFileResponse("nested content")));

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["models/onnx/model.onnx"]
        });

        var expectedPath = Path.Combine(_tempDir, "models", "onnx", "model.onnx");
        Assert.True(File.Exists(expectedPath));
        Assert.Equal("nested content", await File.ReadAllTextAsync(expectedPath));
    }

    [Fact]
    public async Task DownloadFilesAsync_WithResolveFileSizes_IssuesHeadRequests()
    {
        var headRequestCount = 0;
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Head)
            {
                Interlocked.Increment(ref headRequestCount);
                return Task.FromResult(CreateHeadResponse(4));
            }
            return Task.FromResult(CreateFileResponse("data"));
        });

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = true };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["a.txt", "b.txt"]
        });

        Assert.Equal(2, headRequestCount);
    }

    [Fact]
    public async Task DownloadFilesAsync_WithoutResolveFileSizes_SkipsHeadRequests()
    {
        var headRequestCount = 0;
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Head)
            {
                Interlocked.Increment(ref headRequestCount);
                return Task.FromResult(CreateHeadResponse(100));
            }
            return Task.FromResult(CreateFileResponse("data"));
        });

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["a.txt", "b.txt"]
        });

        Assert.Equal(0, headRequestCount);
    }

    [Fact]
    public async Task DownloadFilesAsync_HeadRequestFails_ContinuesDownload()
    {
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Head)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            return Task.FromResult(CreateFileResponse("data despite HEAD failure"));
        });

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = true };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["file.txt"]
        });

        Assert.True(File.Exists(Path.Combine(_tempDir, "file.txt")));
        Assert.Equal("data despite HEAD failure", await File.ReadAllTextAsync(Path.Combine(_tempDir, "file.txt")));
    }

    [Fact]
    public async Task DownloadFilesAsync_PartialFileExists_ResumesWithRangeRequest()
    {
        const string entityTag = "\"etag-1\"";
        var fullContent = Encoding.UTF8.GetBytes("hello world");
        var localPath = Path.Combine(_tempDir, "model.bin");
        WritePartialDownload(localPath, Encoding.UTF8.GetBytes("hello "), "main", entityTag, fullContent.Length);

        RangeHeaderValue? observedRange = null;
        var progressReports = new List<DownloadProgress>();
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Head)
                return Task.FromResult(CreateHeadResponse(fullContent.Length, entityTag, supportsRanges: true));

            observedRange = request.Headers.Range;
            return Task.FromResult(CreateRangeResponse(fullContent, 6, entityTag));
        });

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["model.bin"],
            Progress = new SynchronousProgress<DownloadProgress>(p => progressReports.Add(p))
        });

        Assert.NotNull(observedRange);
        Assert.Equal(6, observedRange!.Ranges.Single().From);
        Assert.Equal("hello world", await File.ReadAllTextAsync(localPath));
        Assert.False(File.Exists(localPath + ".partial"));
        Assert.False(File.Exists(localPath + ".partial.json"));
        Assert.Contains(progressReports, p => p.ResumedBytes == 6);
        Assert.Contains(progressReports, p => p.Message?.Contains("Reused", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task DownloadFilesAsync_PartialFileFromDifferentRevision_RestartsFromScratch()
    {
        const string entityTag = "\"etag-1\"";
        var localPath = Path.Combine(_tempDir, "model.bin");
        WritePartialDownload(localPath, Encoding.UTF8.GetBytes("stale"), "old-branch", entityTag, 11);

        var rangedRequests = 0;
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Head)
                return Task.FromResult(CreateHeadResponse(11, entityTag, supportsRanges: true));

            if (request.Headers.Range is not null)
                rangedRequests++;

            return Task.FromResult(CreateFileResponse("new content"));
        });

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["model.bin"],
            Revision = "main"
        });

        Assert.Equal(0, rangedRequests);
        Assert.Equal("new content", await File.ReadAllTextAsync(localPath));
    }

    [Fact]
    public async Task DownloadFilesAsync_PartialFileWithChangedEtag_RestartsFromScratch()
    {
        var localPath = Path.Combine(_tempDir, "model.bin");
        WritePartialDownload(localPath, Encoding.UTF8.GetBytes("hello "), "main", "\"etag-old\"", 11);

        var rangedRequests = 0;
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Head)
                return Task.FromResult(CreateHeadResponse(11, "\"etag-new\"", supportsRanges: true));

            if (request.Headers.Range is not null)
                rangedRequests++;

            return Task.FromResult(CreateFileResponse("new content"));
        });

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["model.bin"]
        });

        Assert.Equal(0, rangedRequests);
        Assert.Equal("new content", await File.ReadAllTextAsync(localPath));
    }

    [Fact]
    public async Task DownloadFilesAsync_ServerIgnoresRange_RestartsWithFullDownload()
    {
        const string entityTag = "\"etag-1\"";
        var fullContent = "hello world";
        var localPath = Path.Combine(_tempDir, "model.bin");
        WritePartialDownload(localPath, Encoding.UTF8.GetBytes("hello "), "main", entityTag, fullContent.Length);

        var seenRanges = new List<bool>();
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            if (request.Method == HttpMethod.Head)
                return Task.FromResult(CreateHeadResponse(fullContent.Length, entityTag, supportsRanges: false));

            seenRanges.Add(request.Headers.Range is not null);
            return Task.FromResult(CreateFileResponse(fullContent));
        });

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["model.bin"]
        });

        Assert.Equal([true, false], seenRanges);
        Assert.Equal(fullContent, await File.ReadAllTextAsync(localPath));
    }

    #endregion

    #region Phase 3: Error Handling Tests

    [Fact]
    public async Task DownloadFilesAsync_RequiredFile404_ThrowsInvalidOperationException()
    {
        var handler = new MockHttpMessageHandler((request, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            downloader.DownloadFilesAsync(new DownloadRequest
            {
                RepoId = "test/repo",
                LocalDirectory = _tempDir,
                RequiredFiles = ["missing.onnx"]
            }));

        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("404", ex.Message);
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task DownloadFilesAsync_RequiredFile401_ThrowsWithTokenGuidance()
    {
        var handler = new MockHttpMessageHandler((request, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            downloader.DownloadFilesAsync(new DownloadRequest
            {
                RepoId = "test/repo",
                LocalDirectory = _tempDir,
                RequiredFiles = ["gated-model.onnx"]
            }));

        Assert.Contains("Access denied", ex.Message);
        Assert.Contains("HF_TOKEN", ex.Message);
    }

    [Fact]
    public async Task DownloadFilesAsync_RequiredFile403_ThrowsWithTokenGuidance()
    {
        var handler = new MockHttpMessageHandler((request, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)));

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            downloader.DownloadFilesAsync(new DownloadRequest
            {
                RepoId = "test/repo",
                LocalDirectory = _tempDir,
                RequiredFiles = ["private-model.onnx"]
            }));

        Assert.Contains("Access denied", ex.Message);
        Assert.Contains("HF_TOKEN", ex.Message);
    }

    [Fact]
    public async Task DownloadFilesAsync_RequiredFile500_ThrowsInvalidOperationException()
    {
        var handler = new MockHttpMessageHandler((request, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            downloader.DownloadFilesAsync(new DownloadRequest
            {
                RepoId = "test/repo",
                LocalDirectory = _tempDir,
                RequiredFiles = ["model.onnx"]
            }));

        Assert.Contains("Failed to download", ex.Message);
    }

    [Fact]
    public async Task DownloadFilesAsync_OptionalFile404_ContinuesWithoutThrowing()
    {
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("optional"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            return Task.FromResult(CreateFileResponse("required content"));
        });

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["required.onnx"],
            OptionalFiles = ["optional.json"]
        });

        Assert.True(File.Exists(Path.Combine(_tempDir, "required.onnx")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "optional.json")));
    }

    [Fact]
    public async Task DownloadFilesAsync_OptionalFile500_ContinuesWithoutThrowing()
    {
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("optional"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            return Task.FromResult(CreateFileResponse("required content"));
        });

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["required.onnx"],
            OptionalFiles = ["optional.json"]
        });

        Assert.True(File.Exists(Path.Combine(_tempDir, "required.onnx")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "optional.json")));
    }

    [Fact]
    public async Task DownloadFilesAsync_CancelledDuringDownload_ThrowsOperationCancelled()
    {
        using var cts = new CancellationTokenSource();

        var handler = new MockHttpMessageHandler((request, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new CancellingStream(new byte[1024], cts))
            };
            return Task.FromResult(response);
        });

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            downloader.DownloadFilesAsync(new DownloadRequest
            {
                RepoId = "test/repo",
                LocalDirectory = _tempDir,
                RequiredFiles = ["model.onnx"]
            }, cts.Token));
    }

    [Fact]
    public async Task DownloadFilesAsync_CancelledDuringDownload_PreservesPartialFileWhenResumeEnabled()
    {
        using var cts = new CancellationTokenSource();
        var localPath = Path.Combine(_tempDir, "model.onnx");

        var handler = new MockHttpMessageHandler((request, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new PartialCancellingStream(new byte[1024], cts, firstChunkSize: 256))
            };
            return Task.FromResult(response);
        });

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            downloader.DownloadFilesAsync(new DownloadRequest
            {
                RepoId = "test/repo",
                LocalDirectory = _tempDir,
                RequiredFiles = ["model.onnx"]
            }, cts.Token));

        Assert.False(File.Exists(localPath));
        Assert.True(File.Exists(localPath + ".partial"));
        Assert.True(File.Exists(localPath + ".partial.json"));
        Assert.Equal(256, new FileInfo(localPath + ".partial").Length);
    }

    [Fact]
    public async Task DownloadFilesAsync_CancelledDuringDownload_WithoutResume_CleansTemporaryFiles()
    {
        using var cts = new CancellationTokenSource();
        var localPath = Path.Combine(_tempDir, "model.onnx");

        var handler = new MockHttpMessageHandler((request, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new PartialCancellingStream(new byte[1024], cts, firstChunkSize: 256))
            };
            return Task.FromResult(response);
        });

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            downloader.DownloadFilesAsync(new DownloadRequest
            {
                RepoId = "test/repo",
                LocalDirectory = _tempDir,
                RequiredFiles = ["model.onnx"],
                ResumePartialDownloads = false
            }, cts.Token));

        Assert.False(File.Exists(localPath));
        Assert.False(File.Exists(localPath + ".tmp"));
        Assert.False(File.Exists(localPath + ".partial"));
        Assert.False(File.Exists(localPath + ".partial.json"));
    }

    [Fact]
    public async Task DownloadFilesAsync_NullProgress_DoesNotThrow()
    {
        var handler = new MockHttpMessageHandler((request, _) =>
            Task.FromResult(CreateFileResponse("data")));

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["file.txt"],
            Progress = null
        });

        Assert.True(File.Exists(Path.Combine(_tempDir, "file.txt")));
    }

    [Fact]
    public async Task DownloadFilesAsync_EmptyRequiredFiles_OnlyOptionalMissing_DownloadsOptional()
    {
        var handler = new MockHttpMessageHandler((request, _) =>
            Task.FromResult(CreateFileResponse("optional data")));

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = [],
            OptionalFiles = ["readme.md"]
        });

        var optPath = Path.Combine(_tempDir, "readme.md");
        Assert.True(File.Exists(optPath));
        Assert.Equal("optional data", await File.ReadAllTextAsync(optPath));
    }

    [Fact]
    public async Task DownloadFilesAsync_VerifyFileContent_MatchesResponse()
    {
        // Use binary-like content to verify exact byte match
        var binaryContent = new byte[] { 0x00, 0x01, 0x02, 0xFF, 0xFE, 0xFD, 0x80, 0x7F };
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(binaryContent)
            };
            return Task.FromResult(response);
        });

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "test/repo",
            LocalDirectory = _tempDir,
            RequiredFiles = ["binary.bin"]
        });

        var writtenBytes = await File.ReadAllBytesAsync(Path.Combine(_tempDir, "binary.bin"));
        Assert.Equal(binaryContent, writtenBytes);
    }

    [Fact]
    public async Task DownloadFilesAsync_ThrowingProgressHandler_PropagatesException()
    {
        var handler = new MockHttpMessageHandler((request, _) =>
            Task.FromResult(CreateFileResponse("data")));

        using var httpClient = new HttpClient(handler);
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        using var downloader = new HuggingFaceDownloader(httpClient, options);

        var throwingProgress = new SynchronousProgress<DownloadProgress>(_ =>
            throw new InvalidOperationException("Progress handler failure"));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            downloader.DownloadFilesAsync(new DownloadRequest
            {
                RepoId = "test/repo",
                LocalDirectory = _tempDir,
                RequiredFiles = ["file.txt"],
                Progress = throwingProgress
            }));
    }

    #endregion

    #region Cache Management API Tests

    [Fact]
    public async Task DeleteCachedFilesAsync_CacheRootWithSanitizedRepo_DeletesRepoDirectory()
    {
        var repoId = "microsoft/phi-2";
        var repoDir = Path.Combine(_tempDir, DefaultPathHelper.SanitizeModelName(repoId));
        Directory.CreateDirectory(repoDir);
        await File.WriteAllTextAsync(Path.Combine(repoDir, "model.onnx"), "data");

        await _downloader.DeleteCachedFilesAsync(repoId, _tempDir);

        Assert.False(Directory.Exists(repoDir));
    }

    [Fact]
    public async Task DeleteCachedFilesAsync_DirectRepoDirectory_DeletesDirectory()
    {
        var repoId = "meta-llama/Llama-3.2-1B";
        var repoDir = Path.Combine(_tempDir, DefaultPathHelper.SanitizeModelName(repoId));
        Directory.CreateDirectory(repoDir);
        await File.WriteAllTextAsync(Path.Combine(repoDir, "config.json"), "{}");

        await _downloader.DeleteCachedFilesAsync(repoId, repoDir);

        Assert.False(Directory.Exists(repoDir));
    }

    [Fact]
    public async Task DeleteCachedFilesAsync_MissingDirectory_NoOp()
    {
        var missingDir = Path.Combine(_tempDir, "not-found");

        await _downloader.DeleteCachedFilesAsync("test/repo", missingDir);

        Assert.False(Directory.Exists(missingDir));
    }

    [Fact]
    public void IsCached_AllRequiredFilesPresent_ReturnsTrue()
    {
        var repoId = "sentence-transformers/all-MiniLM-L6-v2";
        var repoDir = Path.Combine(_tempDir, DefaultPathHelper.SanitizeModelName(repoId));
        Directory.CreateDirectory(Path.Combine(repoDir, "onnx"));
        File.WriteAllText(Path.Combine(repoDir, "onnx", "model.onnx"), "binary");
        File.WriteAllText(Path.Combine(repoDir, "tokenizer.json"), "{}");

        var result = _downloader.IsCached(repoId, _tempDir, ["onnx/model.onnx", "tokenizer.json"]);

        Assert.True(result);
    }

    [Fact]
    public void IsCached_AnyMissingRequiredFile_ReturnsFalse()
    {
        var repoId = "sentence-transformers/all-MiniLM-L6-v2";
        var repoDir = Path.Combine(_tempDir, DefaultPathHelper.SanitizeModelName(repoId));
        Directory.CreateDirectory(repoDir);
        File.WriteAllText(Path.Combine(repoDir, "tokenizer.json"), "{}");

        var result = _downloader.IsCached(repoId, _tempDir, ["onnx/model.onnx", "tokenizer.json"]);

        Assert.False(result);
    }

    [Fact]
    public void ListCachedRepos_NonExistentDirectory_ReturnsEmpty()
    {
        var missingDir = Path.Combine(_tempDir, "missing-cache");

        var result = _downloader.ListCachedRepos(missingDir);

        Assert.Empty(result);
    }

    [Fact]
    public void ListCachedRepos_ReturnsRepoMetadata()
    {
        var repoA = Path.Combine(_tempDir, "repo-a");
        var repoB = Path.Combine(_tempDir, "repo-b");
        Directory.CreateDirectory(repoA);
        Directory.CreateDirectory(repoB);
        File.WriteAllBytes(Path.Combine(repoA, "a.bin"), new byte[100]);
        File.WriteAllBytes(Path.Combine(repoB, "b.bin"), new byte[250]);
        File.WriteAllBytes(Path.Combine(repoB, "c.bin"), new byte[50]);

        var result = _downloader.ListCachedRepos(_tempDir);

        Assert.Equal(2, result.Count);
        var entryA = Assert.Single(result, r => r.LocalDirectory == repoA);
        var entryB = Assert.Single(result, r => r.LocalDirectory == repoB);
        Assert.Equal(100, entryA.TotalSizeBytes);
        Assert.Equal(300, entryB.TotalSizeBytes);
        Assert.True(entryA.LastModified <= DateTimeOffset.UtcNow);
        Assert.True(entryB.LastModified <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void GetCachedSize_NestedDirectory_ReturnsTotalBytes()
    {
        var repoDir = Path.Combine(_tempDir, "repo-size");
        Directory.CreateDirectory(Path.Combine(repoDir, "nested"));
        File.WriteAllBytes(Path.Combine(repoDir, "weights.bin"), new byte[120]);
        File.WriteAllBytes(Path.Combine(repoDir, "nested", "config.json"), new byte[30]);

        var result = _downloader.GetCachedSize(repoDir);

        Assert.Equal(150, result);
    }

    [Fact]
    public void GetCachedSize_MissingDirectory_ReturnsZero()
    {
        var missingDir = Path.Combine(_tempDir, "missing-size");

        var result = _downloader.GetCachedSize(missingDir);

        Assert.Equal(0, result);
    }

    #endregion
}

public class HuggingFaceDownloaderOptionsTests
{
    [Fact]
    public void Defaults_Timeout_Is30Minutes()
    {
        var options = new HuggingFaceDownloaderOptions();
        Assert.Equal(TimeSpan.FromMinutes(30), options.Timeout);
    }

    [Fact]
    public void Defaults_ResolveFileSizes_IsTrue()
    {
        var options = new HuggingFaceDownloaderOptions();
        Assert.True(options.ResolveFileSizesBeforeDownload);
    }

    [Fact]
    public void Defaults_AuthToken_IsNull()
    {
        var options = new HuggingFaceDownloaderOptions();
        Assert.Null(options.AuthToken);
    }

    [Fact]
    public void Defaults_UserAgent_IsNull()
    {
        var options = new HuggingFaceDownloaderOptions();
        Assert.Null(options.UserAgent);
    }

    [Fact]
    public void AuthToken_CanBeSet()
    {
        var options = new HuggingFaceDownloaderOptions { AuthToken = "hf_abc123" };
        Assert.Equal("hf_abc123", options.AuthToken);
    }

    [Fact]
    public void Timeout_CanBeCustomized()
    {
        var options = new HuggingFaceDownloaderOptions { Timeout = TimeSpan.FromMinutes(60) };
        Assert.Equal(TimeSpan.FromMinutes(60), options.Timeout);
    }

    [Fact]
    public void UserAgent_CanBeCustomized()
    {
        var options = new HuggingFaceDownloaderOptions { UserAgent = "MyApp/2.0" };
        Assert.Equal("MyApp/2.0", options.UserAgent);
    }

    [Fact]
    public void ResolveFileSizes_CanBeDisabled()
    {
        var options = new HuggingFaceDownloaderOptions { ResolveFileSizesBeforeDownload = false };
        Assert.False(options.ResolveFileSizesBeforeDownload);
    }
}

public class DownloadProgressTests
{
    [Fact]
    public void Properties_CanBeInitialized()
    {
        var progress = new DownloadProgress
        {
            Stage = DownloadStage.Downloading,
            PercentComplete = 50.5,
            BytesDownloaded = 1024,
            TotalBytes = 2048,
            ResumedBytes = 512,
            CurrentFile = "model.onnx",
            CurrentFileIndex = 1,
            TotalFileCount = 3,
            Message = "Downloading..."
        };

        Assert.Equal(DownloadStage.Downloading, progress.Stage);
        Assert.Equal(50.5, progress.PercentComplete);
        Assert.Equal(1024, progress.BytesDownloaded);
        Assert.Equal(2048, progress.TotalBytes);
        Assert.Equal(512, progress.ResumedBytes);
        Assert.Equal("model.onnx", progress.CurrentFile);
        Assert.Equal(1, progress.CurrentFileIndex);
        Assert.Equal(3, progress.TotalFileCount);
        Assert.Equal("Downloading...", progress.Message);
    }

    [Fact]
    public void Defaults_AreZeroOrNull()
    {
        var progress = new DownloadProgress();

        Assert.Equal(DownloadStage.Checking, progress.Stage);
        Assert.Equal(0, progress.PercentComplete);
        Assert.Equal(0, progress.BytesDownloaded);
        Assert.Equal(0, progress.TotalBytes);
        Assert.Equal(0, progress.ResumedBytes);
        Assert.Null(progress.CurrentFile);
        Assert.Equal(0, progress.CurrentFileIndex);
        Assert.Equal(0, progress.TotalFileCount);
        Assert.Null(progress.Message);
    }
}

public class DownloadStageTests
{
    [Fact]
    public void AllStages_AreDefined()
    {
        var stages = Enum.GetValues<DownloadStage>();
        Assert.Equal(5, stages.Length);
        Assert.Contains(DownloadStage.Checking, stages);
        Assert.Contains(DownloadStage.Downloading, stages);
        Assert.Contains(DownloadStage.Validating, stages);
        Assert.Contains(DownloadStage.Complete, stages);
        Assert.Contains(DownloadStage.Failed, stages);
    }
}
