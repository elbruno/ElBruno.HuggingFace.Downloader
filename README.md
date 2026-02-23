# ElBruno.HuggingFace.Downloader

A .NET library to download files (ONNX models, tokenizers, voice presets, etc.) from [Hugging Face Hub](https://huggingface.co) repositories with progress reporting, caching, and authentication support.

## Features

- **Download any file** from public or private Hugging Face repositories
- **Rich progress reporting** with stages (Checking → Downloading → Validating → Complete)
- **HF_TOKEN authentication** for gated/private repositories (env var or explicit)
- **Atomic writes** using temp files to avoid partial/corrupt downloads
- **Required vs optional files** — optional files fail silently
- **HEAD requests** to resolve total download size before starting
- **Skip existing files** — only downloads what's missing
- **Cross-platform** cache directory helpers (Windows, Linux, macOS)
- **DI-friendly** with `IServiceCollection` extension methods
- **ILogger integration** for structured logging

## Installation

```bash
dotnet add package ElBruno.HuggingFace.Downloader
```

## Quick Start

```csharp
using ElBruno.HuggingFace;

var downloader = new HuggingFaceDownloader();

await downloader.DownloadFilesAsync(new DownloadRequest
{
    RepoId = "sentence-transformers/all-MiniLM-L6-v2",
    LocalDirectory = "./models/miniLM",
    RequiredFiles = ["onnx/model.onnx", "tokenizer.json"],
    OptionalFiles = ["tokenizer_config.json", "vocab.txt"],
    Progress = new Progress<DownloadProgress>(p =>
        Console.WriteLine($"[{p.Stage}] {p.PercentComplete:F0}% — {p.Message}"))
});
```

## Authentication (Private/Gated Repos)

Set the `HF_TOKEN` environment variable, or pass it explicitly:

```csharp
var downloader = new HuggingFaceDownloader(new HuggingFaceDownloaderOptions
{
    AuthToken = "hf_your_token_here"
});
```

## Dependency Injection

```csharp
builder.Services.AddHuggingFaceDownloader(options =>
{
    options.Timeout = TimeSpan.FromMinutes(60);
});
```

## License

MIT
