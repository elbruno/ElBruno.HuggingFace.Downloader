using System.CommandLine;
using ElBruno.HuggingFace;
using ElBruno.HuggingFace.Cli.Services;
using Spectre.Console;

namespace ElBruno.HuggingFace.Cli.Commands;

/// <summary>
/// The <c>purge</c> command — deletes all cached models from the cache directory.
/// </summary>
public static class PurgeCommand
{
    /// <summary>
    /// Creates the <c>purge</c> <see cref="Command"/> with options and handler.
    /// </summary>
    public static Command Create()
    {
        var cacheDirOption = new Option<string>("--cache-dir")
        {
            Description = "Cache directory to purge",
            DefaultValueFactory = _ => DefaultPathHelper.GetDefaultCacheDirectory("hfdownload")
        };

        var forceOption = new Option<bool>("--force", "-f")
        {
            Description = "Skip confirmation prompt"
        };

        var command = new Command("purge", "Delete all cached models");
        command.Add(cacheDirOption);
        command.Add(forceOption);

        command.SetAction((parseResult, _) =>
        {
            var cacheDir = parseResult.GetValue(cacheDirOption)!;
            var force = parseResult.GetValue(forceOption);

            var manager = new CacheManager();
            var models = manager.GetCachedModels(cacheDir);

            if (models.Count == 0)
            {
                AnsiConsole.MarkupLine($"[yellow]No cached models found in[/] {Markup.Escape(cacheDir)}");
                return Task.FromResult(0);
            }

            var totalFiles = models.Sum(m => m.FileCount);
            var totalSize = models.Sum(m => m.TotalSize);

            AnsiConsole.MarkupLine($"Models: [bold]{models.Count}[/]");
            AnsiConsole.MarkupLine($"Files:  [bold]{totalFiles}[/]");
            AnsiConsole.MarkupLine($"Size:   [bold]{ByteFormatHelper.FormatBytes(totalSize)}[/]");

            if (!force)
            {
                if (!AnsiConsole.Confirm("This will delete ALL cached models. Are you sure?", defaultValue: false))
                {
                    AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
                    return Task.FromResult(0);
                }
            }

            var deletedCount = manager.PurgeAll(cacheDir);
            AnsiConsole.MarkupLine(
                $"[green]Purged[/] [bold]{deletedCount}[/] model(s) ({ByteFormatHelper.FormatBytes(totalSize)})");
            return Task.FromResult(0);
        });

        return command;
    }
}
