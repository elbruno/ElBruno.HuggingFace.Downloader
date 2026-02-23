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
    OptionalFiles = ["tokenizer_config.json", "vocab.txt"]
});
```

### 2) Check if files are already downloaded

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

### 3) Track download progress

```csharp
var progress = new Progress<DownloadProgress>(p =>
{
    switch (p.Stage)
    {
        case DownloadStage.Checking:
            Console.WriteLine($"🔍 {p.Message}");
            break;
        case DownloadStage.Downloading:
            Console.Write($"\r⬇️ [{p.CurrentFileIndex}/{p.TotalFileCount}] {p.CurrentFile} — {p.PercentComplete:F0}%");
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

### 4) Authenticate for private/gated repositories

```csharp
// Option A: Set the HF_TOKEN environment variable (recommended)
// export HF_TOKEN=hf_your_token_here

// Option B: Pass the token explicitly
var downloader = new HuggingFaceDownloader(new HuggingFaceDownloaderOptions
{
    AuthToken = "hf_your_token_here"
});
```

### 5) Use with Dependency Injection

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

### 6) Download from a specific branch or tag

```csharp
await downloader.DownloadFilesAsync(new DownloadRequest
{
    RepoId = "my-org/my-model",
    LocalDirectory = "./models",
    RequiredFiles = ["model.onnx"],
    Revision = "v2.0"  // branch, tag, or commit SHA
});
```

### 7) Use platform-specific cache directories

```csharp
// Returns OS-appropriate cache path:
// Windows: %LOCALAPPDATA%/MyApp/models
// Linux/macOS: ~/.local/share/MyApp/models
string cacheDir = DefaultPathHelper.GetDefaultCacheDirectory("MyApp");

// Sanitize model names for use as directory names
string safeName = DefaultPathHelper.SanitizeModelName("org/model-name");
// → "org_model-name"
```

### 8) Disable atomic writes (for performance)

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
