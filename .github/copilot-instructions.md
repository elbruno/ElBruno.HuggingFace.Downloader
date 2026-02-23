# Copilot Instructions for ElBruno.HuggingFace.Downloader

## Repository Conventions

### Documentation Structure
- **Root level**: Only `README.md` and `LICENSE` are documentation files at the repository root. No other `.md` documentation files should be placed at the root.
- **`docs/` folder**: All additional documentation (getting started guides, API references, architecture docs, etc.) must go in the `docs/` folder.
- When creating new documentation, always place it in `docs/` and link to it from the README if appropriate.

### Project Structure
- **`src/`**: Production library code only. The main library project is `src/ElBruno.HuggingFace.Downloader/`.
- **`tests/`**: All test projects go here. The test project is `tests/ElBruno.HuggingFace.Downloader.Tests/`.
- **`docs/`**: All documentation beyond README and LICENSE.
- **`.github/workflows/`**: CI/CD workflows.

### Code Style
- Use C# 12+ features (collection expressions, primary constructors, raw string literals where appropriate).
- Target `net8.0` and `net9.0` for the library. Tests target `net9.0`.
- Namespace: `ElBruno.HuggingFace` (not `ElBruno.HuggingFace.Downloader`).
- All public types must have XML documentation comments (`///`).
- Use `sealed` on classes that are not designed for inheritance.
- Use `init` properties for immutable configuration objects.
- Use `required` keyword for properties that must be set at initialization.
- Prefer `IReadOnlyList<T>` over `List<T>` in public APIs.
- Use `ConfigureAwait(false)` in library async code.

### Testing
- Test framework: xUnit.
- All test files must include `using Xunit;`.
- Use `IDisposable` pattern in test classes that create temp directories or resources.
- Tests should not require network access (mock HTTP where needed for download tests).
- Test class names should match the class being tested (e.g., `HuggingFaceDownloaderTests`).

### NuGet Package
- Package ID: `ElBruno.HuggingFace.Downloader`
- Author: Bruno Capuano
- License: MIT
- The package includes the root `README.md` as the NuGet readme.

### Key Design Decisions
- The library is **model-agnostic** — it downloads arbitrary files from HF repos. Never add model-specific logic.
- **Consumers provide** the file lists and local directories. The library doesn't decide what to download.
- The `HuggingFaceDownloader` class owns its `HttpClient` when created via options, but accepts an externally managed `HttpClient` via constructor overload.
- Atomic writes (temp file + rename) are enabled by default to prevent partial file corruption.
- HF_TOKEN is read from environment variable by default but can be overridden via `HuggingFaceDownloaderOptions.AuthToken`.
