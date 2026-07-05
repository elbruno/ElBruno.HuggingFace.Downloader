using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using ElBruno.HuggingFace;
using ElBruno.HuggingFace.Cli.Services;
using Spectre.Console;

namespace ElBruno.HuggingFace.Cli.Commands;

/// <summary>
/// Defines the <c>download</c> command for fetching files from a Hugging Face repository.
/// </summary>
internal static class DownloadCommand
{
    /// <summary>
    /// Creates a fully configured <c>download</c> <see cref="Command"/>.
    /// </summary>
    public static Command Create()
    {
        var repoIdArg = new Argument<string?>("repo-id")
        {
            Description = "Hugging Face repository ID (e.g., microsoft/Phi-4-mini-instruct-onnx)",
            Arity = ArgumentArity.ZeroOrOne
        };

        var filesArg = new Argument<string[]>("files")
        {
            Description = "Files to download, relative to the repo root",
            Arity = ArgumentArity.ZeroOrMore
        };

        var manifestOption = new Option<string?>("--manifest", "-m")
        {
            Description = "Path to a model bundle manifest JSON file"
        };

        var outputOption = new Option<string?>("--output", "-o")
        {
            Description = "Local directory for downloaded files"
        };

        var revisionOption = new Option<string>("--revision", "-r")
        {
            Description = "Git revision — branch, tag, or commit SHA",
            DefaultValueFactory = _ => "main"
        };

        var tokenOption = new Option<string?>("--token", "-t")
        {
            Description = "Hugging Face auth token (overrides HF_TOKEN env var)"
        };

        var optionalFlag = new Option<bool>("--optional")
        {
            Description = "Treat listed files as optional (skip failures instead of aborting)"
        };

        var noProgressFlag = new Option<bool>("--no-progress")
        {
            Description = "Suppress progress bar output"
        };

        var noResumeFlag = new Option<bool>("--no-resume")
        {
            Description = "Disable resuming preserved partial downloads"
        };

        var quietFlag = new Option<bool>("--quiet", "-q")
        {
            Description = "Minimal output (only errors)"
        };

        var command = new Command("download", "Download files from a Hugging Face repository");
        command.Add(repoIdArg);
        command.Add(filesArg);
        command.Add(manifestOption);
        command.Add(outputOption);
        command.Add(revisionOption);
        command.Add(tokenOption);
        command.Add(optionalFlag);
        command.Add(noProgressFlag);
        command.Add(noResumeFlag);
        command.Add(quietFlag);

        command.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            var repoId = parseResult.GetValue(repoIdArg);
            var files = parseResult.GetValue(filesArg) ?? [];
            var manifestPath = parseResult.GetValue(manifestOption);
            var output = parseResult.GetValue(outputOption);
            var revision = parseResult.GetValue(revisionOption) ?? "main";
            var token = parseResult.GetValue(tokenOption);
            var isOptional = parseResult.GetValue(optionalFlag);
            var noProgress = parseResult.GetValue(noProgressFlag);
            var noResume = parseResult.GetValue(noResumeFlag);
            var quiet = parseResult.GetValue(quietFlag);

            var options = new HuggingFaceDownloaderOptions
            {
                AuthToken = token,
                ResolveFileSizesBeforeDownload = true
            };

            using var downloader = new HuggingFaceDownloader(options);

            ModelBundleManifest? manifest = null;
            if (!string.IsNullOrWhiteSpace(manifestPath))
            {
                if (!string.IsNullOrWhiteSpace(repoId) || files.Length > 0)
                {
                    AnsiConsole.MarkupLine("[red]Error:[/] Use either [bold]--manifest[/] or positional repo/file arguments, not both.");
                    return 1;
                }

                try
                {
                    manifest = await ModelBundleManifestJson.LoadAsync(manifestPath, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException or ArgumentException)
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] Failed to load manifest: {Markup.Escape(ex.Message)}");
                    return 1;
                }

                repoId = manifest.RepoId;
                files = manifest.Files.Select(file => file.Path).ToArray();
            }
            else
            {
                if (string.IsNullOrWhiteSpace(repoId))
                {
                    AnsiConsole.MarkupLine("[red]Error:[/] A repository ID is required unless [bold]--manifest[/] is used.");
                    return 1;
                }

                if (files.Length == 0)
                {
                    AnsiConsole.MarkupLine("[red]Error:[/] At least one file must be specified.");
                    AnsiConsole.MarkupLine("[dim]Usage: hfdownload download <repo-id> file1 [[file2 ...]] or hfdownload download --manifest bundle.json[/]");
                    return 1;
                }
            }

