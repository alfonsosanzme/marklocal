# Especificación del proyecto: editor y visor Markdown local para Windows

**Nombre provisional:** MarkLocal  
**Versión del documento:** 0.1  
**Fecha:** 2026-05-12  
**Objetivo:** construir una aplicación local para Windows que permita abrir, editar, previsualizar y guardar archivos Markdown de forma cómoda, rápida y sin depender de nube, cuentas ni servicios externos.

---

## 1. Resumen ejecutivo

MarkLocal será un editor de Markdown local para Windows inspirado en la experiencia básica de Typora: escritura limpia, vista previa inmediata, buen tratamiento de imágenes, tablas, listas, estructura del documento y modo oscuro. No se busca clonar Typora al 100% en la primera versión, porque su edición WYSIWYM en una sola superficie es compleja. La primera versión debe priorizar una solución robusta: editor a la izquierda, vista previa renderizada a la derecha, y modos alternativos de solo edición o solo lectura.

La idea central: **abrir un `.md`, escribir, ver el resultado al instante, guardar y exportar sin pelearse con la herramienta**. El Markdown ya tiene suficientes caracteres raros; la aplicación no debe añadir drama.

---

## 2. Referencia funcional: características básicas de Typora a tomar como inspiración

Typora destaca por estas ideas:

1. **Experiencia de lectura y escritura integrada.** Typora elimina la separación tradicional entre editor y preview, ocultando parte de la sintaxis Markdown y mostrando una vista viva del contenido.
2. **Interfaz minimalista.** Pocos elementos visibles, foco en el documento.
3. **Soporte Markdown amplio.** Encabezados, listas, tablas, bloques de código, imágenes, matemáticas, diagramas y estilos inline.
4. **Gestión cómoda de imágenes.** Mostrar imágenes en el documento, admitir arrastrar y soltar, rutas relativas y ajustes de tamaño.
5. **Herramientas de documento largo.** Tabla de contenidos, enlaces internos, panel de esquema, conteo de palabras y navegación por secciones.
6. **Funciones de productividad.** Atajos de teclado, modo oscuro, modo foco, modo máquina de escribir, autopareado de comillas/corchetes y gestión de archivos recientes.
7. **Integración con Windows.** Apertura desde línea de comandos, asociación de archivos, instalación por usuario, modo oscuro y finales de línea configurables.

Para MarkLocal v1 se recomienda imitar **la utilidad**, no necesariamente la implementación exacta.

---

## 3. Alcance del proyecto

### 3.1 Objetivo general

Crear una aplicación de escritorio para Windows que permita:

- Crear archivos Markdown.
- Abrir archivos `.md`, `.markdown`, `.txt`.
- Editar texto Markdown con resaltado básico.
- Ver previsualización HTML en tiempo real.
- Guardar cambios localmente.
- Insertar imágenes locales con rutas relativas.
- Navegar por el esquema del documento.
- Usar tema claro/oscuro.
- Exportar a HTML y, opcionalmente, imprimir/exportar a PDF.

### 3.2 No objetivos de la versión 1

Quedan fuera de v1:

- Clonar el modo Typora de edición/renderizado en una sola superficie.
- Sincronización en la nube.
- Colaboración en tiempo real.
- Sistema de plugins.
- Base de datos de notas tipo Obsidian.
- Inteligencia artificial integrada.
- Aplicación móvil.
- Compatibilidad perfecta con todos los dialectos Markdown existentes.

---

## 4. Usuarios previstos

### 4.1 Usuario principal

Persona que trabaja con documentación, apuntes, guiones, artículos, README, clases o documentación técnica y quiere editar Markdown en local sin abrir un IDE completo.

### 4.2 Casos de uso típicos

1. **Profesor/divulgador:** redacta materiales, guiones, apuntes y exporta a HTML/PDF.
2. **Desarrollador:** edita README, changelogs y documentación de proyectos.
3. **Estudiante:** toma apuntes locales con imágenes y tablas.
4. **Usuario técnico no programador:** quiere un editor limpio para escribir documentos estructurados.

---

## 5. Principios de diseño

1. **Local-first.** Todo funciona sin internet.
2. **Ligero.** Evitar Electron si el objetivo es reducir peso y requisitos.
3. **Predecible.** Lo que se guarda es Markdown plano, sin formato propietario.
4. **Rápido.** La app debe abrir rápido y no bloquearse con documentos medianos.
5. **Poco intrusivo.** La interfaz debe acompañar, no pedir protagonismo como actor secundario con complejo de Hamlet.
6. **Compatible.** Usar GitHub Flavored Markdown como base razonable.
7. **Seguro.** Renderizar Markdown sin permitir scripts peligrosos por defecto.

