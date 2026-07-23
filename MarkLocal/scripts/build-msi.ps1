#requires -Version 5.1
<#
.SYNOPSIS
    Genera un MSI de MarkLocal con WiX Toolset v5.

.DESCRIPTION
    1. Hace dotnet publish self-contained (a menos que se pase -SkipBuild).
    2. Invoca "wix build" con installer\wix\MarkLocal.wxs.
    3. Deja dist\MarkLocal-vX.Y.Z.msi.

    El MSI:
    - Instala en %LocalAppData%\Programs\MarkLocal (per-user, sin admin).
    - Crea atajos en menú Inicio (siempre), escritorio (opcional) y asociación .md
      (opcional). Las dos opcionales se eligen en el wizard.
    - Tiene MajorUpgrade activado: al instalarse sobre una versión anterior la
      desinstala primero.

.PARAMETER OutputDirectory
    Carpeta del .msi. Por defecto: dist\ junto a la raíz del repositorio.

.PARAMETER SkipBuild
    Si se indica, no ejecuta dotnet publish (asume que el publish ya está hecho).

.PARAMETER Runtime
    Identificador de runtime (RID) que se pasa a dotnet publish. Por defecto: win-x64.

.EXAMPLE
    .\build-msi.ps1
    .\build-msi.ps1 -SkipBuild
#>

[CmdletBinding()]
param(
    [string]$OutputDirectory,
    [switch]$SkipBuild,
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$ScriptDirectory = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }
$projectRoot    = Split-Path -Parent $ScriptDirectory
$repoRoot       = Split-Path -Parent $projectRoot
$csproj         = Join-Path $projectRoot "MarkLocal.csproj"
$wxsFile        = Join-Path $repoRoot "installer\wix\MarkLocal.wxs"
$iconFile       = Join-Path $projectRoot "Assets\marklocal.ico"

if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot "dist" }
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

function Get-WixPath {
    $userTools = Join-Path $env:USERPROFILE ".dotnet\tools\wix.exe"
    if (Test-Path $userTools) { return $userTools }
    $resolved = Get-Command wix -ErrorAction SilentlyContinue
    if ($resolved) { return $resolved.Source }
    throw "No se encontró WiX Toolset. Instálalo con: dotnet tool install -g wix --version 5.0.2"
}

$version    = Get-Version $csproj
$dotnet     = Get-DotnetPath
$wix        = Get-WixPath
$publishDir = Join-Path $projectRoot ("bin\Release\net8.0-windows\{0}\publish" -f $Runtime)

if (-not $SkipBuild) {
    Write-Host "Cerrando cualquier MarkLocal en ejecución..." -ForegroundColor Yellow
    Get-Process -Name "MarkLocal" -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 400

    Write-Host "Publicando self-contained ($Runtime)..." -ForegroundColor Cyan
    & $dotnet publish $csproj -c Release -r $Runtime --self-contained true -p:PublishSingleFile=false --nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish devolvió $LASTEXITCODE." }
}

# El portable.flag NO debe ir dentro del MSI.
$flagInPublish = Join-Path $publishDir "portable.flag"
if (Test-Path $flagInPublish) {
    Remove-Item $flagInPublish -Force
}

if (-not (Test-Path (Join-Path $publishDir "MarkLocal.exe"))) {
    throw "No se encontró MarkLocal.exe en $publishDir. Lanza el publish manualmente o quita -SkipBuild."
}

# WiX v5 valida que la versión sea x.y.z(.w). 0.1.0 vale; añadimos .0 si solo trae 2 partes.
$wixVersion = $version
$parts = $wixVersion.Split('.')
if ($parts.Length -eq 2) { $wixVersion = $version + ".0" }

$msiName = "MarkLocal-v$version.msi"
$msiPath = Join-Path $OutputDirectory $msiName
if (Test-Path $msiPath) { Remove-Item $msiPath -Force }

Write-Host "Construyendo MSI con WiX..." -ForegroundColor Cyan
Write-Host ("  Versión:     {0}" -f $wixVersion)
Write-Host ("  PublishDir:  {0}" -f $publishDir)
Write-Host ("  Icono:       {0}" -f $iconFile)
Write-Host ("  Salida:      {0}" -f $msiPath)
Write-Host ""

# Cargamos la extensión UI para WixUI_FeatureTree.
$wixArgs = @(
    "build",
    $wxsFile,
    "-arch", "x64",
    "-culture", "es-ES",
    "-d", "Version=$wixVersion",
    "-d", "PublishDir=$publishDir",
    "-d", "IconPath=$iconFile",
    "-ext", "WixToolset.UI.wixext",
    "-out", $msiPath
)

& $wix @wixArgs
if ($LASTEXITCODE -ne 0) { throw "wix build devolvió $LASTEXITCODE." }

$sizeMB = [math]::Round((Get-Item $msiPath).Length / 1MB, 1)
Write-Host ""
Write-Host ("OK: {0} ({1} MB)" -f $msiPath, $sizeMB) -ForegroundColor Green
Write-Host ""
Write-Host "Para instalar (silencioso): msiexec /i `"$msiPath`" /qb"
Write-Host "Para instalar (interactivo): doble clic en el .msi"
Write-Host "Para desinstalar: msiexec /x `"$msiPath`" /qb"
