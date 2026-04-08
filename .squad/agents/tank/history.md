# Tank — History

## Project Context
**Project:** ElBruno.HuggingFace.Downloader
**Stack:** C# 12+, .NET 8/10, NuGet package
**Author:** Bruno Capuano
**Description:** A .NET library for downloading files from Hugging Face Hub repositories with progress reporting, caching, atomic writes, and HF_TOKEN authentication support.

## Learnings
- System.CommandLine 2.0.0-beta5 uses `command.Add(sub)`, `command.SetAction(action)`, and `new CommandLineConfiguration(root).InvokeAsync(args)` — API broke from beta4's `AddCommand`/`SetHandler`/`InvokeAsync`.
- CLI project lives at `src/ElBruno.HuggingFace.Downloader.Cli/` with namespace `ElBruno.HuggingFace.Cli`.
- CLI tool command name: `hfdownload`. PackageId: `ElBruno.HuggingFace.Downloader.Cli`.
- Library targets `net8.0;net10.0` (not net9.0). CLI matches those TFMs.
- Spectre.Console 0.50.0 is included for future console UX (progress bars, tables, colors).
- The .slnx format uses `<Folder Name="/src/">` grouping for solution folders.
- Pack output goes to `src/ElBruno.HuggingFace.Downloader.Cli/nupkg/`.
- Multi-TFM `dotnet run` prompts for framework selection interactively; use `--framework net8.0` to avoid.
- Subcommands: download, check, list, info, delete, delete-file, purge, config — all stubbed with "Not yet implemented" messages.