---

## 6. Plataforma y stack técnico recomendado

### 6.1 Plataforma objetivo

- Windows 10 y Windows 11.
- Arquitectura principal: x64.
- Sin cuenta de usuario, sin servidor y sin conexión obligatoria.

### 6.2 Stack recomendado

Opción recomendada para desarrollar con pocos requisitos:

- **Lenguaje:** C#.
- **Framework:** .NET 10 LTS o versión LTS vigente al iniciar el desarrollo.
- **UI:** WPF.
- **Editor de texto:** AvalonEdit.
- **Parser Markdown:** Markdig.
- **Vista previa HTML:** Microsoft Edge WebView2.
- **Resaltado de código en preview:** highlight.js o Prism.js, incluidos como archivos locales.
- **Sanitización HTML:** Ganss.Xss/HtmlSanitizer o política propia restrictiva.
- **Configuración:** archivo JSON en `%AppData%/MarkLocal/settings.json`.
- **Persistencia:** archivos Markdown normales; no usar base de datos en v1.

### 6.3 Por qué este stack

- WPF permite una aplicación Windows nativa sin arrastrar un navegador completo como hace Electron.
- AvalonEdit evita implementar desde cero un editor de texto decente.
- Markdig ofrece conversión Markdown a HTML en .NET.
- WebView2 permite renderizar HTML/CSS moderno dentro de una aplicación nativa.
- El resultado es suficientemente simple para mantenerlo, pero potente para crecer.

---

## 7. Requisitos funcionales

### 7.1 Gestión de archivos

**RF-01. Crear documento nuevo**

- El usuario podrá crear un documento vacío.
- El documento nuevo se mostrará como `Sin título.md` hasta que se guarde.
- Si hay cambios sin guardar, la app debe pedir confirmación antes de cerrar o abrir otro archivo.

**RF-02. Abrir archivo Markdown**

- La app permitirá abrir `.md`, `.markdown` y `.txt`.
- Codificación recomendada: UTF-8.
- Si se detecta otra codificación, se intentará abrir sin romper caracteres.

**RF-03. Guardar y guardar como**

- `Ctrl+S` guarda el archivo actual.
- `Ctrl+Shift+S` abre “Guardar como”.
- La app debe preservar finales de línea configurados: `LF` o `CRLF`.

**RF-04. Archivos recientes**

- Mantener lista de últimos 10 archivos abiertos.
- Guardar rutas en configuración local.
- Si un archivo ya no existe, mostrarlo como no disponible o retirarlo al intentar abrirlo.

**RF-05. Carpeta de trabajo**

- Permitir abrir una carpeta como espacio de trabajo.
- Mostrar árbol lateral filtrado por `.md`, `.markdown`, `.txt` e imágenes.
- Esta función puede ser de fase 2 si se quiere un MVP muy pequeño.

---

### 7.2 Editor Markdown

**RF-06. Edición de texto**

- Editor monoespaciado opcional o fuente configurable.
- Ajuste de línea activable/desactivable.
- Deshacer/rehacer.
- Cortar/copiar/pegar.
- Seleccionar todo.
- Buscar y reemplazar.

**RF-07. Resaltado de sintaxis Markdown**

- Resaltar encabezados, listas, citas, enlaces, imágenes, código inline y bloques de código.
- No es necesario que el resaltado sea perfecto en v1.

**RF-08. Atajos de formato**

- `Ctrl+B`: negrita.
- `Ctrl+I`: cursiva.
- `Ctrl+K`: insertar enlace.
- `Ctrl+Shift+C`: bloque de código.
- `Ctrl+Shift+L`: lista de tareas.
- `Ctrl+Alt+T`: insertar tabla básica.

**RF-09. Autopareado básico**

