using System.CommandLine;
using ElBruno.HuggingFace;
using Spectre.Console;

namespace ElBruno.HuggingFace.Cli.Commands;

/// <summary>
/// Defines the <c>check</c> command for verifying that files exist in the local cache.
/// </summary>
internal static class CheckCommand
{
    /// <summary>
    /// Creates a fully configured <c>check</c> <see cref="Command"/>.
    /// </summary>
    public static Command Create()
    {
        var repoIdArg = new Argument<string>("repo-id")
        {
            Description = "Hugging Face repository ID (e.g., microsoft/Phi-4-mini-instruct-onnx)"
        };

        var filesArg = new Argument<string[]>("files")
        {
            Description = "Files to check, relative to the repo root",
            Arity = ArgumentArity.OneOrMore
        };

        var outputOption = new Option<string?>("--output", "-o")
        {
            Description = "Local directory to check (defaults to the hfdownload cache)"
        };

        var revisionOption = new Option<string>("--revision", "-r")
        {
            Description = "Git revision (kept for consistency with download command)",
            DefaultValueFactory = _ => "main"
        };

        var command = new Command("check", "Check if files exist in the local cache");
        command.Add(repoIdArg);
        command.Add(filesArg);
        command.Add(outputOption);
        command.Add(revisionOption);

        command.SetAction((ParseResult parseResult) =>
        {
            var repoId = parseResult.GetRequiredValue(repoIdArg);
            var files = parseResult.GetValue(filesArg) ?? [];
            var output = parseResult.GetValue(outputOption);

            var localDir = output
                ?? Path.Combine(
                    DefaultPathHelper.GetDefaultCacheDirectory("hfdownload"),
                    DefaultPathHelper.SanitizeModelName(repoId));

            int present = 0;
            int missing = 0;

            foreach (var file in files)
            {
                var localPath = Path.Combine(localDir, file.Replace('/', Path.DirectorySeparatorChar));

                if (File.Exists(localPath))
                {
                    AnsiConsole.MarkupLine($"  [green]✅[/] {Markup.Escape(file)}");
                    present++;
                }
                else
                {
                    AnsiConsole.MarkupLine($"  [red]❌[/] {Markup.Escape(file)}");
                    missing++;
                }
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold]{present}[/] of [bold]{files.Length}[/] files present in [dim]{Markup.Escape(localDir)}[/]");

            return missing == 0 ? 0 : 1;
        });

        return command;
    }
}
