# Flows

## Render pipeline (edit → preview)

1. Editor text changes → debounce (configurable) → `MarkdownService.ToHtml` (Markdig).
2. `PreviewService` wraps the body: theme CSS + optional user CSS (mtime-cached) + strict
   CSP + sanitized embedded HTML + injected JS (scroll reporting, checkbox clicks,
   double-click navigation) + optional Mermaid.
3. HTML is loaded into WebView2 (`NavigateToString`); preview scroll position is restored.
- Entry point: `MainWindow` editor `TextChanged` → `RenderPreviewAsync`.

## Bidirectional scroll sync

1. Outline anchors: `MarkdownService.ExtractOutline` records each heading's source line and
   its **Markdig ID**.
2. Editor→preview: first visible editor line → interpolate between surrounding heading
   anchors → scroll preview to interpolated position. Fallback to pure ratio when a document
   has no headings.
3. Preview→editor: injected JS posts the topmost visible heading + intra-section ratio back
   via `WebMessageReceived`; editor scrolls accordingly.
- Loop prevention: each direction sets a ~250 ms guard flag that suppresses the echo event.

## View & focus modes

- Three view modes (editor / split / preview) persisted in settings; `ApplyViewMode` also
  manages `ColumnDefinition.MinWidth` by code — **MinWidth beats Width=0**, so leaving
  MinWidth set breaks collapsed layouts (see TECHNICAL_NOTES).
- Focus: `FocusState` enum None/Simple/Full. F11 = Simple (hides sidebar + menu, keeps main
  toolbar buttons); Shift+F11 = Full (immersive fullscreen); Esc exits.

## Crash recovery

1. While a document is dirty, `RecoveryService` writes PID-keyed `DraftSnapshot`s on the
   auto-save tick.
2. Clean exit deletes the session's snapshots.
3. On startup, snapshots whose PID is dead are orphans → `RecoveryDialog` offers
   restore/discard per draft.

## Startup & CLI

1. `App.xaml.cs` registers `CodePagesEncodingProvider`, detects `portable.flag`, loads
   settings, calls `Loc.Initialize(settings.language)`.
2. Args: file path → open; folder path → open as workspace; `--export-html [--output]
   [--theme]` → headless export (exit codes, no stdout — WinExe); `--version`/`--help` →
   MessageBox.

## Language switch

1. `SettingsWindow` → language combo (`Tag` = auto/en/es/de/fr/zh-Hans) → saved to settings.
2. UI strings load once at startup, so a change shows a localized "restart needed" notice;
   next start initializes `Loc` with the new language.

## Release / distribution

1. Bump `<Version>` in `MarkLocal/MarkLocal.csproj`.
2. `build-portable.ps1` → self-contained publish + `portable.flag` + ZIP in `dist\`.
3. `build-msi.ps1` → publish + one MSI per culture (`-Cultures "en-US,es-ES"`), WiX v5 with
   `-loc` per-culture WXL. Fixed UpgradeCode + MajorUpgrade = clean in-place upgrades.
4. Tag `vX.Y.Z`, push, create GitHub release with the 3 artifacts
   (`gh release create`). Repo: github.com/alfonsosanzme/marklocal.
- OneDrive gotcha: build to a temp dir and copy into `dist\` with retries — OneDrive locks
  fresh MSI files (see TECHNICAL_NOTES).
