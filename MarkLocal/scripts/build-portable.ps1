#requires -Version 5.1
<#
.SYNOPSIS
    Construye la versión portable de MarkLocal: publica self-contained, deja un portable.flag
    y empaqueta todo en un ZIP que se puede descomprimir y ejecutar sin instalar.

.DESCRIPTION
    El ZIP resultante contiene MarkLocal.exe junto a su runtime, sus assets y un archivo
    portable.flag. Al arrancar, MarkLocal detecta ese flag y guarda configuración, borradores,
    plantillas y la caché de WebView2 dentro de la subcarpeta Data junto al ejecutable.
    No toca %AppData% ni %LocalAppData% ni el registro.

.PARAMETER OutputDirectory
    Carpeta donde dejar el ZIP. Por defecto: dist\ junto a la raíz del repositorio.

.PARAMETER SkipBuild
    Si se indica, no ejecuta dotnet publish (asume que el publish ya está hecho).

.PARAMETER Runtime
    Identificador de runtime (RID) que se pasa a dotnet publish. Por defecto: win-x64.

.EXAMPLE
    .\build-portable.ps1
    .\build-portable.ps1 -OutputDirectory C:\Releases -Runtime win-x64
#>

[CmdletBinding()]
param(
    [string]$OutputDirectory,
    [switch]$SkipBuild,
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$ScriptDirectory = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }
$projectRoot    = Split-Path -Parent $ScriptDirectory       # ...\MarkLocal
$repoRoot       = Split-Path -Parent $projectRoot           # ...\MarkDown Editor
$csproj         = Join-Path $projectRoot "MarkLocal.csproj"

if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $repoRoot "dist"
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

function Get-Version {
    param([string]$Csproj)
    $xml = [xml](Get-Content -Raw -Path $Csproj)
    $v = $xml.Project.PropertyGroup.Version
    if ($v -is [System.Array]) { $v = $v | Where-Object { $_ } | Select-Object -First 1 }
    if (-not $v) { $v = "0.1.0" }
    return $v
}

function Get-DotnetPath {
    $candidates = @(
        (Join-Path $env:ProgramFiles "dotnet\dotnet.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "dotnet\dotnet.exe"),
        "dotnet.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { return $c }
        $resolved = Get-Command $c -ErrorAction SilentlyContinue
        if ($resolved) { return $resolved.Source }
    }
    throw "No se encontró dotnet.exe en PATH ni en Program Files."
}

$version = Get-Version $csproj
$dotnet = Get-DotnetPath
$publishDir = Join-Path $projectRoot ("bin\Release\net8.0-windows\{0}\publish" -f $Runtime)

if (-not $SkipBuild) {
    Write-Host "Cerrando cualquier MarkLocal en ejecución..." -ForegroundColor Yellow
    Get-Process -Name "MarkLocal" -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 400

    Write-Host "Publicando self-contained ($Runtime)..." -ForegroundColor Cyan
    & $dotnet publish $csproj -c Release -r $Runtime --self-contained true -p:PublishSingleFile=false --nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish devolvió $LASTEXITCODE." }
}

if (-not (Test-Path (Join-Path $publishDir "MarkLocal.exe"))) {
    throw "No se encontró MarkLocal.exe en $publishDir. Lanza el publish manualmente o quita -SkipBuild."
}

Write-Host "Marcando como portable..." -ForegroundColor Cyan
$flag = Join-Path $publishDir "portable.flag"
$flagContent = @(
    "MarkLocal portable build",
    "Al iniciar, la app guarda configuracion, borradores, plantillas y la cache de WebView2 en .\Data junto a este archivo.",
    "Version: $version",
    "Generado: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
) -join [Environment]::NewLine
Set-Content -Path $flag -Value $flagContent -Encoding UTF8

# README rápido dentro del ZIP
$readme = @"
MarkLocal $version - portable
================================

1. Descomprime la carpeta en cualquier ubicacion donde tengas permiso de escritura
   (p.ej. C:\Apps\MarkLocal, una memoria USB, OneDrive personal, etc.).
2. Doble clic en MarkLocal.exe.
3. La carpeta Data se creara junto al exe con tu configuracion, borradores y plantillas.

Para deshacer:
- Cierra MarkLocal.
- Borra la carpeta entera. No queda nada en %AppData% ni en el registro.

Requisito invisible:
- Microsoft Edge WebView2 Runtime (en Windows 11 viene de fabrica).
  Si la previsualizacion no funciona, instalalo desde:
  https://go.microsoft.com/fwlink/p/?LinkId=2124703

Creado por Alfonso Sanz Lopez - Kairis (kairis.es).
Asistencia de desarrollo: Claude Opus 4.7.
"@
Set-Content -Path (Join-Path $publishDir "LEEME-portable.txt") -Value $readme -Encoding UTF8

$zipName = "MarkLocal-portable-{0}-v{1}.zip" -f $Runtime, $version
$zipPath = Join-Path $OutputDirectory $zipName
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Write-Host "Empaquetando ZIP..." -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -CompressionLevel Optimal -Force

$sizeMB = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host ""
Write-Host ("OK: {0} ({1} MB)" -f $zipPath, $sizeMB) -ForegroundColor Green
Write-Host "Para probar: descomprime el ZIP y ejecuta MarkLocal.exe."
