# Agent briefing

> Entry point for an AI agent or a developer joining with zero prior context.
> Read this first, then branch into the other docs as needed.

## What this project is

**MarkLocal** — a local Markdown editor/viewer for Windows (WPF, .NET 8). No cloud, no
accounts, no telemetry. Editor (AvalonEdit) + live preview (Markdig → WebView2) with
bidirectional scroll sync, folder workspaces, draft recovery, light/dark themes, and a
multilingual UI (EN base, ES, DE, FR, zh-Hans). Open source (MIT) at
**github.com/alfonsosanzme/marklocal**. Author: Alfonso Sanz López (Kairis, kairis.es);
built with Claude Code. The user works in Spanish — report to them in Spanish.

## Current state

**v1.0.0 released** (2026-10-20): tag `v1.0.0` + GitHub release with 3 artifacts
(MSI -en, MSI -es, portable ZIP). `main` is clean and pushed; 80/80 xUnit tests green.
Nothing is in progress — the project is parked awaiting the next improvement round.
The original Spanish spec lives in `docs/especificacion.md`.

## Map of the codebase

- `MarkLocal/` — the WPF app. `Core/` services (Markdown, Preview, Document, Workspace,
  Recovery, Settings, Localization, Export, Update, Template, CliRunner), `Models/`,
  `Views/` (Settings, Find/Replace, Recovery), `Infrastructure/` (recent files, registry),
  `Resources/` (XSHD light+dark), `Assets/` (preview CSS, icon, `i18n/<lang>/*.json`,
  optional `lib/`), `scripts/` (build/install PowerShell).
- `MarkLocal.Tests/` — xUnit; `TestFactory` initializes `Loc` with "en".
- `installer/wix/` — WiX v5 MSI (`.wxs` + per-culture `.wxl`).
- `docs/` — this documentation + `distribution-plan.md`, `update-strategy.md`.
- `MarkLocal - copia/` — the user's manual backup. **Never touch or delete it** (gitignored).

## How it fits together

`MainWindow` orchestrates everything; domain logic lives in `Core/` services. Text changes
flow editor → Markdig → PreviewService (CSS/CSP/sanitizer/JS) → WebView2. Scroll sync
interpolates between heading anchors using Markdig's own IDs. Settings/drafts/templates are
JSON files whose location flips to `Data\` when `portable.flag` sits next to the exe.
Details: `docs/ARCHITECTURE.md`, `docs/FLOWS.md`, `docs/DATA_MODEL.md`.

## Key decisions already made (docs/decisions/)

- ADR-0001 — custom flat-JSON i18n (`Loc.T` / `{local:Loc}`), EN fallback, restart on change.
- ADR-0002 — themes swap whole frozen brushes in `ApplyTheme`; never DynamicResource colors.
- ADR-0003 — WiX v5 per-user MSI; **UpgradeCode `1C9CE61E-...` must never change**; one MSI
  per installer culture.
- ADR-0004 — portable mode via `portable.flag` → `Data\` roots, no registry.

## What NOT to break

- The i18n contract: new strings go to **all 5 languages**, EN always has the key.
- Markdig heading IDs for outline/scroll anchors (no hand-rolled slugs).
- `.ps1` files stay UTF-8 **with BOM**; BOM is trimmed before `JsonSerializer` reads drafts.
- MinWidth of layout columns is managed in code by `ApplyViewMode` — not in XAML.
- `portable.flag` must never end up inside the MSI.
- Full gotcha list: `docs/TECHNICAL_NOTES.md`.

## Environment quirks (this user's machine)

- Repo lives under **OneDrive** → fresh build outputs get locked; build to temp and copy
  with retries. Prefer building via the provided scripts.
- `dotnet` may not be on PATH in new shells: `$env:PATH = "C:\Program Files\dotnet;" + $env:PATH`;
  `wix` is in `$env:USERPROFILE\.dotnet\tools`. Use pwsh 7 for script work.
- Commands: `dotnet test .\MarkLocal.Tests\MarkLocal.Tests.csproj` (80 tests);
  `.\MarkLocal\scripts\build-portable.ps1`; `.\MarkLocal\scripts\build-msi.ps1`.

## Open threads

- Code-signing certificate (SmartScreen warning on the MSI).
- Publish `updates.json` on kairis.es so Help → "Check for updates" works; later real
  auto-update (`docs/update-strategy.md`).
- Minor: German `Strg`/`Umschalt` accelerator labels; optional bundled Mermaid/MathJax.
- Deliberately parked: spell checker; possible online version (long-term idea).
