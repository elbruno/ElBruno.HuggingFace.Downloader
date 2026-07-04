# Architecture

## Overview

`ElBruno.HuggingFace.Downloader` is a .NET library that downloads files from [Hugging Face Hub](https://huggingface.co) repositories. It was extracted from common download logic found across multiple projects ([ElBruno.LocalEmbeddings](https://github.com/elbruno/elbruno.localembeddings), [ElBruno.QwenTTS](https://github.com/elbruno/ElBruno.QwenTTS), [ElBruno.VibeVoiceTTS](https://github.com/elbruno/ElBruno.VibeVoiceTTS)) to provide a single, reusable NuGet package.

## Design Principles

1. **No model-specific logic** — The library downloads arbitrary files from HF repos. Consumers define which files they need (ONNX models, tokenizers, voice presets, etc.).

2. **Feature superset** — The API combines the best features from all three source implementations:
   - Rich progress reporting (from VibeVoiceTTS)
   - Atomic temp-file writes (from LocalEmbeddings)
   - HF_TOKEN authentication (from VibeVoiceTTS)
   - Required vs optional file handling (from LocalEmbeddings + VibeVoiceTTS)
   - HEAD requests for total size (from VibeVoiceTTS)

3. **DI-friendly** — Constructor injection, `ILogger` support, and `IServiceCollection` extensions.

4. **Zero opinions on caching** — Consumers provide the local directory. The `DefaultPathHelper` utility suggests platform-appropriate paths but doesn't enforce them.

## How It Works

```
┌─────────────────────────────────────────────┐
│              HuggingFaceDownloader           │
│                                             │
│  DownloadFilesAsync(DownloadRequest)        │
│    │                                        │
│    ├─ 1. Build file list (required+optional)│
│    ├─ 2. Filter to missing files only       │
│    ├─ 3. HEAD requests for total size       │
│    ├─ 4. Download each file (streaming)     │
│    │     ├─ Reuse .partial when safe        │
│    │     ├─ Send HTTP range requests        │
│    │     ├─ Report per-byte progress        │
│    │     └─ Rename temp file → final        │
│    ├─ 5. Validate all required files exist  │
│    └─ 6. Report Complete                    │
│                                             │
│  GetMissingFiles() / AreFilesAvailable()    │
│    └─ Check local filesystem                │
└─────────────────────────────────────────────┘
```

## URL Pattern

All Hugging Face file downloads use the resolve API:

```
https://huggingface.co/{repoId}/resolve/{revision}/{filePath}
```

For example:
```
https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx
```

## Atomic Writes

When `UseAtomicWrites` is enabled (default), incomplete downloads are written to a resumable `.partial` file plus a small metadata sidecar:

```
model.onnx.partial
model.onnx.partial.json
  →  (download complete and validated)  →  model.onnx
```

If the request is retried and the remote file still matches the saved revision and ETag, the downloader resumes with an HTTP range request. If the remote changed or the server ignores ranges, the partial artifacts are discarded and the file restarts from byte zero.

## Authentication

The library supports Hugging Face authentication for private/gated repositories:

1. **Environment variable** (recommended): Set `HF_TOKEN` — the library reads it automatically
2. **Explicit token**: Pass `AuthToken` in `HuggingFaceDownloaderOptions`

The token is sent as a `Bearer` token in the `Authorization` HTTP header.

## Error Handling

| Scenario | Behavior |
|---|---|
| Required file returns 401/403 | `InvalidOperationException` with auth guidance |
| Required file returns 404 | `InvalidOperationException` with file not found message |
| Required file returns other error | `InvalidOperationException` wrapping `HttpRequestException` |
| Optional file fails | Silently skipped, logged as warning |
| Download partially completes with resume enabled | `.partial` artifacts are preserved for the next retry |
| Remote revision or ETag changes | Preserved partial download is discarded and restarted |
| Server ignores range requests | Preserved partial download is discarded and restarted |
| All downloads complete but required file missing | `InvalidOperationException` listing missing files |

## Project Structure

```
ElBruno.HuggingFace.Downloader/
├── src/
│   └── ElBruno.HuggingFace.Downloader/
│       ├── HuggingFaceDownloader.cs          # Core download engine
│       ├── DownloadRequest.cs                # Download configuration
│       ├── DownloadProgress.cs               # Progress reporting model
│       ├── DownloadStage.cs                  # Progress stage enum
│       ├── HuggingFaceDownloaderOptions.cs   # Downloader configuration
│       ├── HuggingFaceUrlBuilder.cs          # URL construction
│       ├── ByteFormatHelper.cs               # Byte formatting utility
│       ├── DefaultPathHelper.cs              # Platform cache paths
│       └── ServiceCollectionExtensions.cs    # DI registration
├── tests/
│   └── ElBruno.HuggingFace.Downloader.Tests/
├── docs/
│   ├── GETTING_STARTED.md
│   ├── API_REFERENCE.md
│   └── ARCHITECTURE.md
├── README.md
└── LICENSE
```
