using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using ElBruno.HuggingFace.Cli.Models;

namespace ElBruno.HuggingFace.Cli.Tests;

/// <summary>
/// Tests for <see cref="CachedModel"/> and <see cref="CachedFileInfo"/> serialization and defaults.
/// </summary>
public sealed class CachedModelTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact]
    public void CachedModel_SerializesToJson_Correctly()
    {
        var model = new CachedModel
        {
            Name = "test-model",
            FullPath = @"C:\cache\test-model",
            TotalSize = 1024,
            FileCount = 2,
            LastModified = new DateTime(2024, 1, 15, 10, 30, 0),
            Files =
            [
                new CachedFileInfo
                {
                    RelativePath = "weights.bin",
                    Size = 900,
                    LastModified = new DateTime(2024, 1, 15, 10, 30, 0),
                },
                new CachedFileInfo
                {
                    RelativePath = "config.json",
                    Size = 124,
                    LastModified = new DateTime(2024, 1, 14, 8, 0, 0),
                },
            ],
        };

        var json = JsonSerializer.Serialize(model, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<CachedModel>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal("test-model", deserialized.Name);
        Assert.Equal(1024, deserialized.TotalSize);
        Assert.Equal(2, deserialized.FileCount);
        Assert.Equal(2, deserialized.Files.Count);
    }

    [Fact]
    public void CachedFileInfo_SerializesToJson_Correctly()
    {
        var fileInfo = new CachedFileInfo
        {
            RelativePath = "subdir/model.onnx",
            Size = 4096,
            LastModified = new DateTime(2024, 3, 10, 14, 0, 0),
        };

        var json = JsonSerializer.Serialize(fileInfo, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<CachedFileInfo>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal("subdir/model.onnx", deserialized.RelativePath);
        Assert.Equal(4096, deserialized.Size);
    }

    [Fact]
    public void CachedModel_EmptyFilesList_SerializesCorrectly()
    {
        var model = new CachedModel
        {
            Name = "empty-model",
            FullPath = @"C:\cache\empty-model",
            TotalSize = 0,
            FileCount = 0,
            LastModified = DateTime.MinValue,
            Files = [],
        };

        var json = JsonSerializer.Serialize(model, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<CachedModel>(json, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal("empty-model", deserialized.Name);
        Assert.Empty(deserialized.Files);
    }

    [Fact]
    public void CachedModel_DefaultFiles_IsEmptyList()
    {
        var model = new CachedModel
        {
            Name = "default-files",
            FullPath = @"C:\cache\default-files",
            TotalSize = 0,
            FileCount = 0,
            LastModified = DateTime.MinValue,
        };

        Assert.NotNull(model.Files);
        Assert.Empty(model.Files);
    }
}
