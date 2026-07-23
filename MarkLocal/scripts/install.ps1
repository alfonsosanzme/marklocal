#requires -Version 5.1
<#
.SYNOPSIS
    Instala MarkLocal en el perfil del usuario actual, sin necesidad de privilegios de administrador.

.DESCRIPTION
    Copia los binarios publicados a %LocalAppData%\Programs\MarkLocal, crea atajos en el menú Inicio
    (y opcionalmente en el escritorio), registra la asociación de archivos .md y .markdown para el usuario
    actual y crea la entrada de desinstalación en "Aplicaciones y características".

    Diseñado para ejecutarse contra la salida de:
        dotnet publish MarkLocal.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false

.PARAMETER SourceDirectory
    Carpeta que contiene los binarios publicados (publish). Por defecto: bin\Release\net8.0-windows\win-x64\publish.

.PARAMETER InstallDirectory
    Carpeta de destino. Por defecto: %LocalAppData%\Programs\MarkLocal.

.PARAMETER NoDesktopShortcut
    Si se indica, no crea atajo en el escritorio.

.PARAMETER NoAssociation
    Si se indica, no asocia .md/.markdown a MarkLocal.

.PARAMETER SkipWebView2Check
    Si se indica, no comprueba la presencia del runtime evergreen de WebView2.

.EXAMPLE
    .\install.ps1
    .\install.ps1 -InstallDirectory "D:\Apps\MarkLocal" -NoDesktopShortcut
#>

[CmdletBinding()]
param(
    [string]$SourceDirectory,
    [string]$InstallDirectory = (Join-Path $env:LocalAppData "Programs\MarkLocal"),
    [switch]$NoDesktopShortcut,
    [switch]$NoAssociation,
    [switch]$SkipWebView2Check
)

$ErrorActionPreference = "Stop"

$ScriptDirectory = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }

function Test-WebView2Runtime {
    # GUID del WebView2 evergreen runtime, instalado por usuario o por máquina.
    $clientGuid = "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"
    $paths = @(
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\$clientGuid",
        "HKLM:\SOFTWARE\Microsoft\EdgeUpdate\Clients\$clientGuid",
        "HKCU:\Software\Microsoft\EdgeUpdate\Clients\$clientGuid"
    )
    foreach ($p in $paths) {
        if (Test-Path $p) {
            $pv = (Get-ItemProperty -Path $p -Name "pv" -ErrorAction SilentlyContinue).pv
            if ($pv -and $pv -ne "0.0.0.0") { return $pv }
        }
    }
    return $null
}

function Install-WebView2Runtime {
    $bootstrapper = Join-Path $env:TEMP "MicrosoftEdgeWebview2Setup.exe"
    $url = "https://go.microsoft.com/fwlink/p/?LinkId=2124703"
    Write-Host "Descargando bootstrap de WebView2 desde $url..." -ForegroundColor Cyan
    try {
        Invoke-WebRequest -Uri $url -OutFile $bootstrapper -UseBasicParsing
    }
    catch {
        Write-Warning "No se pudo descargar el bootstrap de WebView2: $($_.Exception.Message)"
        Write-Warning "Instálalo manualmente desde: $url"
        return $false
    }
    Write-Host "Ejecutando el instalador de WebView2 (modo silencioso por usuario)..." -ForegroundColor Cyan
    $args = @("/silent", "/install")
    try {
        $proc = Start-Process -FilePath $bootstrapper -ArgumentList $args -Wait -PassThru
        if ($proc.ExitCode -ne 0) {
            Write-Warning "El instalador de WebView2 terminó con código $($proc.ExitCode)."
            return $false
        }
        return $true
    }
    catch {
        Write-Warning "No se pudo lanzar el instalador de WebView2: $($_.Exception.Message)"
        return $false
    }
    finally {
        Remove-Item $bootstrapper -Force -ErrorAction SilentlyContinue
    }
}

function Resolve-SourceDirectory {
    param([string]$Provided, [string]$ScriptDir)
    if ($Provided) {
        if (-not (Test-Path $Provided)) { throw "No existe la carpeta de origen: $Provided" }
        return (Resolve-Path $Provided).Path
    }
    $projectRoot = Split-Path -Parent $ScriptDir
    $candidate = Join-Path $projectRoot "bin\Release\net8.0-windows\win-x64\publish"
    if (Test-Path $candidate) { return $candidate }
    $candidate2 = Join-Path $projectRoot "bin\Release\net8.0-windows\publish"
    if (Test-Path $candidate2) { return $candidate2 }
    $candidate3 = Join-Path $projectRoot "bin\Debug\net8.0-windows"
    if (Test-Path $candidate3) {
        Write-Warning "No se encontró una carpeta publish. Usando $candidate3 (sólo válido si .NET 8 Runtime está instalado)."
        return $candidate3
    }
    throw "No se encontró ninguna carpeta de publicación. Ejecuta antes 'dotnet publish ... --self-contained true'."
}

