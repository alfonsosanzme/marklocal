# Plan de distribución de MarkLocal

> **Estado actual (mayo 2026):** portable **hecho** y MSI **hecho y validado** con WiX Toolset v5. La estrategia de actualizaciones está documentada en [`update-strategy.md`](./update-strategy.md). La opción Inno Setup queda como alternativa de futuro si en algún momento se prefiere un `.exe` sobre un `.msi`.

Hoy MarkLocal ya tiene tres mecanismos de despliegue:

- `bin\Release\net8.0-windows\win-x64\publish\` con `dotnet publish --self-contained` (autocontenido, ~164 MB).
- `scripts\install.ps1` que copia ese publish a `%LocalAppData%\Programs\MarkLocal`, crea atajos, asocia `.md` y registra el desinstalador en *Configuración → Aplicaciones*.
- `scripts\uninstall.ps1` para revertirlo.

Lo que falta para "distribuible de verdad" son dos formatos que la gente espera:

1. **Versión portable**: un único `.zip` que se descomprime y se ejecuta sin tocar el sistema.
2. **Instalador formal**: un `.exe` (o `.msi`) que se distribuye y que el usuario instala con doble clic.

Este plan describe cómo llegar a ambos sin meter dependencias propietarias ni firmar nada todavía (lo dejo aparte porque la firma de código es un tema con coste económico distinto).

---

## 1. Portable ✅ HECHO

Se generó `scripts/build-portable.ps1` y `App.OnStartup` detecta `portable.flag`. Verificado en `%Temp%\MarkLocal-Portable-Test`: arranca sin tocar `%AppData%\MarkLocal` ni `%LocalAppData%\MarkLocal`, deja la configuración, los borradores, las plantillas y la caché de WebView2 dentro de `Data\` junto al exe. En la barra de estado aparece la etiqueta **MODO PORTABLE**.

Para regenerar el ZIP:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\MarkLocal\scripts\build-portable.ps1
```

Salida: `dist\MarkLocal-portable-win-x64-vX.Y.Z.zip` (~68 MB comprimido).

### Objetivo (referencia histórica)

Un `MarkLocal-portable-x64-vX.Y.Z.zip` que se descomprime en cualquier carpeta. Al ejecutar `MarkLocal.exe`:

- No escribe en `%LocalAppData%\Programs`.
- Mantiene la configuración junto al ejecutable (`./Data/settings.json`, `./Data/drafts/`, `./Data/templates/`) en lugar de en `%AppData%`.
- Detecta WebView2 Runtime; si falta, abre la URL de descarga oficial en el navegador.
- No toca el registro salvo que el usuario active explícitamente la asociación de `.md` desde Preferencias.

### Cambios de código necesarios

| Cambio | Ubicación | Esfuerzo |
| --- | --- | --- |
| Detección de "modo portable" mediante un archivo `portable.flag` en la carpeta del exe | `App.OnStartup` | Bajo |
| `SettingsService` y `RecoveryService` aceptan un directorio raíz alternativo (ya soportan path explícito) | `Core/SettingsService.cs`, `Core/RecoveryService.cs` | Muy bajo |
| `TemplateService` igual: aceptar ruta explícita | `Core/TemplateService.cs` | Muy bajo |
| Aviso visible "Modo portable" en la barra de estado | `MainWindow.xaml.cs` | Muy bajo |
| Documentación en README explicando el flag | `README.md` | Muy bajo |

### Cómo se generaría

```powershell
# 1) Publish
dotnet publish .\MarkLocal\MarkLocal.csproj `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:PublishReadyToRun=true

# 2) Marcador portable
New-Item -Path ".\bin\Release\net8.0-windows\win-x64\publish\portable.flag" -ItemType File

# 3) ZIP
Compress-Archive -Path ".\bin\Release\net8.0-windows\win-x64\publish\*" `
                 -DestinationPath ".\dist\MarkLocal-portable-x64-v0.1.0.zip"
```

Esto irá en un nuevo script: `scripts/build-portable.ps1`.

### Pros y contras

- 👍 Cero fricción para evaluar la app en cualquier máquina.
- 👍 Bueno para entornos corporativos restringidos (sin admin, sin escritura en perfil).
- 👍 Trivial de actualizar: descomprimir encima.
- 👎 Pesa ~165 MB por culpa del runtime self-contained. Trim no ayuda mucho con WPF.
- 👎 No hay menú Inicio ni asociación automática de `.md`.

---

## 2. Instalador formal

### Opciones evaluadas

| Tecnología | Pros | Contras | Veredicto |
| --- | --- | --- | --- |
| **Inno Setup** | Gratuito, ligero, scripts en Pascal, comunidad enorme, genera `.exe` autoejecutable, soporta instalación por usuario o por máquina | Requiere instalar el compilador (`iscc.exe`) en la máquina que construye | ✔ **Recomendado** |
| **WiX Toolset (v4 / Heat)** | Estándar del sector, MSI nativo, perfecto para despliegues empresariales | Curva más empinada, XML verboso, dependencia .NET extra para herramientas | Para más adelante si se quiere ofrecer MSI corporativo |
| **MSIX** | Modelo moderno, sandbox de Windows 10/11, auto-actualización vía Store o sideload | Pide firma de código sí o sí, AppContainer puede chocar con WebView2 user-data dir, despliegue empresarial necesita certificados | Aparcado |
| **Velopack / Squirrel** | Auto-update genial, instalador atómico | Otra dependencia .NET y formato propio | Descartado por ahora |

Iremos por **Inno Setup** como primera opción y dejamos WiX como camino MSI futuro.

