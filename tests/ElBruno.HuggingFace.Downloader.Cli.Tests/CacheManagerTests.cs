using Xunit;
using ElBruno.HuggingFace.Cli.Services;

namespace ElBruno.HuggingFace.Cli.Tests;

/// <summary>
/// Tests for <see cref="CacheManager"/> — cache scanning, deletion, model enumeration.
/// </summary>
public sealed class CacheManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly CacheManager _manager;

    public CacheManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _manager = new CacheManager();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── GetCachedModels ─────────────────────────────────────────────

    [Fact]
    public void GetCachedModels_NonExistentDirectory_ReturnsEmptyList()
    {
        var nonExistent = Path.Combine(_tempDir, "does-not-exist");

        var result = _manager.GetCachedModels(nonExistent);

        Assert.Empty(result);
    }

    [Fact]
    public void GetCachedModels_EmptyDirectory_ReturnsEmptyList()
    {
        var result = _manager.GetCachedModels(_tempDir);

        Assert.Empty(result);
    }

    [Fact]
    public void GetCachedModels_PopulatedDirectory_ReturnsModels()
    {
        CreateModelDir("model-a", ("weights.bin", 100));
        CreateModelDir("model-b", ("config.json", 50), ("tokenizer.json", 75));

        var result = _manager.GetCachedModels(_tempDir);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, m => m.Name == "model-a");
        Assert.Contains(result, m => m.Name == "model-b");
    }

    [Fact]
    public void GetCachedModels_CalculatesTotalSizeCorrectly()
    {
        CreateModelDir("model-x", ("a.bin", 1024), ("b.bin", 2048));

        var result = _manager.GetCachedModels(_tempDir);

        var model = Assert.Single(result);
        Assert.Equal(1024 + 2048, model.TotalSize);
        Assert.Equal(2, model.FileCount);
    }

    [Fact]
    public void GetCachedModels_EmptyModelDirectory_ReportsZeroSize()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "empty-model"));

        var result = _manager.GetCachedModels(_tempDir);

        var model = Assert.Single(result);
        Assert.Equal(0, model.TotalSize);
        Assert.Equal(0, model.FileCount);
    }

    // ── GetModelDetails ─────────────────────────────────────────────

    [Fact]
    public void GetModelDetails_NonExistentModel_ReturnsNull()
    {
        var result = _manager.GetModelDetails(_tempDir, "no-such-model");

        Assert.Null(result);
    }

    [Fact]
    public void GetModelDetails_ExistingModel_ReturnsDetailedFileInfo()
    {
        CreateModelDir("microsoft_phi-2", ("model.onnx", 500), ("config.json", 30));

        var result = _manager.GetModelDetails(_tempDir, "microsoft_phi-2");

        Assert.NotNull(result);
        Assert.Equal("microsoft_phi-2", result.Name);
        Assert.Equal(2, result.FileCount);
        Assert.Equal(530, result.TotalSize);
        Assert.Equal(2, result.Files.Count);
        Assert.Contains(result.Files, f => f.RelativePath == "model.onnx");
        Assert.Contains(result.Files, f => f.RelativePath == "config.json");
    }

    [Fact]
    public void GetModelDetails_ReportsCorrectFileSizes()
    {
        CreateModelDir("size-test", ("big.bin", 4096));

        var result = _manager.GetModelDetails(_tempDir, "size-test");

        Assert.NotNull(result);
        var file = Assert.Single(result.Files);
        Assert.Equal(4096, file.Size);
    }

    // ── DeleteModel ─────────────────────────────────────────────────

    [Fact]
    public void DeleteModel_NonExistentModel_ReturnsFalse()
    {
        var result = _manager.DeleteModel(_tempDir, "phantom-model");

        Assert.False(result);
    }

    [Fact]
    public void DeleteModel_ExistingModel_RemovesDirectoryAndReturnsTrue()
    {
        CreateModelDir("doomed-model", ("file.bin", 10));

        var result = _manager.DeleteModel(_tempDir, "doomed-model");

        Assert.True(result);
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "doomed-model")));
    }

    [Fact]
    public void DeleteModel_AfterDeletion_GetModelDetailsReturnsNull()
    {
        CreateModelDir("transient", ("data.bin", 10));
        _manager.DeleteModel(_tempDir, "transient");

        var result = _manager.GetModelDetails(_tempDir, "transient");

        Assert.Null(result);
    }

    // ── DeleteFile ──────────────────────────────────────────────────

    [Fact]
    public void DeleteFile_ExistingFile_RemovesFileAndReturnsTrue()
    {
        CreateModelDir("my-model", ("keep.txt", 10), ("remove.txt", 20));

        var result = _manager.DeleteFile(_tempDir, "my-model", "remove.txt");

        Assert.True(result);
        Assert.False(File.Exists(Path.Combine(_tempDir, "my-model", "remove.txt")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "my-model", "keep.txt")));
    }

    [Fact]
    public void DeleteFile_NonExistentFile_ReturnsFalse()
    {
        CreateModelDir("sparse-model", ("only.txt", 10));

        var result = _manager.DeleteFile(_tempDir, "sparse-model", "ghost.txt");

        Assert.False(result);
    }

    [Fact]
    public void DeleteFile_NonExistentModel_ReturnsFalse()
    {
        var result = _manager.DeleteFile(_tempDir, "no-model", "file.txt");

        Assert.False(result);
    }

    [Fact]
    public void DeleteFile_PathTraversal_ReturnsFalse()
    {
        CreateModelDir("safe-model", ("legit.txt", 10));

        var result = _manager.DeleteFile(_tempDir, "safe-model", "../../etc/passwd");

        Assert.False(result);
    }

    [Fact]
    public void DeleteFile_PathTraversalWithBackslash_ReturnsFalse()
    {
        CreateModelDir("safe-model2", ("legit.txt", 10));

        var result = _manager.DeleteFile(_tempDir, "safe-model2", @"..\..\windows\system32\config");

        Assert.False(result);
    }

    // ── PurgeAll ────────────────────────────────────────────────────

    [Fact]
    public void PurgeAll_EmptyDirectory_ReturnsZero()
    {
        var result = _manager.PurgeAll(_tempDir);

        Assert.Equal(0, result);
    }

    [Fact]
    public void PurgeAll_NonExistentDirectory_ReturnsZero()
    {
        var nonExistent = Path.Combine(_tempDir, "nope");

        var result = _manager.PurgeAll(nonExistent);

        Assert.Equal(0, result);
    }

    [Fact]
    public void PurgeAll_MultipleModels_RemovesAllAndReturnsCount()
    {
        CreateModelDir("alpha", ("a.bin", 10));
        CreateModelDir("beta", ("b.bin", 20));
        CreateModelDir("gamma", ("c.bin", 30));

        var result = _manager.PurgeAll(_tempDir);

        Assert.Equal(3, result);
        Assert.Empty(Directory.GetDirectories(_tempDir));
    }

    [Fact]
    public void PurgeAll_AfterPurge_GetCachedModelsReturnsEmpty()
    {
        CreateModelDir("ephemeral", ("data.bin", 10));
        _manager.PurgeAll(_tempDir);

        var models = _manager.GetCachedModels(_tempDir);

        Assert.Empty(models);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private void CreateModelDir(string name, params (string fileName, int size)[] files)
    {
        var modelDir = Path.Combine(_tempDir, name);
        Directory.CreateDirectory(modelDir);

        foreach (var (fileName, size) in files)
        {
            var filePath = Path.Combine(modelDir, fileName);
            File.WriteAllBytes(filePath, new byte[size]);
        }
    }
}