- Opcional en MVP.
- Autocompletar `()`, `[]`, `{}`, `""`, `''`, `` ` ``.
- Debe poder desactivarse.

---

### 7.3 Vista previa

**RF-10. Preview en tiempo real**

- Al editar, la vista previa se actualizará automáticamente.
- Usar debounce de 250-400 ms para evitar renderizar en cada pulsación.
- Si el documento es grande, renderizar en segundo plano.

**RF-11. Modos de visualización**

La app debe ofrecer tres modos:

1. **Edición:** solo editor.
2. **Dividido:** editor + preview.
3. **Lectura:** solo preview.

Modo dividido será el predeterminado en v1.

**RF-12. Scroll sincronizado**

- Cuando el usuario se desplace en el editor, la preview intentará seguir una posición equivalente.
- Esta función puede ser aproximada en v1.
- Si complica demasiado, dejarla para fase 2.

**RF-13. Enlaces internos**

- Los encabezados generarán anclas HTML.
- Clic en enlaces internos debe navegar dentro del preview.

**RF-14. Enlaces externos**

- Abrir enlaces externos en el navegador predeterminado.
- No permitir navegación externa dentro del WebView2 salvo configuración explícita.

---

### 7.4 Sintaxis Markdown soportada

El MVP debe soportar:

- Encabezados `#`, `##`, `###`...
- Párrafos.
- Negrita y cursiva.
- Tachado.
- Listas ordenadas y no ordenadas.
- Listas de tareas `- [ ]` y `- [x]`.
- Citas `>`.
- Código inline.
- Bloques de código con triple backtick.
- Tablas estilo GFM.
- Enlaces.
- Imágenes.
- Reglas horizontales.
- Front matter YAML tratado como bloque especial.
- Notas al pie, si Markdig lo permite fácilmente con extensiones.

Opcional en fase 2:

- Matemáticas con MathJax local.
- Diagramas Mermaid locales.
- Tabla de contenidos `[TOC]`.
- Callouts estilo GitHub/Obsidian.

---

### 7.5 Imágenes

**RF-15. Insertar imagen local**

- El usuario podrá insertar imagen desde menú o arrastrando al editor.
- La app generará sintaxis Markdown:

```md
![Texto alternativo](ruta/imagen.png)
```

**RF-16. Copiar imágenes a carpeta local**

- Si se arrastra una imagen externa al directorio del documento, preguntar:
  - usar ruta actual,
  - copiar a carpeta `assets/`,
  - cancelar.
- Recomendación predeterminada: copiar a `assets/` junto al archivo Markdown.

**RF-17. Rutas relativas**

- Usar rutas relativas siempre que sea posible.
- Evitar rutas absolutas tipo `C:\Users\...` salvo decisión expresa del usuario.

**RF-18. Pegar imagen desde portapapeles**

- Fase 2.
- Al pegar una imagen, guardarla como `assets/image-YYYYMMDD-HHMMSS.png` e insertar enlace Markdown.

---

### 7.6 Panel de esquema

**RF-19. Extraer encabezados**

- Analizar encabezados `#` a `######`.
- Mostrar árbol de secciones en un panel lateral derecho o izquierdo.
- Clic en una sección mueve el editor y la preview a esa posición.

**RF-20. Tabla de contenidos**

- Fase 2.
- Si el documento contiene `[TOC]`, renderizar una tabla de contenidos automática.

---

### 7.7 Conteo y estado del documento

**RF-21. Barra de estado**

Mostrar:

- Ruta del archivo o “sin guardar”.
- Estado: guardado / modificado.
- Palabras.
- Caracteres.
- Línea y columna.
- Modo actual: edición/dividido/lectura.

---

### 7.8 Temas y apariencia

**RF-22. Tema claro y oscuro**

- La app debe incluir tema claro y oscuro.
- Opción para seguir el tema del sistema.

**RF-23. CSS de preview**

- Incluir CSS propio para una lectura agradable.
- Ancho máximo recomendado del contenido: 760-900 px.
- Interlineado: 1.5-1.7.
- Permitir CSS personalizado en fase 2.

**RF-24. Zoom**

- `Ctrl+Plus`: aumentar tamaño.
- `Ctrl+Minus`: reducir tamaño.
- `Ctrl+0`: restaurar.

---

### 7.9 Exportación

**RF-25. Exportar a HTML**

- Exportar el documento como HTML.
- Incluir CSS embebido para que el archivo sea portable.
- Resolver imágenes mediante rutas relativas.

**RF-26. Exportar/imprimir a PDF**

- Fase 2 o 3.
- Usar impresión de WebView2 o diálogo de impresión del sistema.
- No introducir Pandoc como dependencia obligatoria en v1.

**RF-27. Exportar a DOCX**

- Fuera de v1.
- Puede añadirse más adelante mediante Pandoc opcional.

---

### 7.10 Integración con Windows

**RF-28. Asociación de archivos**

- Permitir que `.md` se abra con MarkLocal.
- Se puede gestionar desde el instalador o desde Windows.

**RF-29. Abrir desde línea de comandos**

Soportar:

```powershell
marklocal.exe archivo.md
marklocal.exe .
```

Si se pasa una carpeta, abrirla como espacio de trabajo.

**RF-30. Instalación por usuario**

- Instalador sin privilegios de administrador si es posible.
- Carpeta recomendada: `%LocalAppData%/Programs/MarkLocal/`.

**RF-31. Menú contextual “Nuevo Markdown”**

