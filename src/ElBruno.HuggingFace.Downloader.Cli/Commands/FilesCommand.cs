using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using ElBruno.HuggingFace;
using Spectre.Console;

namespace ElBruno.HuggingFace.Cli.Commands;

/// <summary>
/// The <c>files</c> command — lists the files available in a remote Hugging Face repository,
/// without requiring the caller to know file names in advance.
/// </summary>
internal static class FilesCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Creates a fully configured <c>files</c> <see cref="Command"/>.
    /// </summary>
    public static Command Create()
    {
        var repoIdArg = new Argument<string>("repo-id")
        {
            Description = "Hugging Face repository ID (e.g., microsoft/Phi-4-mini-instruct-onnx)"
        };

        var repoTypeOption = new Option<string>("--repo-type")
        {
            Description = "Repository type (model, dataset, or space)",
            DefaultValueFactory = _ => "model"
        };
        repoTypeOption.AcceptOnlyFromAmong("model", "dataset", "space");

        var revisionOption = new Option<string>("--revision", "-r")
        {
            Description = "Git revision — branch, tag, or commit SHA",
            DefaultValueFactory = _ => "main"
        };

        var pathOption = new Option<string?>("--path")
        {
            Description = "Optional subdirectory within the repository to list"
        };

        var tokenOption = new Option<string?>("--token", "-t")
        {
            Description = "Hugging Face auth token (overrides HF_TOKEN env var)"
        };

        var formatOption = new Option<string>("--format")
        {
            Description = "Output format (table or json)",
            DefaultValueFactory = _ => "table"
        };
        formatOption.AcceptOnlyFromAmong("table", "json");

        var command = new Command("files", "List files in a remote Hugging Face repository");
        command.Add(repoIdArg);
        command.Add(repoTypeOption);
        command.Add(revisionOption);
        command.Add(pathOption);
        command.Add(tokenOption);
        command.Add(formatOption);

        command.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            var repoId = parseResult.GetRequiredValue(repoIdArg);
            var repoTypeText = parseResult.GetValue(repoTypeOption)!;
            var revision = parseResult.GetValue(revisionOption) ?? "main";
            var path = parseResult.GetValue(pathOption);
            var token = parseResult.GetValue(tokenOption);
            var format = parseResult.GetValue(formatOption)!;

            var repoType = repoTypeText switch
            {
                "dataset" => RepoType.Dataset,
                "space" => RepoType.Space,
                _ => RepoType.Model
            };

            var options = new HuggingFaceDownloaderOptions { AuthToken = token };
            using var downloader = new HuggingFaceDownloader(options);

            try
            {
                var files = await downloader.ListRepoFilesAsync(repoId, repoType, revision, path, cancellationToken);

                if (format == "json")
                {
                    var json = JsonSerializer.Serialize(files, JsonOptions);
                    Console.WriteLine(json);
                }
                else
                {
                    if (files.Count == 0)
                    {
                        AnsiConsole.MarkupLine($"[yellow]No files found for[/] {Markup.Escape(repoId)}");
                        return 0;
                    }

                    var table = new Table();
                    table.AddColumn("Path");
                    table.AddColumn("Type");
                    table.AddColumn(new TableColumn("Size").RightAligned());

                    foreach (var file in files)
                    {
                        table.AddRow(
                            Markup.Escape(file.Path),
                            file.IsDirectory ? "dir" : "file",
                            file.SizeBytes is { } size ? ByteFormatHelper.FormatBytes(size) : "-");
                    }

                    AnsiConsole.Write(table);
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[bold]{files.Count}[/] entries in [dim]{Markup.Escape(repoId)}[/]");
                }

                return 0;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Access denied"))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
                AnsiConsole.MarkupLine("[yellow]Hint:[/] Set the [bold]HF_TOKEN[/] environment variable or use [bold]--token[/].");
                return 1;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found (404)"))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
                AnsiConsole.MarkupLine("[yellow]Hint:[/] Check the repository ID and type.");
                return 1;
            }
            catch (InvalidOperationException ex)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
                return 1;
            }
            catch (OperationCanceledException)
            {
                AnsiConsole.MarkupLine("[yellow]Listing cancelled.[/]");
                return 1;
            }
        });

        return command;
    }
}
