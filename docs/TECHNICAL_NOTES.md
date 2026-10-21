# Technical notes

Living notes. Add, don't rewrite. Prune only when something is truly obsolete.

## Known issues

- **Unsigned installer** — SmartScreen shows "unknown publisher" until a code-signing
  certificate is bought and wired into the build.
- **Update feed not published** — `UpdateService` checks a JSON feed URL; the feed
  (`updates.json`) still needs to be hosted (plan: kairis.es). Until then, "Check for
  updates" reports no update.
- **German shortcut labels** — menu accelerators show `Ctrl`/`Shift`, not `Strg`/`Umschalt`.
  Cosmetic; keys work.
- **No spell checker** — deliberate (AvalonEdit doesn't expose one; alternatives are heavy).
  Documented in README as a known limitation.

## Gotchas (things that look wrong but are intentional, or will bite you)

- **Theme brushes are replaced whole in `ApplyTheme`** — do NOT switch to
  `SolidColorBrush Color="{DynamicResource ...}"`: WPF freezes those brushes inside menu/
  popup visual trees and the colors never update. `ApplyTheme` swaps complete brushes
  (`SetBrush` + `Freeze`). See ADR-0002.
- **`ColumnDefinition.MinWidth` beats `Width=0`** — collapsing a column requires clearing
  its MinWidth in code. `ApplyViewMode`/focus handling owns this; don't set MinWidth in XAML.
- **Outline anchor IDs come from Markdig** (`heading.GetAttributes().Id`), never from a
  hand-rolled slugifier — accented headings diverge otherwise and scroll sync breaks.
- **JsonSerializer rejects UTF-8 BOM** — `RecoveryService.TryReadSnapshot` trims BOM bytes
  before deserializing. Keep that when touching snapshot IO.
- **All `.ps1` scripts are UTF-8 WITH BOM** — Windows PowerShell 5.1 reads BOM-less files as
  ANSI and mangles accented strings. Keep the BOM when editing scripts.
- **Don't name a PowerShell function `R`** — collides with the `r` alias (Invoke-History).
- **WinExe has no stdout** — CLI feedback uses MessageBox + exit codes.
- **`portable.flag` must never ship inside the MSI** — build-msi.ps1 deletes it from the
  publish dir before `wix build`.
- **ToolBar overflow button** — hidden via `toolBar.Template.FindName("OverflowButton")` in
  `Loaded`; replacing the whole ToolBar template crashes (tried, reverted).
- **OneDrive locks fresh build outputs** — the repo lives under OneDrive. build scripts
  write to a temp dir and copy to `dist\` with retries; if a copy fails, retry after a pause.
- **XSHD front matter** — never use a multiline `^---$...^---$` Span: it swallows content
  between `---` horizontal rules.

## Useful commands

PowerShell may need dotnet on PATH first:
`$env:PATH = "C:\Program Files\dotnet;" + $env:PATH` (wix lives in
`$env:USERPROFILE\.dotnet\tools`).

- `dotnet run --project .\MarkLocal\MarkLocal.csproj` — run the app.
- `dotnet test .\MarkLocal.Tests\MarkLocal.Tests.csproj` — 80 tests, all green at v1.0.0.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\MarkLocal\scripts\build-portable.ps1` — portable ZIP.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\MarkLocal\scripts\build-msi.ps1` — MSIs
  (needs `dotnet tool install -g wix --version 5.0.2` + `wix extension add -g WixToolset.UI.wixext/5.0.2`).
- `gh release create vX.Y.Z dist\*.msi dist\*.zip --title ... --notes ...` — publish release.

## Sensitive config

- None. No secrets anywhere in the project. Update feed URL in settings is public.

## Pending / backlog

- Code signing certificate (removes SmartScreen warning).
- Host `updates.json` on kairis.es and point the default feed there; later: real
  auto-update (download+launch MSI), see `docs/update-strategy.md`.
- Optional bundled Mermaid/MathJax assets (currently user-supplied in `Assets/lib/`).
- German `Strg`/`Umschalt` accelerator labels.
- Possible online version (author's long-term idea, not planned).
- Spell checker — parked deliberately.
