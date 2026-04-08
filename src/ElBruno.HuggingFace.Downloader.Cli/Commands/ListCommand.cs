using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using ElBruno.HuggingFace;
using ElBruno.HuggingFace.Cli.Services;
using Spectre.Console;

namespace ElBruno.HuggingFace.Cli.Commands;

/// <summary>
/// The <c>list</c> command — displays all cached models in the cache directory.
/// </summary>
public static class ListCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Creates the <c>list</c> <see cref="Command"/> with options and handler.
    /// </summary>
    public static Command Create()
    {
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

        var command = new Command("list", "List downloaded models in the cache directory");
        command.Add(cacheDirOption);
        command.Add(formatOption);

        command.SetAction((parseResult, _) =>
        {
            var cacheDir = parseResult.GetValue(cacheDirOption)!;
            var format = parseResult.GetValue(formatOption)!;

            var manager = new CacheManager();
            var models = manager.GetCachedModels(cacheDir);

            if (models.Count == 0)
            {
                AnsiConsole.MarkupLine($"[yellow]No cached models found in[/] {Markup.Escape(cacheDir)}");
                return Task.FromResult(0);
            }

            if (format == "json")
            {
                var json = JsonSerializer.Serialize(models, JsonOptions);
                Console.WriteLine(json);
            }
            else
            {
                var table = new Table();
                table.AddColumn("Model");
                table.AddColumn(new TableColumn("Files").RightAligned());
                table.AddColumn(new TableColumn("Size").RightAligned());
                table.AddColumn("Last Modified");

                foreach (var model in models)
                {
                    table.AddRow(
                        Markup.Escape(model.Name),
                        model.FileCount.ToString(),
                        ByteFormatHelper.FormatBytes(model.TotalSize),
                        model.LastModified.ToString("yyyy-MM-dd HH:mm"));
                }

                AnsiConsole.Write(table);
            }

            return Task.FromResult(0);
        });

        return command;
    }
}
