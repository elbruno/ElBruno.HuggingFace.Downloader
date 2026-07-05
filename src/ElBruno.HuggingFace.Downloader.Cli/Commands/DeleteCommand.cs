using System.CommandLine;
using ElBruno.HuggingFace;
using ElBruno.HuggingFace.Cli.Services;
using Spectre.Console;

namespace ElBruno.HuggingFace.Cli.Commands;

/// <summary>
/// The <c>delete</c> command — deletes a cached model and all its files.
/// </summary>
public static class DeleteCommand
{
    /// <summary>
    /// Creates the <c>delete</c> <see cref="Command"/> with arguments, options, and handler.
    /// </summary>
    public static Command Create()
    {
        var repoIdArgument = new Argument<string>("repo-id")
        {
            Description = "Repository ID of the cached model to delete"
        };

        var cacheDirOption = new Option<string>("--cache-dir")
        {
            Description = "Cache directory containing downloaded models",
            DefaultValueFactory = _ => DefaultPathHelper.GetDefaultCacheDirectory("hfdownload")
        };

        var revisionOption = new Option<string?>("--revision", "-r")
        {
            Description = "Requested revision or resolved commit SHA to delete when multiple cached revisions exist"
        };

        var forceOption = new Option<bool>("--force", "-f")
        {
            Description = "Skip confirmation prompt"
        };

        var command = new Command("delete", "Delete a cached model and all its files");
        command.Add(repoIdArgument);
        command.Add(cacheDirOption);
        command.Add(revisionOption);
        command.Add(forceOption);

        command.SetAction((parseResult, _) =>
        {
            var repoId = parseResult.GetValue(repoIdArgument)!;
            var cacheDir = parseResult.GetValue(cacheDirOption)!;
            var revision = parseResult.GetValue(revisionOption);
            var force = parseResult.GetValue(forceOption);

            var manager = new CacheManager();
            var model = manager.GetModelDetails(cacheDir, repoId, revision);

            if (model is null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Model [bold]{Markup.Escape(repoId)}[/] not found in {Markup.Escape(cacheDir)}");
                return Task.FromResult(1);
            }

            AnsiConsole.MarkupLine($"Repo:   [bold]{Markup.Escape(model.RepoId ?? repoId)}[/]");
            if (!string.IsNullOrWhiteSpace(model.RequestedRevision))
                AnsiConsole.MarkupLine($"Ref:    {Markup.Escape(model.RequestedRevision)}");
            if (!string.IsNullOrWhiteSpace(model.ResolvedCommitSha))
                AnsiConsole.MarkupLine($"Commit: {Markup.Escape(model.ResolvedCommitSha)}");
            AnsiConsole.MarkupLine($"Cache:  {Markup.Escape(model.Name)}");
            AnsiConsole.MarkupLine($"Files:  {model.FileCount}");
            AnsiConsole.MarkupLine($"Size:   {ByteFormatHelper.FormatBytes(model.TotalSize)}");

            if (!force)
            {
                if (!AnsiConsole.Confirm("Are you sure you want to delete this model?", defaultValue: false))
                {
                    AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
                    return Task.FromResult(0);
                }
            }

            manager.DeleteModel(cacheDir, repoId, revision);
            AnsiConsole.MarkupLine($"[green]Deleted model[/] [bold]{Markup.Escape(model.Name)}[/] ({ByteFormatHelper.FormatBytes(model.TotalSize)})");
            return Task.FromResult(0);
        });

        return command;
    }
}
