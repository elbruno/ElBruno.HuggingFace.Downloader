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
- Phase 3 cache commands (list, info, delete, delete-file, purge) implemented with CacheManager service and Spectre.Console table output.
- Phase 2 download & check commands fully implemented. download uses Spectre.Console Progress() for live progress bars; check uses MarkupLine with ✅/❌ emoji output.
- System.CommandLine beta5 constructors: `Argument<T>(string name)` and `Option<T>(string name, params string[] aliases)`. Description and DefaultValueFactory set via object initializer properties, NOT constructor params.
- Spectre.Console `MarkupLine` interprets `[...]` as markup tags. Literal brackets must be escaped as `[[` and `]]`. User strings must go through `Markup.Escape()`.
- SetAction overload `Func<ParseResult, CancellationToken, Task<int>>` enables async handlers with exit code returns. Synchronous variant `Func<ParseResult, int>` works for non-async commands like check.
- Cache convention: each immediate subdirectory of the cache root = one model (dir name = sanitized repo ID via DefaultPathHelper.SanitizeModelName).
- CacheManager.ResolveModelPath tries sanitized name first, then falls back to direct name match. Includes path traversal protection on file deletes.
- System.CommandLine beta5 SetAction with `(ParseResult, CancellationToken) => Task<int>` supports exit code propagation; returning `Task.FromResult(1)` sets non-zero exit.
- `ElBruno.HuggingFace` types (DefaultPathHelper, ByteFormatHelper) are accessible from child namespaces without explicit `using` due to C# namespace resolution, but explicit usings are harmless.
- For Spectre.Console interactive prompts (AnsiConsole.Confirm), provide `--force`/`-f` flag to skip in non-interactive/CI contexts.
- All command classes are `static` with a `Create()` factory method that returns a fully-wired `Command` instance.