- Fase 2.
- Añadir entrada opcional al menú contextual de Windows.

---

## 8. Requisitos no funcionales

### 8.1 Rendimiento

- Arranque en frío razonable: objetivo inferior a 3 segundos en un equipo medio.
- Arranque en caliente: objetivo inferior a 1 segundo.
- Documentos de hasta 1 MB deben editarse con fluidez.
- Documentos de 5-10 MB deben ser utilizables, aunque con preview menos inmediata.

### 8.2 Privacidad

- No enviar contenido a internet.
- No telemetría en v1.
- No cuentas.
- No nube.
- Las fuentes externas, CSS o scripts deben ir empaquetados localmente.

### 8.3 Seguridad

- Por defecto, bloquear ejecución de JavaScript procedente del Markdown del usuario.
- Sanitizar HTML si se permite HTML inline.
- Abrir enlaces externos en navegador del sistema.
- No permitir que el preview navegue a sitios externos dentro de la app.

### 8.4 Accesibilidad

- Navegación por teclado.
- Tamaño de fuente configurable.
- Contraste suficiente en tema claro y oscuro.
- No depender solo del color para indicar errores o estados.

### 8.5 Mantenibilidad

- Código modular.
- Pocas dependencias.
- Configuración simple.
- Tests unitarios para parser, rutas de imágenes y guardado.

---

## 9. Arquitectura propuesta

### 9.1 Módulos

```text
MarkLocal.App
├─ UI
│  ├─ MainWindow
│  ├─ EditorView
│  ├─ PreviewView
│  ├─ OutlineView
│  └─ SettingsWindow
├─ Core
│  ├─ DocumentService
│  ├─ MarkdownService
│  ├─ PreviewService
│  ├─ ImageAssetService
│  ├─ ExportService
│  ├─ SettingsService
│  └─ RecoveryService
├─ Infrastructure
│  ├─ FileSystem
│  ├─ RecentFilesStore
│  └─ WindowsIntegration
└─ Assets
   ├─ preview.css
   ├─ preview-dark.css
   ├─ highlight.min.js
   └─ highlight-theme.css
```

### 9.2 Flujo de renderizado

1. Usuario escribe en AvalonEdit.
2. Se dispara evento de cambio.
3. Se espera debounce de 250-400 ms.
4. `MarkdownService` convierte Markdown a HTML con Markdig.
5. Se aplica sanitización o reglas de seguridad.
6. `PreviewService` inserta el HTML en una plantilla con CSS local.
7. WebView2 actualiza la preview.
8. Se actualiza esquema, conteo de palabras y estado de guardado.

### 9.3 Plantilla HTML de preview

La preview debe generarse como HTML completo:

```html
<!doctype html>
<html lang="es">
<head>
  <meta charset="utf-8">
  <meta http-equiv="Content-Security-Policy" content="default-src 'self' file: data:; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' file: data:;">
  <style>/* CSS embebido o local */</style>
</head>
<body>
  <main id="content">
    <!-- HTML generado desde Markdown -->
  </main>
</body>
</html>
```

---

## 10. Configuración

Archivo:

```text
%AppData%/MarkLocal/settings.json
```

Ejemplo:

```json
{
  "theme": "system",
  "fontFamily": "Consolas",
  "fontSize": 15,
  "previewFontSize": 16,
  "wordWrap": true,
  "defaultLineEnding": "CRLF",
  "autoSave": false,
  "autoSaveIntervalSeconds": 30,
  "copyImagesToAssets": true,
  "assetsFolderName": "assets",
  "recentFiles": []
}
```

---

## 11. MVP recomendado

Para no construir un castillo con foso, dragón y departamento legal propio, el MVP debe limitarse a esto:

1. Abrir/crear/guardar `.md`.
2. Editor con resaltado Markdown básico.
3. Preview HTML en tiempo real.
4. Tres modos: editor, dividido, lectura.
5. Tema claro/oscuro.
6. Insertar imagen local con ruta relativa.
7. Exportar a HTML.
8. Lista de archivos recientes.
9. Barra de estado con palabras, línea, columna y guardado.
10. Instalador simple para Windows.

---

## 12. Roadmap por fases

### Fase 0: Preparación

- Crear repositorio.
- Crear proyecto WPF.
- Añadir dependencias mínimas.
- Definir estilos base.
- Crear estructura de carpetas.

### Fase 1: Núcleo editor-preview

- Ventana principal.
- Editor AvalonEdit.
- Conversión Markdown con Markdig.
- Preview WebView2.
- Abrir/guardar archivo.
- Detección de cambios sin guardar.

### Fase 2: Usabilidad real

