using System.CommandLine;
using Xunit;

namespace ElBruno.HuggingFace.Cli.Tests;

/// <summary>
/// End-to-end tests that invoke CLI commands through System.CommandLine
/// and verify exit codes and filesystem side effects.
/// </summary>
[Collection("ConfigFile")]
public sealed class CommandExecutionTests : IDisposable
{
    private readonly string _tempDir;

    public CommandExecutionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static async Task<int> InvokeAsync(params string[] args)
    {
        var root = TestHelpers.BuildRootCommand();
        var config = new CommandLineConfiguration(root);
        return await config.InvokeAsync(args);
    }

    // ── Check command E2E ────────────────────────────────────────────

    [Fact]
    public async Task Check_AllFilesPresent_ReturnsZero()
    {
        var outputDir = Path.Combine(_tempDir, "check-ok");
        Directory.CreateDirectory(outputDir);
        File.WriteAllBytes(Path.Combine(outputDir, "model.bin"), new byte[10]);
        File.WriteAllBytes(Path.Combine(outputDir, "config.json"), new byte[5]);

        var exitCode = await InvokeAsync("check", "test-repo", "model.bin", "config.json", "--output", outputDir);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Check_MissingFiles_ReturnsOne()
    {
        var outputDir = Path.Combine(_tempDir, "check-missing");
        Directory.CreateDirectory(outputDir);

        var exitCode = await InvokeAsync("check", "test-repo", "nonexistent.bin", "--output", outputDir);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task Check_MixedPresentAndMissing_ReturnsOne()
    {
        var outputDir = Path.Combine(_tempDir, "check-mixed");
        Directory.CreateDirectory(outputDir);
        File.WriteAllBytes(Path.Combine(outputDir, "present.bin"), new byte[10]);

        var exitCode = await InvokeAsync("check", "test-repo", "present.bin", "missing.bin", "--output", outputDir);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task Check_WithOutputOption_UsesSpecifiedDir()
    {
        var outputDir = Path.Combine(_tempDir, "custom-output");
        Directory.CreateDirectory(outputDir);
        File.WriteAllBytes(Path.Combine(outputDir, "file.txt"), new byte[1]);

        var exitCode = await InvokeAsync("check", "test-repo", "file.txt", "--output", outputDir);

        Assert.Equal(0, exitCode);
    }

    // ── List command E2E ─────────────────────────────────────────────

    [Fact]
    public async Task List_EmptyCacheDir_ReturnsZero()
    {
        var cacheDir = Path.Combine(_tempDir, "empty-cache");
        Directory.CreateDirectory(cacheDir);

        var exitCode = await InvokeAsync("list", "--cache-dir", cacheDir);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task List_PopulatedCacheDir_ReturnsZero()
    {
        var cacheDir = Path.Combine(_tempDir, "pop-cache");
        TestHelpers.CreateModelDir(cacheDir, "model-a", ("weights.bin", 100));
        TestHelpers.CreateModelDir(cacheDir, "model-b", ("config.json", 50));

        var exitCode = await InvokeAsync("list", "--cache-dir", cacheDir);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task List_FormatJson_ReturnsZero()
    {
        var cacheDir = Path.Combine(_tempDir, "json-cache");
        TestHelpers.CreateModelDir(cacheDir, "model-j", ("data.bin", 200));

        var exitCode = await InvokeAsync("list", "--cache-dir", cacheDir, "--format", "json");

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task List_FormatTable_ReturnsZero()
    {
        var cacheDir = Path.Combine(_tempDir, "table-cache");
        TestHelpers.CreateModelDir(cacheDir, "model-t", ("data.bin", 200));

        var exitCode = await InvokeAsync("list", "--cache-dir", cacheDir, "--format", "table");

        Assert.Equal(0, exitCode);
    }

    // ── Info command E2E ─────────────────────────────────────────────

    [Fact]
    public async Task Info_ExistingModel_ReturnsZero()
    {
        var cacheDir = Path.Combine(_tempDir, "info-cache");
        TestHelpers.CreateModelDir(cacheDir, "my-model", ("weights.bin", 500));

        var exitCode = await InvokeAsync("info", "my-model", "--cache-dir", cacheDir);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Info_NonExistentModel_ReturnsOne()
    {
        var cacheDir = Path.Combine(_tempDir, "info-cache-empty");
        Directory.CreateDirectory(cacheDir);

        var exitCode = await InvokeAsync("info", "ghost-model", "--cache-dir", cacheDir);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task Info_FormatJson_ReturnsZero()
    {
        var cacheDir = Path.Combine(_tempDir, "info-json");
        TestHelpers.CreateModelDir(cacheDir, "json-model", ("config.json", 30));

        var exitCode = await InvokeAsync("info", "json-model", "--cache-dir", cacheDir, "--format", "json");

        Assert.Equal(0, exitCode);
    }

    // ── Delete command E2E ───────────────────────────────────────────

    [Fact]
    public async Task Delete_ExistingModelWithForce_ReturnsZero_DirectoryRemoved()
    {
        var cacheDir = Path.Combine(_tempDir, "del-cache");
        TestHelpers.CreateModelDir(cacheDir, "doomed-model", ("file.bin", 10));

        var exitCode = await InvokeAsync("delete", "doomed-model", "--cache-dir", cacheDir, "--force");

        Assert.Equal(0, exitCode);
        Assert.False(Directory.Exists(Path.Combine(cacheDir, "doomed-model")));
    }

    [Fact]
    public async Task Delete_NonExistentModel_ReturnsOne()
    {
        var cacheDir = Path.Combine(_tempDir, "del-cache-empty");
        Directory.CreateDirectory(cacheDir);

        var exitCode = await InvokeAsync("delete", "no-such-model", "--cache-dir", cacheDir, "--force");

        Assert.Equal(1, exitCode);
    }

    // ── DeleteFile command E2E ───────────────────────────────────────

    [Fact]
    public async Task DeleteFile_ExistingFile_ReturnsZero_FileRemoved()
    {
        var cacheDir = Path.Combine(_tempDir, "delf-cache");
        TestHelpers.CreateModelDir(cacheDir, "my-model", ("remove.txt", 20), ("keep.txt", 10));

        var exitCode = await InvokeAsync("delete-file", "my-model", "remove.txt", "--cache-dir", cacheDir, "--force");

        Assert.Equal(0, exitCode);
        Assert.False(File.Exists(Path.Combine(cacheDir, "my-model", "remove.txt")));
        Assert.True(File.Exists(Path.Combine(cacheDir, "my-model", "keep.txt")));
    }

    [Fact]
    public async Task DeleteFile_NonExistentModel_ReturnsOne()
    {
        var cacheDir = Path.Combine(_tempDir, "delf-nomodel");
        Directory.CreateDirectory(cacheDir);

        var exitCode = await InvokeAsync("delete-file", "missing-model", "file.txt", "--cache-dir", cacheDir, "--force");

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task DeleteFile_NonExistentFile_ReturnsOne()
    {
        var cacheDir = Path.Combine(_tempDir, "delf-nofile");
        TestHelpers.CreateModelDir(cacheDir, "real-model", ("existing.txt", 10));

        var exitCode = await InvokeAsync("delete-file", "real-model", "ghost.txt", "--cache-dir", cacheDir, "--force");

        Assert.Equal(1, exitCode);
    }

    // ── Purge command E2E ────────────────────────────────────────────

    [Fact]
    public async Task Purge_PopulatedCache_ReturnsZero_AllCleared()
    {
        var cacheDir = Path.Combine(_tempDir, "purge-cache");
        TestHelpers.CreateModelDir(cacheDir, "model-1", ("a.bin", 10));
        TestHelpers.CreateModelDir(cacheDir, "model-2", ("b.bin", 20));

        var exitCode = await InvokeAsync("purge", "--cache-dir", cacheDir, "--force");

        Assert.Equal(0, exitCode);
        Assert.Empty(Directory.GetDirectories(cacheDir));
    }

    [Fact]
    public async Task Purge_EmptyCache_ReturnsZero()
    {
        var cacheDir = Path.Combine(_tempDir, "purge-empty");
        Directory.CreateDirectory(cacheDir);

        var exitCode = await InvokeAsync("purge", "--cache-dir", cacheDir, "--force");

        Assert.Equal(0, exitCode);
    }

    // ── Download command E2E ─────────────────────────────────────────

    [Fact]
    public async Task Download_NoFilesSpecified_ReturnsOne()
    {
        var exitCode = await InvokeAsync("download", "test-org/test-repo");

        Assert.Equal(1, exitCode);
    }

    // ── Config command E2E ───────────────────────────────────────────

    [Fact]
    public async Task Config_Show_ReturnsZero()
    {
        var exitCode = await InvokeAsync("config", "show");

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Config_SetValidKey_ReturnsZero()
    {
        var exitCode = await InvokeAsync("config", "set", "default-revision", "test-branch");

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task Config_ResetWithForce_ReturnsZero()
    {
        var exitCode = await InvokeAsync("config", "reset", "--force");

        Assert.Equal(0, exitCode);
    }
}
