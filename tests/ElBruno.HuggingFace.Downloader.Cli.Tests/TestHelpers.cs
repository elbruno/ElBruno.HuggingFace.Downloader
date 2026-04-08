using System.CommandLine;
using ElBruno.HuggingFace.Cli.Commands;

namespace ElBruno.HuggingFace.Cli.Tests;

/// <summary>
/// Shared helpers for CLI test classes.
/// </summary>
internal static class TestHelpers
{
    /// <summary>
    /// Builds the root command the same way Program.cs does.
    /// </summary>
    public static RootCommand BuildRootCommand()
    {
        var root = new RootCommand(
            "HuggingFace Downloader CLI — download, manage, and inspect cached models from Hugging Face Hub.");

        root.Add(DownloadCommand.Create());
        root.Add(CheckCommand.Create());
        root.Add(ListCommand.Create());
        root.Add(InfoCommand.Create());
        root.Add(DeleteCommand.Create());
        root.Add(DeleteFileCommand.Create());
        root.Add(PurgeCommand.Create());
        root.Add(ConfigCommand.Create());

        return root;
    }

    /// <summary>
    /// Creates a model directory with the given files inside a cache root.
    /// </summary>
    public static void CreateModelDir(string cacheDir, string modelName, params (string fileName, int size)[] files)
    {
        var modelDir = Path.Combine(cacheDir, modelName);
        Directory.CreateDirectory(modelDir);

        foreach (var (fileName, size) in files)
        {
            var filePath = Path.Combine(modelDir, fileName);
            var dir = Path.GetDirectoryName(filePath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllBytes(filePath, new byte[size]);
        }
    }
}
