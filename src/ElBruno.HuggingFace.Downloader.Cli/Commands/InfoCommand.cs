using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using ElBruno.HuggingFace;
using ElBruno.HuggingFace.Cli.Services;
using Spectre.Console;

namespace ElBruno.HuggingFace.Cli.Commands;

/// <summary>
/// The <c>info</c> command — shows detailed information about a specific cached model.
/// </summary>
public static class InfoCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Creates the <c>info</c> <see cref="Command"/> with arguments, options, and handler.
    /// </summary>
    public static Command Create()
    {
        var repoIdArgument = new Argument<string>("repo-id")
        {
            Description = "Repository ID of the cached model (e.g. microsoft/phi-2)"
        };

        var cacheDirOption = new Option<string>("--cache-dir")
        {
            Description = "Cache directory to scan for downloaded models",
            DefaultValueFactory = _ => DefaultPathHelper.GetDefaultCacheDirectory("hfdownload")
        };

        var formatOption = new Option<string>("--format")
        {
            Description = "Output format (table or json)",
            DefaultValueFactory = _ => "table"
        };
        formatOption.AcceptOnlyFromAmong("table", "json");

        var command = new Command("info", "Show details of a cached model");
        command.Add(repoIdArgument);
        command.Add(cacheDirOption);
        command.Add(formatOption);

        command.SetAction((parseResult, _) =>
        {
            var repoId = parseResult.GetValue(repoIdArgument)!;
            var cacheDir = parseResult.GetValue(cacheDirOption)!;
            var format = parseResult.GetValue(formatOption)!;

            var manager = new CacheManager();
            var model = manager.GetModelDetails(cacheDir, repoId);

            if (model is null)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Model [bold]{Markup.Escape(repoId)}[/] not found in {Markup.Escape(cacheDir)}");
                return Task.FromResult(1);
            }

            if (format == "json")
            {
                var json = JsonSerializer.Serialize(model, JsonOptions);
                Console.WriteLine(json);
            }
            else
            {
                AnsiConsole.MarkupLine($"[bold]Model:[/]  {Markup.Escape(model.Name)}");
                AnsiConsole.MarkupLine($"[bold]Path:[/]   {Markup.Escape(model.FullPath)}");
                AnsiConsole.MarkupLine($"[bold]Size:[/]   {ByteFormatHelper.FormatBytes(model.TotalSize)}");
                AnsiConsole.MarkupLine($"[bold]Files:[/]  {model.FileCount}");
                AnsiConsole.WriteLine();

                if (model.Files.Count > 0)
                {
                    var table = new Table();
                    table.AddColumn("File");
                    table.AddColumn(new TableColumn("Size").RightAligned());
                    table.AddColumn("Last Modified");

                    foreach (var file in model.Files)
                    {
                        table.AddRow(
                            Markup.Escape(file.RelativePath),
                            ByteFormatHelper.FormatBytes(file.Size),
                            file.LastModified.ToString("yyyy-MM-dd HH:mm"));
                    }

                    AnsiConsole.Write(table);
                }
            }

            return Task.FromResult(0);
        });

        return command;
    }
}
