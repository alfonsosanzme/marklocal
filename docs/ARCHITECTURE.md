# Architecture

## Overview

MarkLocal is a single-process WPF desktop application (.NET 8, `net8.0-windows`) with no
backend and no network dependencies at runtime (except the optional manual update check).
The UI is one main window (`MainWindow`) that composes an AvalonEdit editor, a WebView2
preview, and a sidebar; all domain logic lives in stateless-ish service classes under
`MarkLocal/Core/`, instantiated once by `MainWindow` and the CLI runner.

## Components

- **MainWindow** (`MarkLocal/MainWindow.xaml(.cs)`) — shell and orchestrator: menus, two
  toolbars, sidebar (outline / same-folder files / workspace tree), status bar, view modes
  (editor / split / preview), focus modes (F11 simple, Shift+F11 full), scroll sync, theme
  application. Large by design; most logic delegates to services.
- **MarkdownService** (`Core/MarkdownService.cs`) — Markdig pipeline
  (`UseAdvancedExtensions().UseAutoIdentifiers()`), HTML conversion, outline extraction
  (`ExtractOutline` reads real Markdig heading IDs via `heading.GetAttributes().Id`), `[TOC]`.
- **PreviewService** (`Core/PreviewService.cs`) — builds the full preview HTML document:
  theme CSS (`Assets/preview.css` / `preview-dark.css`), optional custom user CSS (cached by
  mtime), strict CSP, HtmlSanitizer for embedded HTML, optional Mermaid from
  `Assets/lib/mermaid.min.js`, and the injected JS for scroll reporting, checkbox toggling
  and double-click-to-editor.
- **DocumentService** (`Core/DocumentService.cs`) — load/save, encoding detection (UTF-8 /
  BOM / CP-1252 via `CodePagesEncodingProvider`), line-ending handling, dirty tracking.
- **WorkspaceService** (`Core/WorkspaceService.cs`) — folder workspaces: tree building,
  same-folder file list, root changes, parent navigation.
- **RecoveryService** (`Core/RecoveryService.cs`) — per-session draft snapshots (PID-keyed)
  under the drafts folder, orphan detection on startup, recovery dialog feed.
- **SettingsService / AppSettings** — JSON settings persistence; resolves portable vs
  installed data locations.
- **LocalizationService** (`Core/LocalizationService.cs`, static class `Loc`) — custom JSON
  i18n. `Loc.Initialize(setting)` merges the four JSON files of the resolved language over
  English; `Loc.T(key, args)` in C#, `{local:Loc key}` (`LocExtension.cs`) in XAML.
  Languages: en (base), es, de, fr, zh-Hans. See ADR-0001.
- **ExportService / CliRunner** — HTML export (shared with preview pipeline), PDF via
  WebView2 printing; `CliRunner` handles `--export-html`, `--version`, `--help` (MessageBox
  output — WinExe has no stdout).
- **UpdateService** (`Core/UpdateService.cs`) — manual "check for updates" against a JSON
  feed URL (configurable; not yet published).
- **TemplateService** — "New from template" documents, generated in the active UI language.
- **Infrastructure/** — recent-files store; `WindowsIntegration.cs` for registry file
  association and context-menu entries (used by install/uninstall scripts, skipped in
  portable mode).
- **Views/** — `SettingsWindow` (preferences incl. language picker), `FindReplaceDialog`,
  `RecoveryDialog`.

## Data & integrations

No database, no services. Everything is files: the Markdown documents themselves,
`settings.json`, draft snapshots, templates, and the i18n JSON assets. WebView2 (Evergreen
runtime) is the only external runtime requirement.

## Structure

```
MarkLocal/            WPF app: Core/ (services), Models/, Views/, Infrastructure/,
                      Resources/ (AvalonEdit .xshd light+dark), Assets/ (CSS, icon, i18n/, lib/),
                      scripts/ (install, uninstall, build-portable, build-msi, utilities)
MarkLocal.Tests/      xUnit (80 tests); TestFactory initializes Loc with "en"
installer/wix/        WiX v5: MarkLocal.wxs + per-culture .wxl (en-US, es-ES)
docs/                 This documentation + distribution-plan, update-strategy, especificacion (original spec, Spanish)
blog/                 Launch article (Spanish)
dist/                 Build outputs (gitignored)
```

## Dependencies

- **Markdig** — Markdown parsing/rendering (advanced extensions + auto identifiers).
- **AvalonEdit** — the text editor; custom XSHD highlighting, one file per theme.
- **WebView2** — preview rendering, PDF export, printing.
- **HtmlSanitizer (Ganss.Xss)** — sanitizes raw HTML embedded in Markdown.
- **System.Text.Encoding.CodePages** — CP-1252 support on .NET 8.
- **WiX v5** (build-time only, global dotnet tool + `WixToolset.UI.wixext`) — MSI.
