using ElBruno.HuggingFace;
using Xunit;

namespace ElBruno.HuggingFace.Downloader.Tests;

public class HuggingFaceDownloaderTests
{
    [Fact]
    public void GetMissingFiles_AllMissing_ReturnsAll()
    {
        var downloader = new HuggingFaceDownloader();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var missing = downloader.GetMissingFiles(["a.onnx", "b.json"], tempDir);
            Assert.Equal(2, missing.Count);
            Assert.Contains("a.onnx", missing);
            Assert.Contains("b.json", missing);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GetMissingFiles_SomePresent_ReturnsOnlyMissing()
    {
        var downloader = new HuggingFaceDownloader();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "a.onnx"), "dummy");

        try
        {
            var missing = downloader.GetMissingFiles(["a.onnx", "b.json"], tempDir);
            Assert.Single(missing);
            Assert.Equal("b.json", missing[0]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void AreFilesAvailable_AllPresent_ReturnsTrue()
    {
        var downloader = new HuggingFaceDownloader();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "a.onnx"), "dummy");
        File.WriteAllText(Path.Combine(tempDir, "b.json"), "dummy");

        try
        {
            Assert.True(downloader.AreFilesAvailable(["a.onnx", "b.json"], tempDir));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void AreFilesAvailable_SomeMissing_ReturnsFalse()
    {
        var downloader = new HuggingFaceDownloader();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "a.onnx"), "dummy");

        try
        {
            Assert.False(downloader.AreFilesAvailable(["a.onnx", "b.json"], tempDir));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GetMissingFiles_NestedPaths_HandlesSlashConversion()
    {
        var downloader = new HuggingFaceDownloader();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var subDir = Path.Combine(tempDir, "onnx");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "model.onnx"), "dummy");

        try
        {
            var missing = downloader.GetMissingFiles(["onnx/model.onnx", "tokenizer.json"], tempDir);
            Assert.Single(missing);
            Assert.Equal("tokenizer.json", missing[0]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task DownloadFilesAsync_AllFilesExist_ReportsComplete()
    {
        var downloader = new HuggingFaceDownloader();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "file.txt"), "content");

        DownloadProgress? lastProgress = null;

        try
        {
            await downloader.DownloadFilesAsync(new DownloadRequest
            {
                RepoId = "test/repo",
                LocalDirectory = tempDir,
                RequiredFiles = ["file.txt"],
                Progress = new Progress<DownloadProgress>(p => lastProgress = p)
            });

            // Give the progress callback a moment to execute
            await Task.Delay(100);
            Assert.NotNull(lastProgress);
            Assert.Equal(DownloadStage.Complete, lastProgress!.Stage);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task DownloadFilesAsync_NullRequest_ThrowsArgumentNull()
    {
        var downloader = new HuggingFaceDownloader();
        await Assert.ThrowsAsync<ArgumentNullException>(() => downloader.DownloadFilesAsync(null!));
    }

    [Fact]
    public void Options_AuthToken_CanBeSet()
    {
        var options = new HuggingFaceDownloaderOptions { AuthToken = "my-token" };
        Assert.Equal("my-token", options.AuthToken);
    }
}