function Stop-MarkLocalProcesses {
    Get-Process -Name "MarkLocal" -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "Cerrando instancia previa (PID $($_.Id))..." -ForegroundColor Yellow
        Stop-Process -Id $_.Id -Force
    }
    Start-Sleep -Milliseconds 600
}

function Copy-Files {
    param([string]$From, [string]$To)
    if (-not (Test-Path $To)) {
        New-Item -ItemType Directory -Path $To -Force | Out-Null
    }
    Write-Host "Copiando archivos a $To..." -ForegroundColor Cyan
    robocopy $From $To /MIR /NFL /NDL /NJH /NJS /NC /NS /NP | Out-Null
    if ($LASTEXITCODE -gt 7) { throw "robocopy devolvió el código $LASTEXITCODE." }
}

function New-Shortcut {
    param([string]$Path, [string]$TargetExe, [string]$Description, [string]$WorkingDir)
    $dir = Split-Path -Parent $Path
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($Path)
    $shortcut.TargetPath = $TargetExe
    $shortcut.WorkingDirectory = $WorkingDir
    $shortcut.IconLocation = "$TargetExe,0"
    $shortcut.Description = $Description
    $shortcut.Save()
}

function Set-FileAssociation {
    param([string]$Exe)

    $progId = "MarkLocal.md"
    $command = "`"$Exe`" `"%1`""
    $icon = "`"$Exe`",0"
    $classesRoot = "HKCU:\Software\Classes"

    New-Item -Path "$classesRoot\$progId" -Force | Out-Null
    Set-ItemProperty -Path "$classesRoot\$progId" -Name "(Default)" -Value "Documento Markdown"
    Set-ItemProperty -Path "$classesRoot\$progId" -Name "FriendlyTypeName" -Value "Documento Markdown"

    New-Item -Path "$classesRoot\$progId\DefaultIcon" -Force | Out-Null
    Set-ItemProperty -Path "$classesRoot\$progId\DefaultIcon" -Name "(Default)" -Value $icon

    New-Item -Path "$classesRoot\$progId\shell\open\command" -Force | Out-Null
    Set-ItemProperty -Path "$classesRoot\$progId\shell\open\command" -Name "(Default)" -Value $command

    foreach ($ext in @(".md", ".markdown")) {
        New-Item -Path "$classesRoot\$ext" -Force | Out-Null
        Set-ItemProperty -Path "$classesRoot\$ext" -Name "(Default)" -Value $progId
        New-Item -Path "$classesRoot\$ext\OpenWithProgids" -Force | Out-Null
        Set-ItemProperty -Path "$classesRoot\$ext\OpenWithProgids" -Name $progId -Value ([byte[]]@()) -Type Binary
    }

    $appKey = "$classesRoot\Applications\MarkLocal.exe"
    New-Item -Path $appKey -Force | Out-Null
    Set-ItemProperty -Path $appKey -Name "FriendlyAppName" -Value "MarkLocal"
    New-Item -Path "$appKey\shell\open\command" -Force | Out-Null
    Set-ItemProperty -Path "$appKey\shell\open\command" -Name "(Default)" -Value $command
    New-Item -Path "$appKey\SupportedTypes" -Force | Out-Null
    foreach ($ext in @(".md", ".markdown")) {
        Set-ItemProperty -Path "$appKey\SupportedTypes" -Name $ext -Value ""
    }

    Add-Type -Namespace MarkLocalNative -Name Shell32 -MemberDefinition @"
[System.Runtime.InteropServices.DllImport("shell32.dll", SetLastError=true)]
public static extern void SHChangeNotify(int wEventId, uint uFlags, System.IntPtr dwItem1, System.IntPtr dwItem2);
"@ -ErrorAction SilentlyContinue
    try { [MarkLocalNative.Shell32]::SHChangeNotify(0x08000000, 0, [System.IntPtr]::Zero, [System.IntPtr]::Zero) } catch {}
}

function Register-Uninstaller {
    param([string]$InstallDir, [string]$Version, [string]$UninstallScript)
    $uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\MarkLocal"
    New-Item -Path $uninstallKey -Force | Out-Null
    $exe = Join-Path $InstallDir "MarkLocal.exe"
    $uninstallCmd = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$UninstallScript`""
    Set-ItemProperty -Path $uninstallKey -Name "DisplayName" -Value "MarkLocal"
    Set-ItemProperty -Path $uninstallKey -Name "DisplayVersion" -Value $Version
    Set-ItemProperty -Path $uninstallKey -Name "Publisher" -Value "MarkLocal"
    Set-ItemProperty -Path $uninstallKey -Name "InstallLocation" -Value $InstallDir
    Set-ItemProperty -Path $uninstallKey -Name "DisplayIcon" -Value "$exe,0"
    Set-ItemProperty -Path $uninstallKey -Name "UninstallString" -Value $uninstallCmd
    Set-ItemProperty -Path $uninstallKey -Name "NoModify" -Value 1 -Type DWord
    Set-ItemProperty -Path $uninstallKey -Name "NoRepair" -Value 1 -Type DWord
}

