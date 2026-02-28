# Neo — History

## Session Context

**Project:** ElBruno.HuggingFace.Downloader
**Tech Stack:** C# 12+, .NET 8.0/9.0, NuGet library, xUnit tests
**Author:** Bruno Capuano
**License:** MIT

This is a .NET library for downloading files from Hugging Face Hub repositories. The library is model-agnostic, focusing on robust downloads with atomic writes, progress reporting, and authentication support.

**Key Architectural Patterns:**
- Model-agnostic design (consumers provide file lists)
- Atomic writes (temp file + rename to prevent corruption)
- HttpClient ownership (library-owned or consumer-provided)
- HF_TOKEN from environment or explicit options
- ILogger integration for structured logging
- Dependency injection support via ServiceCollectionExtensions

## Learnings

(Append learnings as sessions progress.)
