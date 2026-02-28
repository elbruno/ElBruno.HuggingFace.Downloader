# Morpheus — History

## Session Context

**Project:** ElBruno.HuggingFace.Downloader
**Tech Stack:** C# 12+, .NET 8.0/9.0, NuGet library, xUnit tests
**Author:** Bruno Capuano
**License:** MIT

This is a .NET library for downloading files from Hugging Face Hub repositories. The DevRel role is to ensure users understand and can easily integrate this library into their projects.

**Documentation Structure:**
- `README.md` — root-level overview, quick start, badges, and links
- `docs/GETTING_STARTED.md` — installation, basic examples, setup steps
- `docs/API_REFERENCE.md` — complete API documentation with examples
- `docs/ARCHITECTURE.md` — design decisions and internal design
- `docs/publishing.md` — NuGet publishing workflow and release notes

**Key Concepts to Document:**
- Model-agnostic design (consumers decide what to download)
- Progress reporting with IProgress<T>
- Atomic writes for data integrity
- HF_TOKEN authentication (environment or explicit)
- Dependency injection integration
- Cache directory helpers (cross-platform)
- Required vs optional files
- Error handling and edge cases

## Learnings

(Append learnings as sessions progress.)
