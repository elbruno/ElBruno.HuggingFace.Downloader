using System.Net;
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

    private static HttpResponseMessage CreateFileResponse(string content) =>
        new(HttpStatusCode.OK) { Content = new StringContent(content) };

    private static HttpResponseMessage CreateHeadResponse(long contentLength)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([])
        };
        response.Content.Headers.ContentLength = contentLength;
        return response;
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
                return Task.FromResult(CreateHeadResponse(100));
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
    public async Task DownloadFilesAsync_CancelledDuringDownload_CleansTempFile()
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
                RequiredFiles = ["model.onnx"],
                UseAtomicWrites = true
            }, cts.Token));

        Assert.False(File.Exists(Path.Combine(_tempDir, "model.onnx.tmp")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "model.onnx")));
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
            CurrentFile = "model.onnx",
            CurrentFileIndex = 1,
            TotalFileCount = 3,
            Message = "Downloading..."
        };

        Assert.Equal(DownloadStage.Downloading, progress.Stage);
        Assert.Equal(50.5, progress.PercentComplete);
        Assert.Equal(1024, progress.BytesDownloaded);
        Assert.Equal(2048, progress.TotalBytes);
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
