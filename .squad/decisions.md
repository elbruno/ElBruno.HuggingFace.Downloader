# Team Decisions

Authoritative record of architectural, scope, and process decisions made by the squad.

> Last updated: 2026-02-28

## Architecture & Design

**Decision: Model-Agnostic Library Design**
- The library downloads arbitrary files from HF repos. It does NOT include model-specific logic.
- Consumers (not the library) decide what files to download by providing file lists.
- Rationale: Flexibility, reusability across different model types and domains.
- Status: ✅ Established (see `docs/ARCHITECTURE.md`)

**Decision: Atomic Writes with Temp File Pattern**
- Downloads write to temp files, then rename atomically to prevent partial file corruption.
- Enabled by default; consumers cannot disable it.
- Rationale: Data integrity, fail-safe recovery.
- Status: ✅ Established

**Decision: HttpClient Ownership Model**
- `HuggingFaceDownloader` owns its `HttpClient` when created via options (`new HuggingFaceDownloaderOptions`).
- Consumers may also pass an externally managed `HttpClient` via constructor overload.
- Rationale: Flexibility (single instance for shared pool) and simplicity (fire-and-forget).
- Status: ✅ Established

**Decision: HF_TOKEN from Environment or Explicit Options**
- Read from `HF_TOKEN` environment variable by default.
- Can be overridden via `HuggingFaceDownloaderOptions.AuthToken`.
- Rationale: Convenience + security (tokens not in code).
- Status: ✅ Established

## Code Style & Conventions

**Decision: C# 12+, .NET 8/9 Target**
- Use C# 12+ features: collection expressions, primary constructors, raw string literals.
- Target `net8.0` and `net9.0` for the library. Tests target `net9.0`.
- Namespace: `ElBruno.HuggingFace` (not `ElBruno.HuggingFace.Downloader`).
- Status: ✅ Established

**Decision: XML Documentation on Public Types**
- All public types must have XML documentation comments (`///`).
- Rationale: NuGet consumers need discoverable API documentation.
- Status: ✅ Established

**Decision: sealed Classes by Default**
- Use `sealed` on classes not designed for inheritance.
- Use `init` properties for immutable configuration objects.
- Use `required` keyword for properties that must be set at initialization.
- Status: ✅ Established

**Decision: IReadOnlyList in Public APIs**
- Prefer `IReadOnlyList<T>` over `List<T>` in public APIs.
- Rationale: API stability, prevents accidental mutations.
- Status: ✅ Established

**Decision: ConfigureAwait(false) in Library Code**
- Library async methods must use `.ConfigureAwait(false)`.
- Rationale: Best practice for reusable libraries (doesn't force consumers onto specific synchronization context).
- Status: ✅ Established

## Testing

**Decision: xUnit Test Framework**
- Test framework: xUnit.
- All test files must include `using Xunit;`.
- No network access in tests (mock HTTP with Moq or similar).
- Test class names match the class being tested (e.g., `HuggingFaceDownloaderTests`).
- Status: ✅ Established

**Decision: IDisposable Pattern in Tests**
- Test classes that create temp directories or resources use `IDisposable` pattern.
- Rationale: Clean resource cleanup, no test pollution.
- Status: ✅ Established

## Documentation Structure

**Decision: Docs in `docs/` Folder**
- Only `README.md` and `LICENSE` at repository root.
- All additional documentation (guides, API reference, architecture) in `docs/`.
- Rationale: Clean root, organized documentation.
- Status: ✅ Established
- Files: `GETTING_STARTED.md`, `API_REFERENCE.md`, `ARCHITECTURE.md`, `publishing.md`

## NuGet Publishing

**Decision: Package Identity**
- Package ID: `ElBruno.HuggingFace.Downloader`
- Author: Bruno Capuano
- License: MIT
- The root `README.md` is included as the NuGet readme.
- Status: ✅ Established

## Process

**Decision: Code Review Required Before Merge**
- All changes to core API or implementation require Neo's (Lead's) code review.
- Test changes reviewed by Agent Smith (Test Architect).
- Documentation reviewed by Morpheus (DevRel).
- Rationale: Quality assurance, consistency, knowledge sharing.
- Status: ✅ Established

**Decision: Ceremonies Enabled**
- Design Review: auto-triggered before multi-agent tasks (design, implementation coordination).
- Retrospective: auto-triggered after failures (build failures, test failures, rejections).
- Rationale: Alignment, continuous improvement.
- Status: ✅ Established (see `.squad/ceremonies.md`)
