using Xunit;
using ElBruno.HuggingFace.Cli.Models;
using ElBruno.HuggingFace.Cli.Services;

namespace ElBruno.HuggingFace.Cli.Tests;

/// <summary>
/// Multi-step workflow regression tests for CacheManager and ConfigManager interactions.
/// </summary>
[Collection("ConfigFile")]
public sealed class RegressionTests : IDisposable
{
    private readonly string _tempDir;
    private readonly CacheManager _cacheManager;
    private readonly string _configPath;
    private readonly string? _originalConfigContent;
    private readonly bool _originalConfigExisted;

    public RegressionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _cacheManager = new CacheManager();

        _configPath = ConfigManager.GetConfigPath();
        _originalConfigExisted = File.Exists(_configPath);
        _originalConfigContent = _originalConfigExisted ? File.ReadAllText(_configPath) : null;

        if (_originalConfigExisted)
            File.Delete(_configPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);

        // Restore config
        if (_originalConfigExisted && _originalConfigContent is not null)
        {
            var dir = Path.GetDirectoryName(_configPath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(_configPath, _originalConfigContent);
        }
        else if (File.Exists(_configPath))
        {
            File.Delete(_configPath);
        }
    }

    // ── Delete model → list no longer includes it ───────────────────

    [Fact]
    public void DeleteModel_ThenList_NoLongerIncludesIt()
    {
        TestHelpers.CreateModelDir(_tempDir, "model-a", ("a.bin", 10));
        TestHelpers.CreateModelDir(_tempDir, "model-b", ("b.bin", 20));

        _cacheManager.DeleteModel(_tempDir, "model-a");
        var models = _cacheManager.GetCachedModels(_tempDir);

        Assert.Single(models);
        Assert.Equal("model-b", models[0].Name);
    }

    // ── Purge → list returns empty → info returns null ──────────────

    [Fact]
    public void Purge_ThenList_Empty_ThenInfo_ReturnsNull()
    {
        TestHelpers.CreateModelDir(_tempDir, "model-x", ("x.bin", 100));
        TestHelpers.CreateModelDir(_tempDir, "model-y", ("y.bin", 200));

        _cacheManager.PurgeAll(_tempDir);

        var models = _cacheManager.GetCachedModels(_tempDir);
        Assert.Empty(models);

        var info = _cacheManager.GetModelDetails(_tempDir, "model-x");
        Assert.Null(info);
    }

    // ── Config set → show reflects → reset → back to defaults ──────

    [Fact]
    public void ConfigSet_ThenShow_ThenReset_ReturnsDefaults()
    {
        var manager = new ConfigManager();
        var config = manager.Load();

        manager.SetValue(config, "default-revision", "custom-branch");
        manager.SetValue(config, "no-progress", "true");
        manager.Save(config);

        var reloaded = manager.Load();
        Assert.Equal("custom-branch", reloaded.DefaultRevision);
        Assert.True(reloaded.NoProgress);

        manager.Reset();

        var afterReset = manager.Load();
        Assert.Equal("main", afterReset.DefaultRevision);
        Assert.False(afterReset.NoProgress);
    }

    // ── Create models → list → delete one → list → correct count ───

    [Fact]
    public void CreateModels_List_DeleteOne_List_CorrectCount()
    {
        TestHelpers.CreateModelDir(_tempDir, "alpha", ("a.bin", 10));
        TestHelpers.CreateModelDir(_tempDir, "beta", ("b.bin", 20));
        TestHelpers.CreateModelDir(_tempDir, "gamma", ("c.bin", 30));

        var beforeDelete = _cacheManager.GetCachedModels(_tempDir);
        Assert.Equal(3, beforeDelete.Count);

        _cacheManager.DeleteModel(_tempDir, "beta");

        var afterDelete = _cacheManager.GetCachedModels(_tempDir);
        Assert.Equal(2, afterDelete.Count);
        Assert.DoesNotContain(afterDelete, m => m.Name == "beta");
    }

    // ── DeleteFile → GetModelDetails shows reduced count/size ───────

    [Fact]
    public void DeleteFile_ThenModelDetails_ShowsReducedFileCountAndSize()
    {
        TestHelpers.CreateModelDir(_tempDir, "file-model",
            ("big.bin", 1000), ("small.txt", 100), ("config.json", 50));

        var before = _cacheManager.GetModelDetails(_tempDir, "file-model");
        Assert.NotNull(before);
        Assert.Equal(3, before.FileCount);
        Assert.Equal(1150, before.TotalSize);

        _cacheManager.DeleteFile(_tempDir, "file-model", "big.bin");

        var after = _cacheManager.GetModelDetails(_tempDir, "file-model");
        Assert.NotNull(after);
        Assert.Equal(2, after.FileCount);
        Assert.Equal(150, after.TotalSize);
    }

    // ── PurgeAll → empty → repopulate → works again ─────────────────

    [Fact]
    public void PurgeAll_ThenRepopulate_GetCachedModelsWorksAgain()
    {
        TestHelpers.CreateModelDir(_tempDir, "model-1", ("f.bin", 10));
        _cacheManager.PurgeAll(_tempDir);

        Assert.Empty(_cacheManager.GetCachedModels(_tempDir));

        TestHelpers.CreateModelDir(_tempDir, "model-2", ("g.bin", 20));

        var result = _cacheManager.GetCachedModels(_tempDir);
        Assert.Single(result);
        Assert.Equal("model-2", result[0].Name);
    }

    // ── Save config → modify → save again → load shows latest ──────

    [Fact]
    public void SaveConfig_Modify_SaveAgain_LoadShowsLatest()
    {
        var manager = new ConfigManager();

        var config1 = new CliConfig { DefaultRevision = "v1", CacheDirectory = @"C:\first" };
        manager.Save(config1);

        var config2 = manager.Load();
        config2.DefaultRevision = "v2";
        config2.CacheDirectory = @"C:\second";
        manager.Save(config2);

        var final = manager.Load();
        Assert.Equal("v2", final.DefaultRevision);
        Assert.Equal(@"C:\second", final.CacheDirectory);
    }
}
