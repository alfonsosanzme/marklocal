# Data model

No database. All persistent data is JSON or plain files on disk.

## Entities

### AppSettings (`Models/AppSettings.cs`)
- Purpose: every user preference, serialized as `settings.json`.
- Key fields: `theme` (light/dark/system), `language` ("auto" or en/es/de/fr/zh-Hans —
  "auto" resolves from the OS UI culture; change requires restart), editor font/size,
  line endings, auto-save interval, image folder, preview debounce, custom preview CSS path,
  update feed URL, last view mode, window placement, recent workspace root.
- Storage: `%AppData%\MarkLocal\settings.json` — or `Data\config\settings.json` in portable
  mode (presence of `portable.flag` next to the exe switches all data roots to `Data\`).

### DocumentModel (`Models/DocumentModel.cs`)
- Purpose: the open document — path, text, encoding, line-ending style, dirty flag.
- Encoding is preserved on save (UTF-8 with/without BOM, CP-1252).

### DraftSnapshot (`Models/DraftSnapshot.cs`)
- Purpose: crash-recovery snapshot of an unsaved/dirty document.
- Key fields: original file path (or null for untitled), content, timestamp, session PID.
- Storage: `%LocalAppData%\MarkLocal\drafts\` (or `Data\drafts\` portable), one file per
  snapshot, named by session/PID. Orphans (PID no longer running) trigger the recovery
  dialog on next start.
- Gotcha: snapshots are read as raw bytes and any UTF-8 BOM is trimmed before
  `JsonSerializer.Deserialize` — the serializer throws on BOM.

### HeadingNode (`Models/HeadingNode.cs`)
- Purpose: outline entry — level, text, **Markdig-generated anchor ID**, source line.
- The ID must come from `heading.GetAttributes().Id` (not a re-implemented slugifier),
  otherwise scroll sync breaks on accented headings.

### WorkspaceNode (`Models/WorkspaceNode.cs`)
- Purpose: node of the folder tree (folders + `.md`/`.markdown` files).

## i18n assets (contract)

`MarkLocal/Assets/i18n/<lang>/{mainui,maincode,views,core}.json` — flat string→string maps,
288 keys total per language, copied to output by the csproj. English is the base and the
fallback: a key missing in another language falls back to the English value at merge time.
Key namespaces follow `file.area.key` (e.g. `views.settings.langRestartTitle`). When adding
a key: add it to **all five languages** (en first), and keep placeholder tokens (`{0}`)
identical across languages.

## Storage summary

| Data | Installed | Portable |
| --- | --- | --- |
| Settings | `%AppData%\MarkLocal\settings.json` | `Data\config\settings.json` |
| Drafts | `%LocalAppData%\MarkLocal\drafts\` | `Data\drafts\` |
| Templates | `%AppData%\MarkLocal\templates\` | `Data\templates\` |
| WebView2 profile | `%LocalAppData%\MarkLocal\WebView2\` | `Data\WebView2\` |
