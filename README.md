# 📥 ElBruno.HuggingFace.Downloader

[![NuGet](https://img.shields.io/nuget/v/ElBruno.HuggingFace.Downloader.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ElBruno.HuggingFace.Downloader)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ElBruno.HuggingFace.Downloader.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ElBruno.HuggingFace.Downloader)
[![Build Status](https://github.com/elbruno/ElBruno.HuggingFace.Downloader/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/elbruno/ElBruno.HuggingFace.Downloader/actions/workflows/build-and-test.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE)
[![GitHub stars](https://img.shields.io/github/stars/elbruno/ElBruno.HuggingFace.Downloader?style=social)](https://github.com/elbruno/ElBruno.HuggingFace.Downloader)
[![X Follow](https://img.shields.io/twitter/follow/elbruno?style=social)](https://www.x.com/elbruno/)

A .NET library and CLI tool to download files (ONNX models, tokenizers, voice presets, etc.) from [Hugging Face Hub](https://huggingface.co) repositories with progress reporting, caching, and authentication support.

## Features

- 📦 **Download any file** from public or private Hugging Face repositories
- 📊 **Rich progress reporting** with stages (Checking → Downloading → Validating → Complete)
- 🔑 **HF_TOKEN authentication** for gated/private repositories (env var or explicit)
- 🔒 **Atomic writes** using temp files to avoid partial/corrupt downloads
- 🔁 **Resumable downloads** using HTTP range requests for interrupted large files
- 📌 **Revision pinning** with resolved commit metadata and optional expected commit enforcement
- ✅ **Required vs optional files** — optional files fail silently
- 🧾 **Manifest-based bundles** with SHA-256 and size validation
- 📏 **HEAD requests** to resolve total download size before starting
- ⏭️ **Skip existing files** — only downloads what's missing
- 🖥️ **Cross-platform** cache directory helpers (Windows, Linux, macOS)
- 💉 **DI-friendly** with `IServiceCollection` extension methods
- 📝 **ILogger integration** for structured logging

## 📦 NuGet Packages

| Package | Version | Downloads | Description |
|---------|---------|-----------|-------------|
| [ElBruno.HuggingFace.Downloader](https://www.nuget.org/packages/ElBruno.HuggingFace.Downloader) | [![NuGet](https://img.shields.io/nuget/v/ElBruno.HuggingFace.Downloader.svg?style=flat-square)](https://www.nuget.org/packages/ElBruno.HuggingFace.Downloader) | [![Downloads](https://img.shields.io/nuget/dt/ElBruno.HuggingFace.Downloader.svg?style=flat-square)](https://www.nuget.org/packages/ElBruno.HuggingFace.Downloader) | Core library for downloading files from Hugging Face Hub repositories |
| [ElBruno.HuggingFace.Downloader.Cli](https://www.nuget.org/packages/ElBruno.HuggingFace.Downloader.Cli) | [![NuGet](https://img.shields.io/nuget/v/ElBruno.HuggingFace.Downloader.Cli.svg?style=flat-square)](https://www.nuget.org/packages/ElBruno.HuggingFace.Downloader.Cli) | [![Downloads](https://img.shields.io/nuget/dt/ElBruno.HuggingFace.Downloader.Cli.svg?style=flat-square)](https://www.nuget.org/packages/ElBruno.HuggingFace.Downloader.Cli) | CLI tool (`hfdownload`) for managing Hugging Face downloads |

## Installation

### Library (NuGet)

```bash
dotnet add package ElBruno.HuggingFace.Downloader
```

### CLI Tool

```bash
dotnet tool install -g ElBruno.HuggingFace.Downloader.Cli
```

Once installed, use the `hfdownload` command:

```bash
# Download model files
hfdownload download sentence-transformers/all-MiniLM-L6-v2 onnx/model.onnx tokenizer.json

# Download a specific tag into a revision-aware cache directory
hfdownload download sentence-transformers/all-MiniLM-L6-v2 onnx/model.onnx --revision v1.0

# Restart from scratch instead of resuming a preserved partial download
hfdownload download sentence-transformers/all-MiniLM-L6-v2 onnx/model.onnx --no-resume

# Ensure a manifest-defined bundle with integrity validation
hfdownload download --manifest ./phi4-bundle.json -o ./models/phi4

# Check if files exist locally
hfdownload check sentence-transformers/all-MiniLM-L6-v2 onnx/model.onnx tokenizer.json

# List cached models
hfdownload list

# Delete a cached model
hfdownload delete sentence-transformers/all-MiniLM-L6-v2

# See all commands
hfdownload --help
```

See the full [CLI Reference](docs/CLI_REFERENCE.md) for all commands and options.

## Quick Start (Library)

### 1) Download model files

```csharp
using ElBruno.HuggingFace;

using var downloader = new HuggingFaceDownloader();

var request = new DownloadRequest
{
    RepoId = "sentence-transformers/all-MiniLM-L6-v2",
    LocalDirectory = "./models/miniLM",
    RequiredFiles = ["onnx/model.onnx", "tokenizer.json"],
    OptionalFiles = ["tokenizer_config.json", "vocab.txt"],
    Revision = "refs/pr/42"
};

await downloader.DownloadFilesAsync(request);

Console.WriteLine($"Resolved commit: {request.ResolvedCommitSha}");
```

### 2) Ensure a manifest-based bundle with SHA-256 validation

```csharp
var manifest = new ModelBundleManifest
{
    RepoId = "microsoft/Phi-4-mini-instruct-onnx",
    Revision = "main",
    Files =
    [
        new ModelBundleFile
        {
            Path = "onnx/model.onnx",
            Size = 123456789,
            Sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
        },
        new ModelBundleFile
        {
            Path = "tokenizer.json",
            Required = false
        }
    ]
};

var bundle = await downloader.EnsureBundleAsync(manifest, "./models/phi4");
Console.WriteLine(bundle.ResolvedManifestPath);
```

Manifest-driven bundle downloads validate existing files before reuse, re-download corrupted files, and write a resolved manifest to `./models/phi4/.hf.bundle.resolved.json`.

### 3) Pin a branch or tag to the expected commit

```csharp
var request = new DownloadRequest
{
    RepoId = "my-org/my-model",
    LocalDirectory = "./models/pinned",
    RequiredFiles = ["model.onnx"],
    Revision = "main",
    ExpectedCommitSha = "1234567890abcdef1234567890abcdef12345678"
};

await downloader.DownloadFilesAsync(request);
Console.WriteLine(request.ResolvedCommitSha);
```

### 4) Check if files are already downloaded

```csharp
bool ready = downloader.AreFilesAvailable(
    ["onnx/model.onnx", "tokenizer.json"],
    "./models/miniLM");

if (!ready)
{
    var missing = downloader.GetMissingFiles(
        ["onnx/model.onnx", "tokenizer.json"],
        "./models/miniLM");
    Console.WriteLine($"Missing {missing.Count} files");
}
```

### 5) Track download progress

```csharp
var progress = new Progress<DownloadProgress>(p =>
{
    if (p.Stage == DownloadStage.Downloading)
        Console.Write($"\r⬇️ [{p.CurrentFile}] {p.PercentComplete:F0}% (reused {ByteFormatHelper.FormatBytes(p.ResumedBytes)})");
    else
        Console.WriteLine($"{p.Stage}: {p.Message}");
});

await downloader.DownloadFilesAsync(new DownloadRequest
{
    RepoId = "sentence-transformers/all-MiniLM-L6-v2",
    LocalDirectory = "./models/miniLM",
    RequiredFiles = ["onnx/model.onnx", "tokenizer.json"],
    Progress = progress
});
```

### 6) Authentication (Private/Gated Repos)

Set the `HF_TOKEN` environment variable, or pass it explicitly:

```csharp
var downloader = new HuggingFaceDownloader(new HuggingFaceDownloaderOptions
{
    AuthToken = "hf_your_token_here"
});
```

### 7) Dependency Injection

```csharp
builder.Services.AddHuggingFaceDownloader(options =>
{
    options.Timeout = TimeSpan.FromMinutes(60);
});

// Then inject HuggingFaceDownloader in your services
public class MyModelService(HuggingFaceDownloader downloader)
{
    public async Task EnsureModelAsync()
    {
        await downloader.DownloadFilesAsync(new DownloadRequest
        {
            RepoId = "my-org/my-model",
            LocalDirectory = DefaultPathHelper.GetDefaultCacheDirectory("MyApp"),
            RequiredFiles = ["model.onnx", "tokenizer.json"]
        });
    }
}
```

### 8) Manage local cache programmatically

```csharp
var cacheRoot = DefaultPathHelper.GetDefaultCacheDirectory("hfdownload");
var repoId = "sentence-transformers/all-MiniLM-L6-v2";

// List all cached repositories
var cachedRepos = downloader.ListCachedRepos(cacheRoot);

// Check whether required files are cached for one repo
bool isCached = downloader.IsCached(
    repoId,
    cacheRoot,
    ["onnx/model.onnx", "tokenizer.json"]);

// Get total cache size for one local repo directory
long size = downloader.GetCachedSize(
    Path.Combine(cacheRoot, DefaultPathHelper.SanitizeModelName(repoId)));

// Delete cached files for the repo (safe no-op if not found)
await downloader.DeleteCachedFilesAsync(repoId, cacheRoot);
```

Direct downloads also write `./models/.../.hf.download.resolved.json` so callers can inspect the requested revision and resolved commit later.

## Documentation

| Topic | Description |
|-------|-------------|
| [Getting Started](docs/GETTING_STARTED.md) | Installation, all usage examples, and setup |
| [CLI Reference](docs/CLI_REFERENCE.md) | Complete CLI command reference |
| [API Reference](docs/API_REFERENCE.md) | Complete class and method documentation |
| [Architecture](docs/ARCHITECTURE.md) | Design decisions, data flow, and project structure |
| [Publishing](docs/publishing.md) | NuGet publishing with GitHub Actions |

## Related Projects

- **[ElBruno.PersonaPlex](https://github.com/elbruno/ElBruno.PersonaPlex)** — Integrates this downloader to auto-download ONNX models for NVIDIA's PersonaPlex-7B-v1 full-duplex speech-to-speech model

## Building from Source

```bash
git clone https://github.com/elbruno/ElBruno.HuggingFace.Downloader.git
cd ElBruno.HuggingFace.Downloader
dotnet build ElBruno.HuggingFace.Downloader.slnx
dotnet test ElBruno.HuggingFace.Downloader.slnx
```

### Requirements

- .NET 8.0 SDK or later

## 🤝 Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

## 👋 About the Author

Hi! I'm **ElBruno** 🧡, a passionate developer and content creator exploring AI, .NET, and modern development practices.

**Made with ❤️ by [ElBruno](https://github.com/elbruno)**

If you like this project, consider following my work across platforms:

- 📻 **Podcast**: [No Tienen Nombre](https://notienenombre.com) — Spanish-language episodes on AI, development, and tech culture
- 💻 **Blog**: [ElBruno.com](https://elbruno.com) — Deep dives on embeddings, RAG, .NET, and local AI
- 📺 **YouTube**: [youtube.com/elbruno](https://www.youtube.com/elbruno) — Demos, tutorials, and live coding
- 🔗 **LinkedIn**: [@elbruno](https://www.linkedin.com/in/elbruno/) — Professional updates and insights
- 𝕏 **X**: [@elbruno](https://www.x.com/elbruno/) — Quick tips, releases, and tech news
