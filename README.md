# MarkLocal

<p>
  <img alt="Plataforma" src="https://img.shields.io/badge/plataforma-Windows%2010%2F11-0078D6">
  <img alt=".NET 8" src="https://img.shields.io/badge/.NET-8.0-512BD4">
  <img alt="Licencia MIT" src="https://img.shields.io/badge/licencia-MIT-green">
  <img alt="Hecho con Claude Code" src="https://img.shields.io/badge/hecho%20con-Claude%20Code-D97757">
</p>

**Editor y visor Markdown local para Windows.** Sin cuentas, sin nube, sin telemetría.

Creado por **[Alfonso Sanz López](https://kairis.es)** en [Kairis](https://kairis.es) · desarrollado con **[Claude Code](https://claude.com/claude-code)** (Anthropic), con distintos modelos de la familia Claude a lo largo del proyecto.

---

## Por qué existe

Cada vez más documentación vive en Markdown: apuntes, guiones, circulares, README, documentación técnica, exportaciones de otras herramientas… y acaban en carpetas llenas de archivos `.md` que Windows no sabe ni previsualizar. Las opciones habituales son abrir un IDE completo para leer dos párrafos, o depender de editores en la nube con cuentas y sincronización.

MarkLocal nace de esa necesidad concreta: **abrir un `.md`, leerlo bien, editarlo rápido y moverse con soltura por carpetas enteras de documentos** — todo local, ligero y sin pedir permiso a nadie. Lo que se guarda es Markdown plano; la herramienta no añade drama.

## Características

- **Editor + vista previa en tiempo real** con scroll sincronizado bidireccional anclado a la primera línea visible.
- **Tres modos**: solo editor · dividido · solo lectura. El último modo usado se recuerda.
- **Panel lateral** con esquema del documento, archivos de la misma carpeta y árbol del espacio de trabajo — navegable, redimensionable y plegable con un clic.
- **Espacios de trabajo por carpeta**: abre una carpeta, navega subcarpetas, cámbiala de raíz, sube al padre.
- **Edición desde la vista previa**: checkboxes de listas de tareas clicables y doble clic para saltar al editor en esa sección.
- **Barra de formato** (negrita, cursiva, encabezados, listas, tablas, enlaces…) y atajos de teclado completos.
- **Dos modos foco**: F11 (sin paneles, con botones principales) y Mayús+F11 (pantalla completa inmersiva).
- **Tema claro / oscuro / sistema**, con cambio en caliente cuando Windows cambia de tema.
- **Exportar a HTML y PDF** (nativo vía WebView2), imprimir, CSS personalizado del preview.
- **`[TOC]` automático**, front matter YAML, tablas GFM, listas de tareas, notas al pie.
- **Auto-guardado y recuperación de borradores** tras un cierre inesperado.
- **Plantillas** ("Nuevo desde plantilla") y snippets de front matter, callouts y tablas.
- **Mermaid opcional** (colocando `mermaid.min.js` en `Assets/lib/` — ver [instrucciones](MarkLocal/Assets/lib/README.md)).
- **Integración con Windows**: asociación de `.md`/`.markdown`, "Nuevo Markdown" en el menú contextual, apertura por línea de comandos.
- **Modo portable**: un ZIP que no toca ni el registro ni el perfil del usuario.

## Instalación

Descarga la última versión desde [Releases](https://github.com/alfonsosanzme/marklocal/releases) — o compílala tú mismo (abajo).

| Formato | Para quién | Notas |
| --- | --- | --- |
| **`MarkLocal-vX.Y.Z.msi`** | Uso normal | Doble clic, asistente en español, instalación por usuario (sin admin). Las versiones nuevas actualizan la anterior automáticamente. |
| **`MarkLocal-portable-win-x64-vX.Y.Z.zip`** | USB, equipos restringidos, probar sin instalar | Descomprime y ejecuta. Todo queda en la carpeta `Data\` junto al exe. Para desinstalar, borra la carpeta. |

**Requisito**: [WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) (incluido de fábrica en Windows 11; el instalador lo comprueba).

## Uso desde línea de comandos

```text
marklocal.exe                          Documento nuevo.
marklocal.exe archivo.md               Abre el archivo.
marklocal.exe C:\ruta\carpeta          Abre la carpeta como espacio de trabajo.
marklocal.exe --export-html doc.md     Exporta a HTML sin interfaz.
                  --output out.html      Ruta de salida.
                  --theme light|dark     Tema del HTML exportado.
marklocal.exe --version | --help
```

## Atajos principales

| | |
| --- | --- |
| `Ctrl+N` / `Ctrl+O` / `Ctrl+S` | Nuevo / abrir / guardar |
| `Ctrl+B` / `Ctrl+I` / `Ctrl+K` | Negrita / cursiva / enlace |
| `Ctrl+Shift+C` / `Ctrl+Shift+L` / `Ctrl+Alt+T` | Bloque de código / lista de tareas / tabla |
| `Ctrl+F` | Buscar y reemplazar |
| `Ctrl+Shift+P` / `Ctrl+P` | Exportar PDF / imprimir |
| `F11` / `Mayús+F11` / `Esc` | Foco / pantalla completa / salir |
| `Ctrl++` `Ctrl+-` `Ctrl+0` | Zoom |

## Compilar desde el código

Necesitas el [SDK de .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
git clone https://github.com/alfonsosanzme/marklocal.git
cd marklocal

# Compilar y ejecutar
dotnet run --project .\MarkLocal\MarkLocal.csproj

# Tests (80)
dotnet test .\MarkLocal.Tests\MarkLocal.Tests.csproj

# Generar el ZIP portable
powershell -NoProfile -ExecutionPolicy Bypass -File .\MarkLocal\scripts\build-portable.ps1

# Generar el MSI (requiere WiX v5: dotnet tool install -g wix; wix extension add -g WixToolset.UI.wixext/5.0.2)
powershell -NoProfile -ExecutionPolicy Bypass -File .\MarkLocal\scripts\build-msi.ps1
```

### Estructura

```
MarkLocal/                    # Aplicación WPF (.NET 8)
├── Core/                     # Servicios: Markdown, Preview, Document, Workspace, Recovery, Update…
├── Models/                   # AppSettings, DocumentModel, HeadingNode, WorkspaceNode…
├── Infrastructure/           # Archivos recientes, integración con el registro de Windows
├── Views/                    # Preferencias, Buscar/Reemplazar, Recuperación
├── Resources/                # Resaltado AvalonEdit (temas claro y oscuro)
├── Assets/                   # CSS del preview, icono, librerías opcionales
└── scripts/                  # install / uninstall / build-portable / build-msi / utilidades

MarkLocal.Tests/              # xUnit
installer/wix/                # Definición del MSI (WiX v5)
docs/                         # Plan de distribución, estrategia de actualizaciones
```

### Dependencias

[Markdig](https://github.com/xoofx/markdig) · [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) · [WebView2](https://learn.microsoft.com/en-us/microsoft-edge/webview2/) · [HtmlSanitizer](https://github.com/mganss/HtmlSanitizer)

## Configuración

`%AppData%\MarkLocal\settings.json` (o `Data\config\` en modo portable). Tema, fuentes, finales de línea, auto-guardado, carpeta de imágenes, debounce del preview, CSS personalizado, feed de actualizaciones… Los borradores de recuperación van a `%LocalAppData%\MarkLocal\drafts\`.

## Privacidad

Nada sale de tu equipo. Sin telemetría, sin cuentas, sin nube. El renderizado usa una CSP estricta y el HTML embebido en el Markdown se sanitiza por defecto.

## El proyecto, en corto

<!-- ENTREVISTA: pendiente de completar con las respuestas del autor -->
*Sección en preparación: una breve entrevista con el autor sobre el origen y el rumbo del proyecto.*

## Historia y créditos

- **Autor y dirección**: [Alfonso Sanz López](https://kairis.es) — [Kairis](https://kairis.es) (`kairis.es`).
- **Desarrollo**: construido en sesiones de pair-programming con **Claude Code** (Anthropic), utilizando distintos modelos de la familia Claude a lo largo del desarrollo.
- El proyecto partió de una [especificación funcional](docs/especificacion.md) inspirada en la experiencia de uso de Typora, y creció iterativamente: editor y preview, espacios de trabajo, recuperación de borradores, distribución MSI/portable.

## Limitaciones conocidas

- **Corrector ortográfico**: no integrado — AvalonEdit no lo expone y las alternativas añaden dependencias pesadas. En el backlog.
- **Instalador sin firma de código**: SmartScreen puede mostrar el aviso de "editor desconocido" hasta que haya certificado.

## Licencia

[MIT](./LICENSE) © 2026 Alfonso Sanz López — Kairis. El código es libre; se agradece conservar la atribución.
