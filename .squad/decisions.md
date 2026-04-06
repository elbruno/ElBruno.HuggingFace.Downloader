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

---

## Implementation & Maintenance

### 2026-04-06: NuGet Dependency Upgrade to 10.x

**By:** Trinity (Backend/Library Developer)  
**Status:** ✅ **Implemented**  
**Timestamp:** 2026-04-06T13:40:33Z

**What:** Upgraded all NuGet dependencies to latest stable versions.

**Library Project (net8.0;net9.0):**
- Microsoft.Extensions.DependencyInjection.Abstractions: 9.0.0 → 10.0.5
- Microsoft.Extensions.Logging.Abstractions: 9.0.0 → 10.0.5

**Test Project (net9.0):**
- Microsoft.Extensions.DependencyInjection: 9.0.0 → 10.0.5
- Microsoft.NET.Test.Sdk: 17.12.0 → 18.3.0
- xunit.runner.visualstudio: 2.8.2 → 3.1.5

**Why:** Security patches, bug fixes, better .NET 10 support, modern test infrastructure. All 65 tests pass without modification. No breaking changes; backward compatible with net8.0.

---

### 2026-04-06T13:48Z: Phase 1-3 Test Implementation Complete

**By:** Agent Smith (Test Architect)  
**Status:** ✅ **Implemented**  
**Timestamp:** 2026-04-06T13:48:19Z

**What:** Implemented Phase 1 (34 tests) and Phase 2+3 (23 tests) from test strategy, bringing total coverage from ~30% to ~45%. Phase 4 deferred.

**Phase 1 Implementation (34 tests):**
- HuggingFaceUrlBuilder: 16 validation tests (null/empty/malformed/path traversal on repoId, filePath, revision)
- HuggingFaceDownloaderOptionsValidationTests: 4 timeout validation tests
- DefaultPathHelper: 4 edge case tests (null/empty handling)

**Phase 2+3 Implementation (23 tests):**
- Core download flow (14 tests): single/multiple/mixed files, progress stages, atomic writes, HEAD requests
- Error handling (9 tests): HTTP 401/403/404/500, optional resilience, cancellation with cleanup

**Why:** Security-critical gaps addressed. Download pipeline behavior verified. Error semantics explicit.

**Validation Gaps Discovered (LOW priority, functional):**
- `DefaultPathHelper.SanitizeModelName` — No null guard (throws NRE vs ArgumentException)
- `DefaultPathHelper.GetDefaultCacheDirectory` — No explicit null/empty validation

**Result:** All 122 tests pass. No bugs in source code. Ready for Phase 4 (next session).

---

### 2026-04-06T13:48Z: User Directive — Defer Phase 4

**By:** Bruno Capuano (via Copilot)  
**Status:** ✅ **Acknowledged**  
**Timestamp:** 2026-04-06T13:48Z

**What:** Do not implement Phase 4 test recommendations (authentication, environment variables, file size resolution). Defer to next week.

**Why:** User request — captured for team memory.

---

### 2026-04-06: Test Coverage Strategy (4-Phase Proposal)

**By:** Agent Smith (Test Architect)  
**Status:** 📋 **Proposed** (Awaiting Bruno Capuano Approval)  
**Timestamp:** 2026-04-06T13:40:33Z

**What:** Four-phase testing strategy to expand coverage from ~30% (core logic) to 90%+ target.

**Phase 1 (CRITICAL):** Security & Validation (22 tests)
- URL validation: null/empty/malformed/path-traversal inputs
- Options timeout validation
- Path helper null/empty handling

**Phase 2 (CRITICAL):** Core Download Flow (18 tests)
- Mocked HTTP download tests (single/multiple files, progress, atomic writes)
- Atomic write verification (temp files, renames)
- Progress reporting (stage transitions)

**Phase 3 (IMPORTANT):** Error Handling (12 tests)
- HTTP error codes (401/403/404/500)
- Cancellation scenarios with cleanup
- File I/O failures (disk full, permissions)

**Phase 4 (IMPORTANT):** Authentication & Configuration (10 tests)
- Authorization headers with/without token
- Environment variable fallback (HF_TOKEN)
- File size resolution via HEAD requests

**Why:** Security vulnerabilities are critical (path traversal). Core download pipeline untested with real HTTP mocking. Error scenarios define user experience. Authentication behavior should be explicit and tested.

**Testing Patterns:**
- HttpMessageHandler mocking (no external dependencies, deterministic)
- Atomic write verification via progress callbacks
- Progress capture with stage transition assertions

**Effort:** 2-4 weeks; prioritize Phases 1-2 (40 tests) for critical gaps.

**Risks & Mitigations:**
- Mocked tests won't catch real-world HTTP issues → Add opt-in integration tests against live HF Hub
- 75 tests is large effort → Focus on critical phases first
- Brittle HTTP mocking → Test behavior, not implementation

**Next Steps:**
1. Get approval from Bruno Capuano
2. Implement Phase 1 tests (security validation)
3. Implement Phase 2 tests (core download flow)
4. Implement Phase 3 tests (error handling)
5. Implement Phase 4 tests (authentication)
