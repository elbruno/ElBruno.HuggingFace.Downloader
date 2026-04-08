using Xunit;
using ElBruno.HuggingFace.Cli.Services;

namespace ElBruno.HuggingFace.Cli.Tests;

/// <summary>
/// Edge case tests for <see cref="CacheManager"/> — nested dirs, sanitized names, loose files.
/// </summary>
public sealed class CacheManagerEdgeCaseTests : IDisposable
{
    private readonly string _tempDir;
    private readonly CacheManager _manager;

    public CacheManagerEdgeCaseTests()
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

    // ── GetCachedModels with nested subdirectories ──────────────────

    [Fact]
    public void GetCachedModels_NestedSubdirectories_CountsAllNestedFiles()
    {
        var modelDir = Path.Combine(_tempDir, "nested-model");
        Directory.CreateDirectory(modelDir);
        File.WriteAllBytes(Path.Combine(modelDir, "root.bin"), new byte[100]);

        var subDir = Path.Combine(modelDir, "subdir");
        Directory.CreateDirectory(subDir);
        File.WriteAllBytes(Path.Combine(subDir, "nested.bin"), new byte[200]);

        var result = _manager.GetCachedModels(_tempDir);

        var model = Assert.Single(result);
        Assert.Equal(2, model.FileCount);
        Assert.Equal(300, model.TotalSize);
    }

    // ── GetModelDetails resolves sanitized repo ID ──────────────────

    [Fact]
    public void GetModelDetails_SanitizedRepoId_ResolvesCorrectly()
    {
        // "microsoft/phi-2" sanitizes to "microsoft_phi-2"
        TestHelpers.CreateModelDir(_tempDir, "microsoft_phi-2", ("model.bin", 500));

        var result = _manager.GetModelDetails(_tempDir, "microsoft/phi-2");

        Assert.NotNull(result);
        Assert.Equal("microsoft_phi-2", result.Name);
    }

    [Fact]
    public void GetModelDetails_DirectNameFallback_WhenSanitizedDoesntMatch()
    {
        // Directory already named "my-model" (not via sanitization)
        TestHelpers.CreateModelDir(_tempDir, "my-model", ("data.bin", 50));

        var result = _manager.GetModelDetails(_tempDir, "my-model");

        Assert.NotNull(result);
        Assert.Equal("my-model", result.Name);
    }

    // ── DeleteModel with deeply nested structure ────────────────────

    [Fact]
    public void DeleteModel_DeeplyNestedStructure_RemovesEverything()
    {
        var modelDir = Path.Combine(_tempDir, "deep-model");
        var deepPath = Path.Combine(modelDir, "a", "b", "c");
        Directory.CreateDirectory(deepPath);
        File.WriteAllBytes(Path.Combine(deepPath, "deep.bin"), new byte[10]);
        File.WriteAllBytes(Path.Combine(modelDir, "top.bin"), new byte[20]);

        var result = _manager.DeleteModel(_tempDir, "deep-model");

        Assert.True(result);
        Assert.False(Directory.Exists(modelDir));
    }

    // ── DeleteFile with nested subdirectory file ────────────────────

    [Fact]
    public void DeleteFile_NestedSubdirectoryFile_DeletesCorrectly()
    {
        var modelDir = Path.Combine(_tempDir, "nested-del");
        var subDir = Path.Combine(modelDir, "subdir");
        Directory.CreateDirectory(subDir);
        File.WriteAllBytes(Path.Combine(subDir, "weights.bin"), new byte[100]);
        File.WriteAllBytes(Path.Combine(modelDir, "config.json"), new byte[50]);

        var result = _manager.DeleteFile(_tempDir, "nested-del", Path.Combine("subdir", "weights.bin"));

        Assert.True(result);
        Assert.False(File.Exists(Path.Combine(subDir, "weights.bin")));
        Assert.True(File.Exists(Path.Combine(modelDir, "config.json")));
    }

    // ── PurgeAll removes loose files at cache root ──────────────────

    [Fact]
    public void PurgeAll_RemovesLooseFilesAtCacheRoot()
    {
        TestHelpers.CreateModelDir(_tempDir, "model-x", ("a.bin", 10));
        File.WriteAllBytes(Path.Combine(_tempDir, "stray-file.txt"), new byte[5]);

        _manager.PurgeAll(_tempDir);

        Assert.Empty(Directory.GetDirectories(_tempDir));
        Assert.Empty(Directory.GetFiles(_tempDir));
    }

    // ── GetCachedModels LastModified from most recent file ──────────

    [Fact]
    public void GetCachedModels_LastModified_ReflectsMostRecentFile()
    {
        var modelDir = Path.Combine(_tempDir, "time-model");
        Directory.CreateDirectory(modelDir);

        var oldFile = Path.Combine(modelDir, "old.bin");
        File.WriteAllBytes(oldFile, new byte[10]);
        File.SetLastWriteTime(oldFile, new DateTime(2020, 1, 1));

        var newFile = Path.Combine(modelDir, "new.bin");
        File.WriteAllBytes(newFile, new byte[20]);
        var expectedTime = new DateTime(2025, 6, 15, 12, 0, 0);
        File.SetLastWriteTime(newFile, expectedTime);

        // Set directory time older so file time wins
        Directory.SetLastWriteTime(modelDir, new DateTime(2019, 1, 1));

        var result = _manager.GetCachedModels(_tempDir);

        var model = Assert.Single(result);
        Assert.Equal(expectedTime, model.LastModified);
    }

    // ── CachedModel.Files includes nested files with relative paths ─

    [Fact]
    public void GetModelDetails_NestedFiles_HaveProperRelativePaths()
    {
        var modelDir = Path.Combine(_tempDir, "rel-path-model");
        Directory.CreateDirectory(modelDir);
        File.WriteAllBytes(Path.Combine(modelDir, "root.bin"), new byte[10]);

        var sub = Path.Combine(modelDir, "sub");
        Directory.CreateDirectory(sub);
        File.WriteAllBytes(Path.Combine(sub, "nested.bin"), new byte[20]);

        var result = _manager.GetModelDetails(_tempDir, "rel-path-model");

        Assert.NotNull(result);
        Assert.Equal(2, result.Files.Count);
        Assert.Contains(result.Files, f => f.RelativePath == "root.bin");
        Assert.Contains(result.Files, f => f.RelativePath == Path.Combine("sub", "nested.bin"));
    }

    // ── Multiple models with same file names in different dirs ──────

    [Fact]
    public void GetCachedModels_MultipleModels_SameFileNames_IndependentMetrics()
    {
        TestHelpers.CreateModelDir(_tempDir, "model-alpha", ("weights.bin", 100), ("config.json", 50));
        TestHelpers.CreateModelDir(_tempDir, "model-beta", ("weights.bin", 200), ("config.json", 75));

        var result = _manager.GetCachedModels(_tempDir);

        Assert.Equal(2, result.Count);
        var alpha = result.First(m => m.Name == "model-alpha");
        var beta = result.First(m => m.Name == "model-beta");

        Assert.Equal(150, alpha.TotalSize);
        Assert.Equal(275, beta.TotalSize);
        Assert.Equal(2, alpha.FileCount);
        Assert.Equal(2, beta.FileCount);
    }
}
