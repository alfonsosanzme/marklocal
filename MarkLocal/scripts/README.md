# Scripts de instalación y pruebas

Estos scripts instalan, desinstalan y prueban **MarkLocal** en el perfil del usuario actual. No requieren privilegios de administrador: todo va a `%LocalAppData%\Programs\MarkLocal` y `HKCU` (registro por usuario).

## 1. Generar los binarios

Desde la raíz del proyecto (`MarkLocal/`):

```powershell
dotnet publish .\MarkLocal.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false
```

Esto deja todo lo necesario en `bin\Release\net8.0-windows\win-x64\publish` (no requiere .NET 8 Runtime preinstalado).

## 2. Instalar

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\install.ps1
```

Lo que hace:

- Comprueba si **WebView2 Runtime** está instalado; si falta, pregunta y lo descarga (modo silencioso por usuario).
- Copia los binarios a `%LocalAppData%\Programs\MarkLocal`.
- Crea un atajo en el **menú Inicio** y otro en el **escritorio**.
- Asocia las extensiones `.md` y `.markdown` con MarkLocal.
- Registra una entrada de desinstalación en *Configuración → Aplicaciones → Aplicaciones instaladas*.

Opciones:

| Flag | Efecto |
| --- | --- |
| `-SourceDirectory <ruta>` | Usar otra carpeta como origen (por defecto detecta la de `publish`). |
| `-InstallDirectory <ruta>` | Destino alternativo (`%LocalAppData%\Programs\MarkLocal` por defecto). |
| `-NoDesktopShortcut` | No crear icono en escritorio. |
| `-NoAssociation` | No asociar `.md`/`.markdown`. |
| `-SkipWebView2Check` | Saltarse la comprobación del runtime de WebView2. |

## 3. Desinstalar

La opción más cómoda: **Configuración → Aplicaciones → Aplicaciones instaladas → MarkLocal → Desinstalar**.

También se puede ejecutar manualmente el `uninstall.ps1` que queda copiado en la carpeta de instalación:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "$env:LocalAppData\Programs\MarkLocal\uninstall.ps1"
```

Por defecto borra binarios, atajos, asociación, entrada de desinstalación y la configuración en `%AppData%\MarkLocal`. Para preservar la configuración usa `-KeepSettings`.

## 4. Banco de pruebas con documentos grandes

`generate-test-docs.ps1` crea documentos Markdown sintéticos de varios tamaños para medir cómo se comporta el editor con texto largo:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\generate-test-docs.ps1
```

Por defecto deja `doc-50KB.md`, `doc-250KB.md`, `doc-1024KB.md` y `doc-5120KB.md` en `scripts\test-docs\`. Para personalizar:

```powershell
.\scripts\generate-test-docs.ps1 -OutputDirectory C:\Temp\md -SizesKB 100,500,2048
```

Qué mirar al abrirlos con MarkLocal:

- Tiempo desde abrir el archivo hasta ver la primera previsualización.
- Fluidez al escribir mientras la preview se actualiza (el debounce vive en `Settings → Preview → Debounce`).
- Memoria de `MarkLocal.exe` en el Administrador de Tareas.
- Para documentos por encima de 1–2 MB conviene aumentar el debounce o cambiar al modo *Edición* mientras se trabaja.
