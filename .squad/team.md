# Squad Roster

**Project:** ElBruno.HuggingFace.Downloader
**Stack:** C# 12, .NET 8/9, NuGet package
**Author:** Bruno Capuano
**Initialized:** 2026-02-28

## Project Context

A .NET library for downloading files from Hugging Face Hub repositories with progress reporting, caching, atomic writes, and HF_TOKEN authentication support. The library is model-agnostic — consumers decide what files to download.

## Members

| Member | Role | Responsibility | Badge |
|--------|------|-----------------|-------|
| Neo | Lead / Architect | Scope decisions, code review, architecture coordination | 🏗️ |
| Trinity | Backend/Library Dev | Core downloader implementation, API design, service integration | 🔧 |
| Agent Smith | Test Architect | Test strategy, edge cases, integration tests, CI resilience | 🧪 |
| Morpheus | Documentation/DevRel | API guides, getting started, examples, publishing workflows | 📝 |
| Scribe | Session Logger | Decisions, memories, session logs | 📋 |
| Ralph | Work Monitor | Issue triage, backlog, keep-alive | 🔄 |

## Issue Source

None configured yet. To connect to GitHub issues, use: `gh issue list --repo owner/repo --label squad --state open`

## Key Decisions

See `.squad/decisions.md` for team decisions on architecture, scope, and process.

## Files of Interest

- **Core Library:** `src/ElBruno.HuggingFace.Downloader/*.cs`
- **Tests:** `tests/ElBruno.HuggingFace.Downloader.Tests/*.cs`
- **Documentation:** `docs/` (GETTING_STARTED.md, API_REFERENCE.md, ARCHITECTURE.md)
- **Project File:** `ElBruno.HuggingFace.Downloader.slnx`
