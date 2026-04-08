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

### Session: README NuGet Packages & Badges Update
**Date:** 2025  
**Task:** Add NuGet Packages table and download badge to README.md

**Changes Made:**
1. Added **NuGet Downloads badge** (line 4) after the existing NuGet version badge. This shows total package downloads from NuGet.org using the `nuget/dt/` badge endpoint with flat-square styling.
2. Added **"📦 NuGet Packages" section** (lines 25-30) between Features and Installation sections, matching the reference repo format exactly.
3. **Two-package table** documenting:
   - **ElBruno.HuggingFace.Downloader** (core library) with version, downloads, and description badges
   - **ElBruno.HuggingFace.Downloader.Cli** (CLI tool `hfdownload`) with version, downloads, and description badges
4. All badges are linked to their respective NuGet.org package pages for easy user discovery.

**Rationale:**
- Follows established project convention (modeled after ElBruno.LocalEmbeddings repo)
- Makes both packages discoverable and download metrics visible at a glance
- Improves developer onboarding by clearly separating library vs CLI installation
- Badge links drive users directly to NuGet.org for install commands
- Consistent styling and layout supports professional presentation