            var localDir = output
                ?? await ResolveDefaultOutputDirectoryAsync(
                    downloader,
                    manifest,
                    repoId!,
                    files,
                    revision,
                    cancellationToken).ConfigureAwait(false);

            IReadOnlyList<string> requiredFiles = isOptional ? Array.Empty<string>() : files;
            IReadOnlyList<string>? optionalFiles = isOptional ? files : null;

            var stopwatch = Stopwatch.StartNew();

            try
            {
                if (manifest is not null)
                {
                    if (quiet)
                    {
                        await RunBundleSilentAsync(downloader, manifest, localDir, cancellationToken);
                    }
                    else if (noProgress)
                    {
                        await RunBundleTextOnlyAsync(downloader, manifest, localDir, cancellationToken);
                    }
                    else
                    {
                        await RunBundleWithProgressAsync(downloader, manifest, localDir, cancellationToken);
                    }
                }
                else if (quiet)
                {
                    await RunSilentAsync(downloader, repoId!, localDir, requiredFiles, optionalFiles, revision, noResume, cancellationToken);
                }
                else if (noProgress)
                {
                    await RunTextOnlyAsync(downloader, repoId!, localDir, requiredFiles, optionalFiles, revision, noResume, cancellationToken);
                }
                else
                {
                    await RunWithProgressAsync(downloader, repoId!, localDir, requiredFiles, optionalFiles, revision, noResume, cancellationToken);
                }

                stopwatch.Stop();

                if (!quiet)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[green]✓[/] Download complete for [bold]{Markup.Escape(repoId!)}[/]");
                    AnsiConsole.MarkupLine($"  [dim]Files:[/]  {files.Length}");
                    AnsiConsole.MarkupLine($"  [dim]Dir:[/]    {Markup.Escape(localDir)}");
                    AnsiConsole.MarkupLine($"  [dim]Time:[/]   {stopwatch.Elapsed:m\\:ss\\.ff}");
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
                AnsiConsole.MarkupLine("[yellow]Hint:[/] Check the repository ID and file names.");
                return 1;
            }
            catch (InvalidOperationException ex)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
                return 1;
            }
            catch (OperationCanceledException)
            {
                AnsiConsole.MarkupLine("[yellow]Download cancelled.[/]");
                return 1;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
                return 1;
            }
        });

        return command;
    }

    private static async Task RunSilentAsync(
        HuggingFaceDownloader downloader, string repoId, string localDir,
        IReadOnlyList<string> requiredFiles, IReadOnlyList<string>? optionalFiles,
        string revision, bool noResume, CancellationToken ct)
    {
        var request = new DownloadRequest
        {
            RepoId = repoId,
            LocalDirectory = localDir,
            RequiredFiles = requiredFiles,
            OptionalFiles = optionalFiles,
            Revision = revision,
            ResumePartialDownloads = !noResume
        };

        await downloader.DownloadFilesAsync(request, ct);
    }

    private static async Task RunTextOnlyAsync(
        HuggingFaceDownloader downloader, string repoId, string localDir,
        IReadOnlyList<string> requiredFiles, IReadOnlyList<string>? optionalFiles,
        string revision, bool noResume, CancellationToken ct)
    {
        var progress = new Progress<DownloadProgress>(p =>
        {
            if (p.Message is not null)
                AnsiConsole.MarkupLine($"[grey]{Markup.Escape(p.Message)}[/]");
        });

        var request = new DownloadRequest
        {
            RepoId = repoId,
            LocalDirectory = localDir,
            RequiredFiles = requiredFiles,
            OptionalFiles = optionalFiles,
            Revision = revision,
            Progress = progress,
            ResumePartialDownloads = !noResume
        };

        await downloader.DownloadFilesAsync(request, ct);
    }

    private static async Task RunWithProgressAsync(
        HuggingFaceDownloader downloader, string repoId, string localDir,
        IReadOnlyList<string> requiredFiles, IReadOnlyList<string>? optionalFiles,
        string revision, bool noResume, CancellationToken ct)
    {
        await AnsiConsole.Progress()
            .AutoRefresh(true)
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var overallTask = ctx.AddTask($"[bold]{Markup.Escape(repoId)}[/]", maxValue: 100);
                var fileTask = ctx.AddTask("Preparing...", maxValue: 100);

                var progress = new Progress<DownloadProgress>(p =>
                {
                    overallTask.Value = Math.Min(p.PercentComplete, 100);

                    switch (p.Stage)
                    {
                        case DownloadStage.Checking:
                            fileTask.Description = Markup.Escape(p.Message ?? "Checking...");
                            break;

                        case DownloadStage.Downloading when p.CurrentFile is not null:
                            fileTask.Description = $"[dim]{Markup.Escape(p.CurrentFile)}[/]";
                            fileTask.Value = p.TotalBytes > 0
                                ? Math.Min(p.PercentComplete, 100)
                                : 0;
                            break;

                        case DownloadStage.Validating:
                            fileTask.Description = "Validating...";
                            fileTask.Value = 99;
                            break;

                        case DownloadStage.Complete:
                            overallTask.Value = 100;
                            fileTask.Value = 100;
                            fileTask.Description = "[green]Done[/]";
                            break;
                    }
                });

                var request = new DownloadRequest
                {
                    RepoId = repoId,
                    LocalDirectory = localDir,
                    RequiredFiles = requiredFiles,
                    OptionalFiles = optionalFiles,
                    Revision = revision,
                    Progress = progress,
                    ResumePartialDownloads = !noResume
                };

                await downloader.DownloadFilesAsync(request, ct);
            });
    }

    private static async Task RunBundleSilentAsync(
        HuggingFaceDownloader downloader,
        ModelBundleManifest manifest,
        string localDir,
        CancellationToken ct)
    {
        await downloader.EnsureBundleAsync(manifest, localDir, cancellationToken: ct);
    }

    private static async Task RunBundleTextOnlyAsync(
        HuggingFaceDownloader downloader,
        ModelBundleManifest manifest,
        string localDir,
        CancellationToken ct)
    {
        var progress = new Progress<DownloadProgress>(p =>
        {
            if (p.Message is not null)
                AnsiConsole.MarkupLine($"[grey]{Markup.Escape(p.Message)}[/]");
        });

        await downloader.EnsureBundleAsync(manifest, localDir, progress, ct);
    }

    private static async Task RunBundleWithProgressAsync(
        HuggingFaceDownloader downloader,
        ModelBundleManifest manifest,
        string localDir,
        CancellationToken ct)
    {
        await AnsiConsole.Progress()
            .AutoRefresh(true)
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var overallTask = ctx.AddTask($"[bold]{Markup.Escape(manifest.RepoId)}[/]", maxValue: 100);
                var fileTask = ctx.AddTask("Preparing manifest...", maxValue: 100);

                var progress = new Progress<DownloadProgress>(p =>
                {
                    overallTask.Value = Math.Min(p.PercentComplete, 100);

                    switch (p.Stage)
                    {
                        case DownloadStage.Checking:
                            fileTask.Description = Markup.Escape(p.Message ?? "Checking manifest...");
                            break;

                        case DownloadStage.Downloading when p.CurrentFile is not null:
                            fileTask.Description = $"[dim]{Markup.Escape(p.CurrentFile)}[/]";
                            fileTask.Value = p.TotalBytes > 0
                                ? Math.Min(p.PercentComplete, 100)
                                : 0;
                            break;

                        case DownloadStage.Validating:
                            fileTask.Description = "Validating bundle...";
                            fileTask.Value = 99;
                            break;

                        case DownloadStage.Complete:
                            overallTask.Value = 100;
                            fileTask.Value = 100;
                            fileTask.Description = "[green]Done[/]";
                            break;
                    }
                });

                await downloader.EnsureBundleAsync(manifest, localDir, progress, ct);
            });
    }

    private static async Task<string> ResolveDefaultOutputDirectoryAsync(
        HuggingFaceDownloader downloader,
        ModelBundleManifest? manifest,
        string repoId,
        IReadOnlyList<string> files,
        string revision,
        CancellationToken cancellationToken)
    {
        var cacheRoot = DefaultPathHelper.GetDefaultCacheDirectory("hfdownload");
        var (filePath, requestedRevision) = ResolveCacheKeyInput(manifest, files, revision);
        var resolvedCommitSha = await downloader.ResolveCommitShaAsync(
            repoId,
            filePath,
            requestedRevision,
            cancellationToken).ConfigureAwait(false);

        return CacheManager.GetDefaultModelDirectory(
            cacheRoot,
            repoId,
            resolvedCommitSha ?? requestedRevision);
    }

    private static (string filePath, string requestedRevision) ResolveCacheKeyInput(
        ModelBundleManifest? manifest,
        IReadOnlyList<string> files,
        string revision)
    {
        if (manifest is null)
            return (files[0], revision);

        var bundleFile = manifest.Files.FirstOrDefault(file => file.Required)
            ?? manifest.Files.First();
        return (bundleFile.Path, bundleFile.Revision ?? manifest.Revision);
    }
}
