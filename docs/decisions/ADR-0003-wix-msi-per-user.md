# ADR-0003: WiX v5 per-user MSI with fixed UpgradeCode, one MSI per culture

- Status: Accepted
- Date: 2026-08

## Context

Distribution needed a proper installer with clean upgrades, no admin rights, optional
desktop shortcut and `.md` association, and (since v1.0.0) a localized install wizard.
Candidates: MSIX (store-oriented, signing hurdles), Inno Setup (no MSI semantics), WiX MSI.

## Decision

WiX Toolset v5 (global dotnet tool 5.0.2 + `WixToolset.UI.wixext`), `installer/wix/MarkLocal.wxs`:

- **UpgradeCode is fixed forever**: `1C9CE61E-67A1-453C-9869-C4BF713037DC`. `MajorUpgrade`
  uninstalls the previous version automatically (verified 0.1.0 → 0.1.1 in place).
- Per-user scope (`%LocalAppData%\Programs\MarkLocal`), no admin, `WixUI_FeatureTree` wizard.
- Installer UI localization via per-culture `.wxl` files; `build-msi.ps1` builds **one MSI
  per culture** (`MarkLocal-vX.Y.Z-en.msi`, `-es.msi`). The app itself is always
  multilingual regardless of which MSI installed it.
- `ProductCode` auto-regenerates per version; `Version` is read from the csproj.

## Consequences

- Upgrades are a plain double-click on the newer MSI; see `docs/update-strategy.md` for the
  full update roadmap (feed JSON → notify → later auto-update).
- **Never change the UpgradeCode** — doing so orphans existing installs.
- Adding an installer language = new `.wxl` + adding the culture to `-Cultures`.
- MSIs are unsigned for now (SmartScreen warning) — pending certificate.
