# Getting Started

## Prerequisites

- .NET 8.0 SDK or later
- A Hugging Face repository with files to download (public or private)

## Installation

```bash
dotnet add package ElBruno.HuggingFace.Downloader
```

## Quick Start

### 1) Download model files

```csharp
using ElBruno.HuggingFace;

using var downloader = new HuggingFaceDownloader();

await downloader.DownloadFilesAsync(new DownloadRequest
{
    RepoId = "sentence-transformers/all-MiniLM-L6-v2",
    LocalDirectory = "./models/miniLM",
    RequiredFiles = ["onnx/model.onnx", "tokenizer.json"],
    OptionalFiles = ["tokenizer_config.json", "vocab.txt"],
    Revision = "refs/pr/42"
});
```

The downloader records the resolved commit in `./models/miniLM/.hf.download.resolved.json`.

### 2) Pin a moving ref to an expected commit

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

### 3) Check if files are already downloaded

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

### 4) Track download progress

```csharp
var progress = new Progress<DownloadProgress>(p =>
{
    switch (p.Stage)
    {
        case DownloadStage.Checking:
            Console.WriteLine($"🔍 {p.Message}");
            break;
        case DownloadStage.Downloading:
            Console.Write($"\r⬇️ [{p.CurrentFileIndex}/{p.TotalFileCount}] {p.CurrentFile} — {p.PercentComplete:F0}% (reused {ByteFormatHelper.FormatBytes(p.ResumedBytes)})");
            break;
        case DownloadStage.Validating:
            Console.WriteLine($"\n✅ {p.Message}");
            break;
        case DownloadStage.Complete:
            Console.WriteLine($"🎉 {p.Message}");
            break;
    }
});

await downloader.DownloadFilesAsync(new DownloadRequest
{
    RepoId = "sentence-transformers/all-MiniLM-L6-v2",
    LocalDirectory = "./models/miniLM",
    RequiredFiles = ["onnx/model.onnx", "tokenizer.json"],
    Progress = progress
});
```

### 5) Authenticate for private/gated repositories

```csharp
// Option A: Set the HF_TOKEN environment variable (recommended)
// export HF_TOKEN=hf_your_token_here

// Option B: Pass the token explicitly
var downloader = new HuggingFaceDownloader(new HuggingFaceDownloaderOptions
{
    AuthToken = "hf_your_token_here"
});
```

### 6) Use with Dependency Injection

```csharp
// In Program.cs or Startup.cs
builder.Services.AddHuggingFaceDownloader(options =>
{
    options.Timeout = TimeSpan.FromMinutes(60);
    options.ResolveFileSizesBeforeDownload = true;
});

// In your service
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

### 7) Download from a specific branch, tag, or commit

```csharp
await downloader.DownloadFilesAsync(new DownloadRequest
{
    RepoId = "my-org/my-model",
    LocalDirectory = "./models",
    RequiredFiles = ["model.onnx"],
    Revision = "release/v2"  // branch, tag, or commit SHA
});
```

### 8) Use platform-specific cache directories

```csharp
// Returns OS-appropriate cache path:
// Windows: %LOCALAPPDATA%/MyApp/models
// Linux/macOS: ~/.local/share/MyApp/models
string cacheDir = DefaultPathHelper.GetDefaultCacheDirectory("MyApp");

// Sanitize model names for use as directory names
string safeName = DefaultPathHelper.SanitizeModelName("org/model-name");
// → "org_model-name"
```

### 9) Resume interrupted downloads by default

```csharp
await downloader.DownloadFilesAsync(new DownloadRequest
{
    RepoId = "my-org/my-model",
    LocalDirectory = "./models",
    RequiredFiles = ["model.onnx"]
    // ResumePartialDownloads defaults to true when atomic writes are enabled.
});
```

Disable resume when you explicitly want a clean restart:

```csharp
await downloader.DownloadFilesAsync(new DownloadRequest
{
    RepoId = "my-org/my-model",
    LocalDirectory = "./models",
    RequiredFiles = ["model.onnx"],
    ResumePartialDownloads = false
});
```

### 10) Disable atomic writes (for performance)

```csharp
await downloader.DownloadFilesAsync(new DownloadRequest
{
    RepoId = "my-org/my-model",
    LocalDirectory = "./models",
    RequiredFiles = ["model.onnx"],
    UseAtomicWrites = false  // Write directly (faster, but no protection against partial downloads)
});
```

## Next Steps

- See the [API Reference](API_REFERENCE.md) for the complete class and method documentation
- See the [Architecture](ARCHITECTURE.md) for design decisions and how it works internally