### Diseño del instalador con Inno Setup

Archivo `installer/MarkLocal.iss`:

```iss
[Setup]
AppName=MarkLocal
AppVersion=0.1.0
AppPublisher=Kairis
AppPublisherURL=https://kairis.es
AppCopyright=© 2026 Alfonso Sanz López — Kairis
DefaultDirName={localappdata}\Programs\MarkLocal
DefaultGroupName=MarkLocal
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\dist
OutputBaseFilename=MarkLocal-setup-x64-v{#AppVersion}
SetupIconFile=..\MarkLocal\Assets\marklocal.ico
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Files]
Source: "..\MarkLocal\bin\Release\net8.0-windows\win-x64\publish\*"; \
    DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\MarkLocal"; Filename: "{app}\MarkLocal.exe"
Name: "{group}\Desinstalar MarkLocal"; Filename: "{uninstallexe}"
Name: "{commondesktop}\MarkLocal"; Filename: "{app}\MarkLocal.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Crear icono en el escritorio"; GroupDescription: "Atajos"; Flags: checkedonce
Name: "associate";   Description: "Asociar archivos .md y .markdown"; GroupDescription: "Integración con Windows"; Flags: checkedonce
Name: "shellnew";    Description: "Añadir ""Nuevo Markdown"" al menú contextual"; GroupDescription: "Integración con Windows"; Flags: unchecked

[Registry]
; Sólo si el usuario marca la tarea "associate".
Root: HKCU; Subkey: "Software\Classes\MarkLocal.md";                     ValueType: string; ValueName: "";        ValueData: "Documento Markdown"; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\MarkLocal.md\DefaultIcon";         ValueType: string; ValueName: "";        ValueData: """{app}\MarkLocal.exe"",0"; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\MarkLocal.md\shell\open\command";  ValueType: string; ValueName: "";        ValueData: """{app}\MarkLocal.exe"" ""%1"""; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\.md";                              ValueType: string; ValueName: "";        ValueData: "MarkLocal.md"; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\.markdown";                        ValueType: string; ValueName: "";        ValueData: "MarkLocal.md"; Tasks: associate
Root: HKCU; Subkey: "Software\Classes\.md\ShellNew";                     ValueType: string; ValueName: "FileName"; ValueData: "{userappdata}\MarkLocal\templates\shellnew.md"; Tasks: shellnew

[Code]
function NeedsWebView2Check(): Boolean;
begin
  Result := not (RegKeyExists(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}')
             or RegKeyExists(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}')
             or RegKeyExists(HKCU, 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}'));
end;

procedure InitializeWizard();
begin
  if NeedsWebView2Check then
    MsgBox('No se ha detectado el runtime de Microsoft Edge WebView2.'#13#10
         + 'La previsualización no funcionará hasta instalarlo.'#13#10
         + 'Tras la instalación, descárgalo desde:'#13#10
         + 'https://go.microsoft.com/fwlink/p/?LinkId=2124703', mbInformation, MB_OK);
end;
```

### Cómo se generaría

```powershell
# Requiere Inno Setup instalado en C:\Program Files (x86)\Inno Setup 6\ISCC.exe
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" .\installer\MarkLocal.iss
# Salida: .\dist\MarkLocal-setup-x64-v0.1.0.exe
```

Se podría meter como tarea de GitHub Actions con `chocolatey install innosetup` cuando se publique al fin un repo.

### Diferencias respecto a `install.ps1`

| Aspecto | `install.ps1` actual | Instalador Inno |
| --- | --- | --- |
| Distribución | Requiere clonar el repo o copiar el publish | Un único `.exe` descargable |
| Desinstalación | Otro `.ps1` o el panel de Configuración | Asistente clásico de Windows |
| Asociaciones / ShellNew | Lo hace siempre | Opcional, marcado por el usuario |
| WebView2 | Lo instala si falta | Avisa con MsgBox y enlaza |
| Privilegios | Sólo usuario | Usuario o, si el usuario lo decide, todos los usuarios |
| Mantener el script actual | Sí, para devs y entornos corp | Sí, complementarios |

---

## 3. Cosas que dejamos para después

- **Firma de código** (`signtool sign /a /tr http://timestamp.digicert.com ...`). Inno Setup acepta `SignTool=...`. Requiere certificado (EV o estándar). Hasta entonces SmartScreen marcará el instalador como "Editor desconocido"; el usuario tendrá que pulsar "Más información → Ejecutar de todos modos".
- **Auto-actualización**. Se puede añadir comprobando la versión publicada en un endpoint propio o GitHub Releases. Velopack/Sparkle son candidatos cuando haya certificado.
- **Internacionalización**. Hoy la UI está en español hardcoded. Si se distribuye fuera del entorno Kairis, plantearlo.

---

## 4. Pasos concretos cuando retomemos

1. Crear `scripts/build-portable.ps1` que haga publish + flag + zip.
2. Implementar lectura del flag `portable.flag` en `App.OnStartup` y reusar `SettingsService(string path)` / `RecoveryService(...)` / `TemplateService(...)`.
3. Mensaje "Modo portable" en barra de estado.
4. Validar arrancando desde una carpeta sin permisos en `%AppData%`.
5. Crear `installer/MarkLocal.iss` con el contenido de arriba.
6. Probar `ISCC.exe .\installer\MarkLocal.iss` y verificar el `.exe` resultante en una VM limpia.
7. Documentar ambos flujos en el README.

---

*Documento creado por Alfonso Sanz López (Kairis) con apoyo de Claude Opus 4.7.*
