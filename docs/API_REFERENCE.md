# API Reference

## Classes

### `HuggingFaceDownloader`

The main entry point for downloading files from Hugging Face Hub repositories.

**Namespace:** `ElBruno.HuggingFace`

**Implements:** `IDisposable`

#### Constructors

| Constructor | Description |
|---|---|
| `HuggingFaceDownloader()` | Creates a downloader with default options |
| `HuggingFaceDownloader(HuggingFaceDownloaderOptions, ILogger?)` | Creates a downloader with custom options |
| `HuggingFaceDownloader(HttpClient, HuggingFaceDownloaderOptions?, ILogger?)` | Creates a downloader using an externally managed HttpClient |

#### Methods

| Method | Returns | Description |
|---|---|---|
| `DownloadFilesAsync(DownloadRequest, CancellationToken)` | `Task` | Downloads files described by the request. Skips existing files. |
| `GetMissingFiles(IEnumerable<string>, string)` | `IReadOnlyList<string>` | Returns files that don't exist in the local directory |
| `AreFilesAvailable(IEnumerable<string>, string)` | `bool` | Returns true if all files exist locally |
| `Dispose()` | `void` | Disposes the HttpClient if owned by this instance |

---

### `DownloadRequest`

Describes a set of files to download from a Hugging Face repository.

**Namespace:** `ElBruno.HuggingFace`

#### Properties

| Property | Type | Default | Description |
|---|---|---|---|
| `RepoId` | `string` | *(required)* | HF repository ID (e.g., `"sentence-transformers/all-MiniLM-L6-v2"`) |
| `LocalDirectory` | `string` | *(required)* | Local directory for downloaded files |
| `RequiredFiles` | `IReadOnlyList<string>` | *(required)* | Files that must be downloaded (failure throws) |
| `OptionalFiles` | `IReadOnlyList<string>?` | `null` | Files downloaded on best-effort basis |
| `Revision` | `string` | `"main"` | Git branch, tag, or commit SHA |
| `Progress` | `IProgress<DownloadProgress>?` | `null` | Progress reporter |
| `UseAtomicWrites` | `bool` | `true` | Write to temp file first, then rename |

---

### `DownloadProgress`

Reports progress during file downloads.

**Namespace:** `ElBruno.HuggingFace`

#### Properties

| Property | Type | Description |
|---|---|---|
| `Stage` | `DownloadStage` | Current download stage |
| `PercentComplete` | `double` | Overall completion (0–100) |
| `BytesDownloaded` | `long` | Total bytes downloaded across all files |
| `TotalBytes` | `long` | Total bytes expected (0 if unknown) |
| `CurrentFile` | `string?` | File currently being downloaded |
| `CurrentFileIndex` | `int` | 1-based index of current file |
| `TotalFileCount` | `int` | Total number of files to download |
| `Message` | `string?` | Human-readable status message |

---

### `DownloadStage` (enum)

| Value | Description |
|---|---|
| `Checking` | Resolving file sizes via HEAD requests |
| `Downloading` | Downloading files |
| `Validating` | Verifying all required files exist |
| `Complete` | All files downloaded and validated |
| `Failed` | Download operation failed |

---

### `HuggingFaceDownloaderOptions`

Configuration for the downloader.

**Namespace:** `ElBruno.HuggingFace`

#### Properties

| Property | Type | Default | Description |
|---|---|---|---|
| `AuthToken` | `string?` | `null` | HF auth token (falls back to `HF_TOKEN` env var) |
| `Timeout` | `TimeSpan` | 30 minutes | HTTP request timeout |
| `ResolveFileSizesBeforeDownload` | `bool` | `true` | Issue HEAD requests for accurate progress |
| `UserAgent` | `string?` | `null` | Custom User-Agent header |

---

## Static Helper Classes

### `HuggingFaceUrlBuilder`

| Method | Description |
|---|---|
| `GetFileUrl(string repoId, string filePath, string revision = "main")` | Returns the HF download URL for a file |

### `ByteFormatHelper`

| Method | Description |
|---|---|
| `FormatBytes(long bytes)` | Formats bytes as human-readable string (e.g., `"1.5 MB"`) |

### `DefaultPathHelper`

| Method | Description |
|---|---|
| `GetDefaultCacheDirectory(string appName)` | Returns OS-appropriate cache directory |
| `SanitizeModelName(string modelName)` | Replaces invalid path characters with `_` |

---

## Extension Methods

### `ServiceCollectionExtensions`

| Method | Description |
|---|---|
| `AddHuggingFaceDownloader(Action<HuggingFaceDownloaderOptions>?)` | Registers `HuggingFaceDownloader` as a singleton |
