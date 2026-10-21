# ADR-0004: Portable mode via a `portable.flag` sentinel file

- Status: Accepted
- Date: 2026-08

## Context

A portable distribution (USB sticks, locked-down machines) must not touch the registry or
the user profile, while the installed version should use standard Windows locations. One
binary should serve both.

## Decision

At startup the app checks for a `portable.flag` file next to `MarkLocal.exe`. If present,
every data root (settings, drafts, templates, WebView2 profile) resolves to `Data\` beside
the exe, and registry integration (file association, context menu) is skipped.
`build-portable.ps1` drops the flag into the ZIP; `build-msi.ps1` deletes it from the
publish dir so it can never ship inside the MSI.

## Consequences

- Single codebase/binary for both distributions; verified zero delta in `%AppData%` when
  running portable.
- Deleting the folder fully uninstalls the portable version.
- Anyone adding a new persisted data location must route it through the same
  portable-aware path resolution in `SettingsService`.