$source = Resolve-SourceDirectory -Provided $SourceDirectory -ScriptDir $ScriptDirectory
$exeInSource = Join-Path $source "MarkLocal.exe"
if (-not (Test-Path $exeInSource)) {
    throw "No se encontró MarkLocal.exe en $source. Comprueba la carpeta de publicación."
}

if (-not $SkipWebView2Check) {
    $wvVersion = Test-WebView2Runtime
    if ($wvVersion) {
        Write-Host "WebView2 Runtime detectado (versión $wvVersion)." -ForegroundColor Green
    }
    else {
        Write-Warning "No se detectó el runtime de WebView2 (necesario para la vista previa)."
        $answer = Read-Host "¿Quieres descargarlo e instalarlo ahora? (S/n)"
        if ([string]::IsNullOrWhiteSpace($answer) -or $answer -match '^[sSyY]') {
            if (-not (Install-WebView2Runtime)) {
                Write-Warning "Continúa la instalación, pero la previsualización podría fallar hasta instalar WebView2 Runtime."
            }
            else {
                Write-Host "WebView2 instalado." -ForegroundColor Green
            }
        }
        else {
            Write-Host "Continúa sin WebView2. Recuerda instalarlo desde https://go.microsoft.com/fwlink/p/?LinkId=2124703 cuando puedas." -ForegroundColor Yellow
        }
    }
}

Stop-MarkLocalProcesses
Copy-Files -From $source -To $InstallDirectory

$exe = Join-Path $InstallDirectory "MarkLocal.exe"
$version = (Get-Item $exe).VersionInfo.ProductVersion
if (-not $version) { $version = "0.1.0" }

# Atajos
$startMenuShortcut = Join-Path $env:AppData "Microsoft\Windows\Start Menu\Programs\MarkLocal.lnk"
New-Shortcut -Path $startMenuShortcut -TargetExe $exe -Description "Editor Markdown local" -WorkingDir $InstallDirectory
Write-Host "Atajo creado en menú Inicio." -ForegroundColor Green

if (-not $NoDesktopShortcut) {
    $desktopShortcut = Join-Path ([Environment]::GetFolderPath("Desktop")) "MarkLocal.lnk"
    New-Shortcut -Path $desktopShortcut -TargetExe $exe -Description "Editor Markdown local" -WorkingDir $InstallDirectory
    Write-Host "Atajo creado en escritorio." -ForegroundColor Green
}

# Asociación
if (-not $NoAssociation) {
    Set-FileAssociation -Exe $exe
    Write-Host "Archivos .md y .markdown asociados a MarkLocal." -ForegroundColor Green
}

# Copia el uninstall.ps1 junto a la app y registra el uninstaller
$installedUninstall = Join-Path $InstallDirectory "uninstall.ps1"
$uninstallSource = Join-Path $ScriptDirectory "uninstall.ps1"
if (Test-Path $uninstallSource) {
    Copy-Item -Path $uninstallSource -Destination $installedUninstall -Force
    Register-Uninstaller -InstallDir $InstallDirectory -Version $version -UninstallScript $installedUninstall
    Write-Host "Desinstalador registrado en Aplicaciones y características." -ForegroundColor Green
}
else {
    Write-Warning "No se encontró uninstall.ps1; no se ha registrado el desinstalador."
}

Write-Host ""
Write-Host "Instalación completada en $InstallDirectory" -ForegroundColor Cyan
Write-Host "Lanza MarkLocal desde el menú Inicio o desde:" -ForegroundColor Cyan
Write-Host "    $exe"
