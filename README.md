# MarkLocal

English | **[Español](README.es.md)**

<p>
  <img alt="Platform: Windows 10/11" src="https://img.shields.io/badge/platform-Windows%2010%2F11-0078D6">
  <img alt=".NET 8" src="https://img.shields.io/badge/.NET-8.0-512BD4">
  <img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-green">
  <img alt="Made with Claude Code" src="https://img.shields.io/badge/made%20with-Claude%20Code-D97757">
</p>

**A local Markdown editor and viewer for Windows.** No accounts, no cloud, no telemetry.

Created by **[Alfonso Sanz López](https://kairis.es)** at [Kairis](https://kairis.es) · developed with **[Claude Code](https://claude.com/claude-code)** (Anthropic), using various Claude models over the course of the project.

---

## Why it exists

More and more documentation lives in Markdown: notes, scripts, memos, READMEs, technical docs, exports from other tools… and it all ends up in folders full of `.md` files that Windows can't even preview. The usual options are opening a full IDE just to read two paragraphs, or relying on cloud editors with accounts and sync.

MarkLocal grew out of that specific need: **open a `.md`, read it properly, edit it fast, and move comfortably through whole folders of documents** — all local, lightweight, and without asking anyone's permission. What gets saved is plain Markdown; the tool doesn't add drama.

## Features

- **Editor + live preview** with bidirectional synchronized scrolling anchored to the first visible line.
- **Three modes**: editor only · split · read only. The last mode used is remembered.
- **Side panel** with the document outline, files in the same folder, and the workspace tree — navigable, resizable, and collapsible with a single click.
- **Per-folder workspaces**: open a folder, browse subfolders, change the root, go up to the parent.
- **Editing from the preview**: clickable task-list checkboxes and double-click to jump to that section in the editor.
- **Formatting toolbar** (bold, italic, headings, lists, tables, links…) and full keyboard shortcuts.
- **Two focus modes**: F11 (no panels, main buttons visible) and Shift+F11 (immersive full screen).
- **Light / dark / system theme**, with hot switching when Windows changes its theme.
- **Multilingual UI**: English, Spanish, German, French, and Simplified Chinese — auto-detected from the system, switchable in Preferences.
- **Export to HTML and PDF** (native via WebView2), print, custom preview CSS.
- **Automatic `[TOC]`**, YAML front matter, GFM tables, task lists, footnotes.
- **Auto-save and draft recovery** after an unexpected shutdown.
- **Templates** ("New from template") and snippets for front matter, callouts, and tables.
- **Optional Mermaid** (drop `mermaid.min.js` into `Assets/lib/` — see [instructions](MarkLocal/Assets/lib/README.md)).
- **Windows integration**: `.md`/`.markdown` file association, "New Markdown" in the context menu, opening from the command line.
- **Portable mode**: a ZIP that touches neither the registry nor the user profile.

## Installation

Download the latest version from [Releases](https://github.com/alfonsosanzme/marklocal/releases) — or build it yourself (below).

| Format | Who it's for | Notes |
| --- | --- | --- |
| **`MarkLocal-vX.Y.Z.msi`** | Everyday use | Double-click, guided installer, per-user install (no admin). Newer versions upgrade the previous one automatically. |
| **`MarkLocal-portable-win-x64-vX.Y.Z.zip`** | USB drives, locked-down machines, trying it without installing | Unzip and run. Everything stays in the `Data\` folder next to the exe. To uninstall, delete the folder. |

**Requirement**: [WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) (ships by default on Windows 11; the installer checks for it).

## Command-line usage

```text
marklocal.exe                          New document.
marklocal.exe archivo.md               Opens the file.
marklocal.exe C:\ruta\carpeta          Opens the folder as a workspace.
marklocal.exe --export-html doc.md     Exports to HTML without the UI.
                  --output out.html      Output path.
                  --theme light|dark     Theme of the exported HTML.
marklocal.exe --version | --help
```

## Main shortcuts

| | |
| --- | --- |
| `Ctrl+N` / `Ctrl+O` / `Ctrl+S` | New / open / save |
| `Ctrl+B` / `Ctrl+I` / `Ctrl+K` | Bold / italic / link |
| `Ctrl+Shift+C` / `Ctrl+Shift+L` / `Ctrl+Alt+T` | Code block / task list / table |
| `Ctrl+F` | Find and replace |
| `Ctrl+Shift+P` / `Ctrl+P` | Export PDF / print |
| `F11` / `Shift+F11` / `Esc` | Focus / full screen / exit |
| `Ctrl++` `Ctrl+-` `Ctrl+0` | Zoom |

## Building from source

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
git clone https://github.com/alfonsosanzme/marklocal.git
cd marklocal

# Build and run
dotnet run --project .\MarkLocal\MarkLocal.csproj

# Tests (80)
dotnet test .\MarkLocal.Tests\MarkLocal.Tests.csproj

# Build the portable ZIP
powershell -NoProfile -ExecutionPolicy Bypass -File .\MarkLocal\scripts\build-portable.ps1

# Build the MSI (requires WiX v5: dotnet tool install -g wix; wix extension add -g WixToolset.UI.wixext/5.0.2)
powershell -NoProfile -ExecutionPolicy Bypass -File .\MarkLocal\scripts\build-msi.ps1
```

### Structure

```
MarkLocal/                    # WPF application (.NET 8)
├── Core/                     # Services: Markdown, Preview, Document, Workspace, Recovery, Update…
├── Models/                   # AppSettings, DocumentModel, HeadingNode, WorkspaceNode…
├── Infrastructure/           # Recent files, Windows registry integration
├── Views/                    # Preferences, Find/Replace, Recovery
├── Resources/                # AvalonEdit highlighting (light and dark themes)
├── Assets/                   # Preview CSS, icon, optional libraries
└── scripts/                  # install / uninstall / build-portable / build-msi / utilities

MarkLocal.Tests/              # xUnit
installer/wix/                # MSI definition (WiX v5)
docs/                         # Distribution plan, update strategy
```

### Dependencies

[Markdig](https://github.com/xoofx/markdig) · [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) · [WebView2](https://learn.microsoft.com/en-us/microsoft-edge/webview2/) · [HtmlSanitizer](https://github.com/mganss/HtmlSanitizer)

## Configuration

`%AppData%\MarkLocal\settings.json` (or `Data\config\` in portable mode). Theme, fonts, line endings, auto-save, image folder, preview debounce, custom CSS, update feed… Recovery drafts go to `%LocalAppData%\MarkLocal\drafts\`.

## Privacy

Nothing leaves your machine. No telemetry, no accounts, no cloud. Rendering uses a strict CSP, and HTML embedded in the Markdown is sanitized by default.

## The project, in short

A short conversation with the author about where MarkLocal came from and where it's headed.

**When did you say "I need this tool"?**
Over the past few months I've had to get used to working with Markdown. At first I didn't see much point to it, but little by little I came to appreciate the advantage of carrying information in the cleanest, lightest format possible. As I took on more projects — and, above all, organized all the context for those projects into text files — it was obvious I had to move to Markdown. I searched around online and couldn't find a decent free option for something that seemed simple to me. So I decided to give it a shot.

**Why local and no cloud?**
Because what I wanted was to work with the files I have locally. All my working sessions are local, and then I sync them with GitHub, with my server over SSH, or whatever the case calls for. The idea was a fast, fluid program for handling information. That's why I added the outline of the `.md` files sitting in the same folder as the one you're viewing; that's why there's a focus mode and a read-only mode, so that when I'm working with dense text everything reads clearly; and that's why there's a button to open the folder you're browsing in Explorer, to move around the documentation surrounding the file.

**What was it like developing with Claude Code?**
I described my problem and how I wanted the program to work, and already in the first version — the first *one shot* — I got something that was almost there. It took several iterations and more technical specifications, and I spent a few months on a beta, but what surprised me was that it worked almost on the first try. It was the first Windows application I'd ever built, and that opened up a thousand possibilities for me.

**Who is it for?**
My goal is for it to be so simple and easy to use that anyone can pick it up, while still being genuinely useful for those of us who juggle lots of files. It's built to solve problems and to keep growing without too much fuss.

**And a year from now?**
I'd like tools like this one to already come built into Claude Code or Codex; and I'll probably end up making an online version. But those are things for later: right now the job is to focus on one project, finish it, and move on to the next — which is exactly the part I struggle with most.

## History and credits

- **Author and direction**: [Alfonso Sanz López](https://kairis.es) — [Kairis](https://kairis.es) (`kairis.es`).
- **Development**: built in pair-programming sessions with **Claude Code** (Anthropic), using various Claude models throughout development.
- The project started from a [functional specification](docs/especificacion.md) (in Spanish) inspired by the experience of using Typora, and grew iteratively: editor and preview, workspaces, draft recovery, MSI/portable distribution.

## Known limitations

- **Spell checker**: not integrated — AvalonEdit doesn't expose one and the alternatives add heavy dependencies. On the backlog.
- **Unsigned installer**: SmartScreen may show the "unknown publisher" warning until there's a code-signing certificate.

## License

[MIT](./LICENSE) © 2026 Alfonso Sanz López — Kairis. The code is free; keeping the attribution is appreciated.
