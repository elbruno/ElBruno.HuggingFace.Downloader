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
| `EnsureBundleAsync(ModelBundleManifest, string, IProgress<DownloadProgress>?, CancellationToken)` | `Task<ModelBundleResult>` | Ensures a manifest-defined bundle, validates size and SHA-256, and writes a resolved manifest file. |
| `ResolveCommitShaAsync(string, string, string, CancellationToken)` | `Task<string?>` | Resolves a branch or tag to an immutable commit SHA when the hub exposes it. |
| `GetMissingFiles(IEnumerable<string>, string)` | `IReadOnlyList<string>` | Returns files that don't exist in the local directory |
| `AreFilesAvailable(IEnumerable<string>, string)` | `bool` | Returns true if all files exist locally |
| `DeleteCachedFilesAsync(string, string, CancellationToken)` | `Task` | Deletes cached files for a repo from a cache root or direct repo directory path (no-op when missing) |
| `IsCached(string, string, IEnumerable<string>)` | `bool` | Returns true when all required files are cached for the specified repo |
| `ListCachedRepos(string)` | `IReadOnlyList<CachedRepoInfo>` | Lists cached repositories under a cache root with size and last-modified metadata |
| `GetCachedSize(string)` | `long` | Returns total size in bytes for a cached repo directory |
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
| `ExpectedCommitSha` | `string?` | `null` | Optional immutable commit SHA that the resolved revision must match |
| `ResolvedCommitSha` | `string?` | `null` | Immutable commit SHA resolved by the downloader when available |
| `Progress` | `IProgress<DownloadProgress>?` | `null` | Progress reporter |
| `UseAtomicWrites` | `bool` | `true` | Write to temp file first, then rename |
| `ResumePartialDownloads` | `bool` | `true` | Reuse preserved atomic partial downloads when the remote file still matches |

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
| `ResumedBytes` | `long` | Total bytes reused from preserved partial downloads |
| `CurrentFile` | `string?` | File currently being downloaded |
| `CurrentFileIndex` | `int` | 1-based index of current file |
| `TotalFileCount` | `int` | Total number of files to download |
| `Message` | `string?` | Human-readable status message |

---

### `ModelBundleManifest`

Describes a manifest-driven bundle of files to ensure from a single Hugging Face repository.

#### Properties

| Property | Type | Default | Description |
|---|---|---|---|
| `RepoId` | `string` | *(required)* | HF repository ID |
| `Revision` | `string` | `"main"` | Default Git revision for bundle files |
| `Files` | `IReadOnlyList<ModelBundleFile>` | *(required)* | Files included in the bundle |

### `ModelBundleFile`

Describes one file entry in a bundle manifest.

#### Properties

| Property | Type | Default | Description |
|---|---|---|---|
| `Path` | `string` | *(required)* | Repository-relative file path |
| `Size` | `long?` | `null` | Optional expected file size in bytes |
| `Sha256` | `string?` | `null` | Optional expected SHA-256 hash |
| `Required` | `bool` | `true` | Whether the file must exist after the run |
| `Revision` | `string?` | `null` | Optional per-file revision override |

### `ModelBundleResult`

Returned by `EnsureBundleAsync` after the bundle is validated and the resolved manifest is written.

#### Properties

| Property | Type | Description |
|---|---|---|
| `LocalDirectory` | `string` | Local directory used for the bundle |
| `ResolvedManifestPath` | `string` | Path to the written `.hf.bundle.resolved.json` file |
| `ResolvedManifest` | `ResolvedModelBundleManifest` | Resolved manifest content |
| `DownloadedFileCount` | `int` | Files downloaded during the current run |
| `ReusedFileCount` | `int` | Files already present and successfully reused |
| `MissingOptionalFileCount` | `int` | Optional files that remained unavailable |

### `ResolvedModelBundleManifest`

Records the resolved state of a validated bundle.

#### Properties

| Property | Type | Description |
|---|---|---|
| `RepoId` | `string` | HF repository ID |
| `Revision` | `string` | Single validated bundle revision |
| `ResolvedCommitSha` | `string?` | Immutable commit SHA resolved from the requested revision when available |
| `GeneratedAtUtc` | `DateTimeOffset` | UTC timestamp when the resolved manifest was written |
| `Files` | `IReadOnlyList<ResolvedModelBundleFile>` | Resolved file entries |

### `ResolvedModelBundleFile`

Records the resolved state of one file in a bundle.

#### Properties

| Property | Type | Description |
|---|---|---|
| `Path` | `string` | Repository-relative file path |
| `Revision` | `string` | Revision used for the file |
| `Required` | `bool` | Whether the file is required |
| `Exists` | `bool` | Whether the file exists locally after the run |
| `Size` | `long?` | Resolved size in bytes |
| `Sha256` | `string?` | Resolved SHA-256 hash |
| `DownloadedThisRun` | `bool` | Whether the file was downloaded during the current run |

### `ModelBundleManifestJson`

Helper for reading and writing manifest JSON files.

#### Methods

| Method | Returns | Description |
|---|---|---|
| `Deserialize(string)` | `ModelBundleManifest` | Deserializes a manifest from JSON text |
| `Serialize(ModelBundleManifest)` | `string` | Serializes a manifest to JSON text |
| `LoadAsync(string, CancellationToken)` | `Task<ModelBundleManifest>` | Loads a manifest from a JSON file |
| `SaveAsync(ModelBundleManifest, string, CancellationToken)` | `Task` | Saves a manifest to a JSON file |

### `DownloadResolutionMetadata`

Stored in `.hf.download.resolved.json` beside direct-download outputs.

#### Properties

| Property | Type | Description |
|---|---|---|
| `RepoId` | `string` | HF repository ID |
| `RequestedRevision` | `string` | Requested branch, tag, or commit SHA |
| `ResolvedCommitSha` | `string?` | Immutable commit SHA resolved by the downloader when available |
| `GeneratedAtUtc` | `DateTimeOffset` | UTC timestamp when the metadata was written |

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

### `CachedRepoInfo`

Aggregate metadata for a cached repository.

**Namespace:** `ElBruno.HuggingFace`

| Property | Type | Description |
|---|---|---|
| `LocalDirectory` | `string` | Absolute path to the cached repository directory |
| `TotalSizeBytes` | `long` | Total bytes for all files under the repository directory |
| `LastModified` | `DateTimeOffset` | Most recent file/directory modification timestamp |

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

### `HuggingFaceMetadataFileNames`

| Field | Description |
|---|---|
| `DownloadResolutionMetadata` | `.hf.download.resolved.json` |
| `ResolvedBundleManifest` | `.hf.bundle.resolved.json` |

---

## Extension Methods

### `ServiceCollectionExtensions`

| Method | Description |
|---|---|
| `AddHuggingFaceDownloader(Action<HuggingFaceDownloaderOptions>?)` | Registers `HuggingFaceDownloader` as a singleton |
