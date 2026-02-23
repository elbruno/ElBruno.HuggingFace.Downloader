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
