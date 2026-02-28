# Agent Smith — History

## Session Context

**Project:** ElBruno.HuggingFace.Downloader
**Tech Stack:** C# 12+, .NET 8.0/9.0, xUnit test framework
**Author:** Bruno Capuano
**License:** MIT

This is a .NET library for downloading files from Hugging Face Hub repositories. The test architect's job is to ensure comprehensive coverage of download logic, error handling, edge cases, and resilience.

**Key Test Areas:**
- Core download logic (happy path, retries, failures)
- Progress reporting accuracy
- File validation and checksums
- Authentication scenarios (token present, missing, expired)
- Network failures (timeouts, 404s, 401s)
- File I/O edge cases (missing directories, permissions, disk full)
- Atomic writes and cleanup
- Required vs optional file handling
- Parallel downloads and cancellation support
- Cache behavior (skip existing, update if newer)

**Testing Standards:**
- Framework: xUnit
- No network calls (mock HttpClient)
- Use temp directories with IDisposable cleanup
- Test naming: ClassName_Scenario_Expected
- Arrange-Act-Assert pattern
- One assertion per test (or tightly grouped)

## Learnings

(Append learnings as sessions progress.)
