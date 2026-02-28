# Trinity — History

## Session Context

**Project:** ElBruno.HuggingFace.Downloader
**Tech Stack:** C# 12+, .NET 8.0/9.0, NuGet library, xUnit tests
**Author:** Bruno Capuano
**License:** MIT

This is a .NET library for downloading files from Hugging Face Hub repositories. The library is model-agnostic, focusing on robust downloads with atomic writes, progress reporting, and authentication support.

**Core Components to Know:**
- `HuggingFaceDownloader` — main API class for downloads
- `DownloadRequest` — request configuration (repo ID, local directory, file lists)
- `DownloadProgress` — progress reporting (stage, percent, message)
- `HuggingFaceUrlBuilder` — URL construction for HF Hub API
- `ServiceCollectionExtensions` — DI registration
- `DefaultPathHelper` — cross-platform cache directory resolution
- `ByteFormatHelper` — human-readable file size formatting

**Code Style Requirements:**
- C# 12+ idioms (collection expressions, primary constructors, raw strings)
- `sealed` classes by default
- `init` properties for configuration
- `required` keyword for required properties
- `ConfigureAwait(false)` on all async calls
- XML docs (`///`) on all public types
- `IReadOnlyList<T>` in public APIs (not `List<T>`)

## Learnings

(Append learnings as sessions progress.)
