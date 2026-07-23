# Contributing to MarkLocal

English | **[Español](CONTRIBUTING.es.md)**

Thanks for your interest! MarkLocal is a small, pragmatic project. Ground rules:

## Project principles

Before proposing anything big, keep in mind the principles that guide decisions (they're in [docs/especificacion.md](docs/especificacion.md), in Spanish):

1. **Local-first** — everything works without internet. No accounts, no cloud, no telemetry.
2. **Lightweight** — few dependencies. Heavy libraries are rejected when there's a reasonable alternative.
3. **Predictable** — what gets saved is plain Markdown, no proprietary format.
4. **Multilingual, English-first** — the app ships in several languages (EN/ES/DE/FR/zh-Hans) with English as the base language. UI contributions must add their keys to the English (`en/`) JSON, and optionally to the other languages.

## How to contribute

- **Bugs**: open an issue with reproduction steps, the version (Help → About), and whether you use the MSI or portable build.
- **Features**: open an issue before writing code; this avoids work that doesn't fit the principles.
- **PRs**: branch from `main`, with a clear description of what and why. Tests must pass:

```powershell
dotnet test .\MarkLocal.Tests\MarkLocal.Tests.csproj
```

## Development environment

- .NET 8 SDK, Windows 10/11.
- `dotnet run --project .\MarkLocal\MarkLocal.csproj` to launch.
- The packaging scripts live in `MarkLocal\scripts\` (portable and MSI).

## Style

- Idiomatic C# for .NET 8, nullable enabled.
- Follow the style of whatever file you're in — sparse comments, only where the code can't explain itself.
- UI strings live in the localization JSON files, not hardcoded — add new keys to the English (`en/`) base and, where you can, the other languages.