- Archivos recientes.
- Tema claro/oscuro.
- Barra de estado.
- Insertar imagen local.
- Exportar HTML.
- Atajos básicos.

### Fase 3: Documentos largos

- Panel de esquema.
- Buscar/reemplazar.
- Scroll sincronizado aproximado.
- Modo foco.
- Configuración visual.

### Fase 4: Pulido

- Instalador.
- Asociación `.md`.
- CLI básica.
- Exportar/imprimir PDF.
- Pruebas con documentos grandes.

---

## 13. Criterios de aceptación

### CA-01. Crear y guardar

Dado que el usuario abre la app, cuando escribe texto y pulsa `Ctrl+S`, entonces se debe guardar un archivo Markdown válido en disco.

### CA-02. Preview inmediata

Dado un documento abierto, cuando el usuario escribe `# Título`, entonces la preview debe mostrar un encabezado renderizado sin acción manual adicional.

### CA-03. Cambios sin guardar

Dado un documento modificado, cuando el usuario intenta cerrar la app, entonces se debe pedir guardar, descartar o cancelar.

### CA-04. Imagen local

Dado un documento guardado, cuando el usuario inserta una imagen, entonces la app debe generar una ruta relativa válida y la preview debe mostrar la imagen.

### CA-05. Modo oscuro

Dado que el usuario cambia a modo oscuro, entonces editor y preview deben aplicar colores oscuros coherentes.

### CA-06. Exportar HTML

Dado un documento Markdown, cuando el usuario exporta a HTML, entonces se genera un archivo `.html` que puede abrirse en un navegador y conserva estructura, estilos básicos e imágenes.

### CA-07. Sin internet

Dado que el equipo no tiene conexión, la app debe seguir permitiendo editar, previsualizar y exportar HTML.

---

## 14. Dependencias mínimas de desarrollo

- Visual Studio 2022 o superior, o SDK de .NET + editor de código.
- .NET LTS vigente.
- Paquetes NuGet:
  - `Markdig`
  - `Microsoft.Web.WebView2`
  - `AvalonEdit`
  - opcional: `HtmlSanitizer`

Comandos orientativos:

```powershell
dotnet new wpf -n MarkLocal -f net10.0-windows
cd MarkLocal
dotnet add package Markdig
dotnet add package Microsoft.Web.WebView2
dotnet add package AvalonEdit
```

Si se decide usar .NET 8 por disponibilidad local, ajustar el target a `net8.0-windows`, pero para un proyecto nuevo iniciado en 2026 conviene usar la LTS vigente.

---

## 15. Riesgos y decisiones técnicas

### Riesgo 1: intentar clonar Typora demasiado pronto

**Problema:** el modo WYSIWYM de Typora parece sencillo, pero técnicamente no lo es.  
**Mitigación:** empezar con modo dividido y añadir mejoras progresivas.

### Riesgo 2: preview lenta en documentos grandes

**Mitigación:** debounce, renderizado en segundo plano y desactivar actualización automática para documentos muy grandes.

### Riesgo 3: WebView2 no disponible en algunos Windows 10

**Mitigación:** instalador que compruebe WebView2 Runtime o lo incluya como prerrequisito.

### Riesgo 4: HTML inseguro dentro del Markdown

**Mitigación:** sanitizar o desactivar HTML inline por defecto.

### Riesgo 5: rutas de imágenes rotas

**Mitigación:** copiar imágenes a `assets/` por defecto y usar rutas relativas.

---

## 16. Backlog posterior a v1

- Pegar imagen desde portapapeles.
- Mermaid local.
- MathJax local.
- Exportación PDF más cuidada.
- Plantillas de documento.
- Snippets.
- Corrector ortográfico.
- Modo máquina de escribir.
- CSS personalizado.
- Panel de carpeta/proyecto.
- Búsqueda global en carpeta.
- Portabilidad a Linux/macOS con Avalonia o Tauri, si algún día apetece sufrir con elegancia.

---

## 17. Fuentes consultadas

- Typora, página oficial de características: https://typora.io/
- Typora Markdown Reference: https://support.typora.io/Markdown-Reference/
- Typora on Windows: https://support.typora.io/Typora-on-Windows/
- Typora Support index: https://support.typora.io/
- Microsoft Edge WebView2: https://learn.microsoft.com/en-us/microsoft-edge/webview2/
- WebView2 Runtime evergreen/fixed: https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/evergreen-vs-fixed-version
- Markdig: https://github.com/xoofx/markdig
- AvalonEdit: https://github.com/icsharpcode/AvalonEdit
- Microsoft .NET Lifecycle: https://learn.microsoft.com/en-us/lifecycle/products/microsoft-net-and-net-core
