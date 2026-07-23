#requires -Version 5.1
<#
.SYNOPSIS
    Desinstala MarkLocal para el usuario actual: archivos, atajos, asociación y entrada de uninstaller.

.PARAMETER InstallDirectory
    Carpeta donde está instalado. Por defecto se lee del registro de desinstalación.

.PARAMETER KeepSettings
    Si se indica, no borra la carpeta de configuración (%AppData%\MarkLocal).
#>

[CmdletBinding()]
param(
    [string]$InstallDirectory,
    [switch]$KeepSettings
)

$ErrorActionPreference = "Continue"

function Stop-MarkLocalProcesses {
    Get-Process -Name "MarkLocal" -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "Cerrando instancia previa (PID $($_.Id))..." -ForegroundColor Yellow
        Stop-Process -Id $_.Id -Force
    }
    Start-Sleep -Milliseconds 600
}

function Get-InstallDirectoryFromRegistry {
    $key = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\MarkLocal"
    if (Test-Path $key) {
        try { return (Get-ItemProperty -Path $key -Name "InstallLocation").InstallLocation } catch {}
    }
    return $null
}

function Remove-FileAssociation {
    $progId = "MarkLocal.md"
    $classesRoot = "HKCU:\Software\Classes"
    foreach ($ext in @(".md", ".markdown")) {
        $extKey = "$classesRoot\$ext"
        if (Test-Path $extKey) {
            $current = (Get-ItemProperty -Path $extKey -Name "(Default)" -ErrorAction SilentlyContinue)."(default)"
            if ($current -eq $progId) {
                Remove-ItemProperty -Path $extKey -Name "(Default)" -ErrorAction SilentlyContinue
            }
            $owp = "$extKey\OpenWithProgids"
            if (Test-Path $owp) {
                Remove-ItemProperty -Path $owp -Name $progId -ErrorAction SilentlyContinue
            }
        }
    }
    Remove-Item -Path "$classesRoot\$progId" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path "$classesRoot\Applications\MarkLocal.exe" -Recurse -Force -ErrorAction SilentlyContinue

    Add-Type -Namespace MarkLocalNative -Name Shell32 -MemberDefinition @"
[System.Runtime.InteropServices.DllImport("shell32.dll", SetLastError=true)]
public static extern void SHChangeNotify(int wEventId, uint uFlags, System.IntPtr dwItem1, System.IntPtr dwItem2);
"@ -ErrorAction SilentlyContinue
    try { [MarkLocalNative.Shell32]::SHChangeNotify(0x08000000, 0, [System.IntPtr]::Zero, [System.IntPtr]::Zero) } catch {}
}

function Remove-Shortcuts {
    $links = @(
        (Join-Path $env:AppData "Microsoft\Windows\Start Menu\Programs\MarkLocal.lnk"),
        (Join-Path ([Environment]::GetFolderPath("Desktop")) "MarkLocal.lnk")
    )
    foreach ($lnk in $links) {
        if (Test-Path $lnk) {
            Remove-Item -Path $lnk -Force -ErrorAction SilentlyContinue
            Write-Host "Atajo eliminado: $lnk" -ForegroundColor Green
        }
    }
}

if (-not $InstallDirectory) {
    $InstallDirectory = Get-InstallDirectoryFromRegistry
}

Stop-MarkLocalProcesses

if ($InstallDirectory -and (Test-Path $InstallDirectory)) {
    Write-Host "Eliminando archivos en $InstallDirectory..." -ForegroundColor Cyan
    try {
        Remove-Item -Path $InstallDirectory -Recurse -Force
        Write-Host "Carpeta eliminada." -ForegroundColor Green
    }
    catch {
        Write-Warning "No se pudieron eliminar todos los archivos: $($_.Exception.Message)"
    }
}
else {
    Write-Warning "No se ha indicado o detectado la carpeta de instalación; se omite el borrado de binarios."
}

Remove-Shortcuts
Remove-FileAssociation
Remove-Item -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\MarkLocal" -Recurse -Force -ErrorAction SilentlyContinue

if (-not $KeepSettings) {
    $configDir = Join-Path $env:AppData "MarkLocal"
    if (Test-Path $configDir) {
        Remove-Item -Path $configDir -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "Configuración eliminada." -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "MarkLocal desinstalado." -ForegroundColor Cyan
