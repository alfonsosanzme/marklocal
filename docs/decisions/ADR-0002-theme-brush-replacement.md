# ADR-0002: Theme switching by replacing whole brushes, not DynamicResource colors

- Status: Accepted
- Date: 2026-07

## Context

Light/dark theming was first implemented with `SolidColorBrush Color="{DynamicResource ...}"`
resources. Menus, popups and tooltips rendered with stale colors after a theme switch: WPF
freezes brushes used inside popup visual trees, so the dynamic Color binding silently stops
working. This produced the "white menus in dark mode" class of bugs.

## Decision

`ApplyTheme` in `MainWindow` replaces the application-level brush resources **entirely**
(new `SolidColorBrush`, then `Freeze()`), instead of mutating colors on existing brushes.
AvalonEdit syntax highlighting follows the same principle with two separate XSHD files
(`MarkdownHighlighting.xshd` / `.Dark.xshd`) reloaded on theme change.

## Consequences

- Theme switches (including live OS theme changes in "system" mode) update every surface,
  popups included.
- Any new themed color must be routed through `ApplyTheme`'s brush table — adding a
  DynamicResource-Color binding will regress popups.
- Frozen brushes are also a minor perf win.
