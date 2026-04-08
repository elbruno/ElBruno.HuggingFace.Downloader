using System.CommandLine;
using ElBruno.HuggingFace;
using ElBruno.HuggingFace.Cli.Services;
using Spectre.Console;

namespace ElBruno.HuggingFace.Cli.Commands;

/// <summary>
/// The <c>delete-file</c> command — deletes a single file from a cached model.
/// </summary>
public static class DeleteFileCommand
{
    /// <summary>
    /// Creates the <c>delete-file</c> <see cref="Command"/> with arguments, options, and handler.
    /// </summary>
    public static Command Create()
    {
        var repoIdArgument = new Argument<string>("repo-id")
        {
            Description = "Repository ID of the cached model"
        };

        var fileArgument = new Argument<string>("file")
        {
            Description = "Relative path of the file to delete within the model directory"
        };

        var cacheDirOption = new Option<string>("--cache-dir")
        {
            Description = "Cache directory containing downloaded models",
            DefaultValueFactory = _ => DefaultPathHelper.GetDefaultCacheDirectory("hfdownload")
        };

        var forceOption = new Option<bool>("--force", "-f")
        {
            Description = "Skip confirmation prompt"
        };

        var command = new Command("delete-file", "Delete a single file from a cached model");
        command.Add(repoIdArgument);
        command.Add(fileArgument);
        command.Add(cacheDirOption);
        command.Add(forceOption);

        command.SetAction((parseResult, _) =>
        {
            var repoId = parseResult.GetValue(repoIdArgument)!;
            var filePath = parseResult.GetValue(fileArgument)!;
            var cacheDir = parseResult.GetValue(cacheDirOption)!;
            var force = parseResult.GetValue(forceOption);

            var manager = new CacheManager();
            var model = manager.GetModelDetails(cacheDir, repoId);

            if (model is null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Model [bold]{Markup.Escape(repoId)}[/] not found in {Markup.Escape(cacheDir)}");
                return Task.FromResult(1);
            }

            // Check if file exists within the model
            var matchingFile = model.Files.FirstOrDefault(
                f => string.Equals(f.RelativePath, filePath, StringComparison.OrdinalIgnoreCase));

            if (matchingFile is null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] File [bold]{Markup.Escape(filePath)}[/] not found in model {Markup.Escape(model.Name)}");
                return Task.FromResult(1);
            }

            AnsiConsole.MarkupLine($"Model:  [bold]{Markup.Escape(model.Name)}[/]");
            AnsiConsole.MarkupLine($"File:   {Markup.Escape(matchingFile.RelativePath)}");
            AnsiConsole.MarkupLine($"Size:   {ByteFormatHelper.FormatBytes(matchingFile.Size)}");

            if (!force)
            {
                if (!AnsiConsole.Confirm("Are you sure you want to delete this file?", defaultValue: false))
                {
                    AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
                    return Task.FromResult(0);
                }
            }

            var deleted = manager.DeleteFile(cacheDir, repoId, filePath);
            if (deleted)
            {
                AnsiConsole.MarkupLine(
                    $"[green]Deleted file[/] [bold]{Markup.Escape(matchingFile.RelativePath)}[/] " +
                    $"from {Markup.Escape(model.Name)} ({ByteFormatHelper.FormatBytes(matchingFile.Size)})");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Failed to delete the file.");
                return Task.FromResult(1);
            }

            return Task.FromResult(0);
        });

        return command;
    }
}
