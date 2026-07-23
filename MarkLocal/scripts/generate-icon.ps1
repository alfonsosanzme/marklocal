#requires -Version 5.1
<#
.SYNOPSIS
    Genera Assets\marklocal.ico con varios tamanos (16, 32, 48, 64, 128, 256).

.DESCRIPTION
    Dibuja un cuadrado redondeado con color de acento y una "M" centrada en blanco.
    Los PNG se empaquetan en formato .ico moderno (PNGs embebidos para 256x256).

.EXAMPLE
    .\scripts\generate-icon.ps1
#>

[CmdletBinding()]
param(
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$ScriptDirectory = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }
$projectRoot = Split-Path -Parent $ScriptDirectory
if (-not $OutputPath) {
    $OutputPath = Join-Path $projectRoot "Assets\marklocal.ico"
}
$assetsDir = Split-Path -Parent $OutputPath
if (-not (Test-Path $assetsDir)) { New-Item -ItemType Directory -Path $assetsDir -Force | Out-Null }

$sizes = @(16, 32, 48, 64, 128, 256)
$pngs = @()

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode  = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    # Fondo cuadrado redondeado en color acento
    $accent = [System.Drawing.Color]::FromArgb(255, 42, 111, 190)
    $accentDark = [System.Drawing.Color]::FromArgb(255, 27, 78, 144)
    $r = [Math]::Max(1, [int][Math]::Round($s * 0.18))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $rect = New-Object System.Drawing.RectangleF 0, 0, $s, $s
    $path.AddArc(0, 0, $r*2, $r*2, 180, 90)
    $path.AddArc($s - $r*2, 0, $r*2, $r*2, 270, 90)
    $path.AddArc($s - $r*2, $s - $r*2, $r*2, $r*2, 0, 90)
    $path.AddArc(0, $s - $r*2, $r*2, $r*2, 90, 90)
    $path.CloseFigure()

    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, $accent, $accentDark, ([System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
    $g.FillPath($brush, $path)
    $brush.Dispose()

    # Letra M en blanco
    $fontSize = [single]($s * 0.62)
    $font = New-Object System.Drawing.Font "Segoe UI", $fontSize, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $fmt = New-Object System.Drawing.StringFormat
    $fmt.Alignment = [System.Drawing.StringAlignment]::Center
    $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
    # Ajuste vertical fino (la M de Segoe UI queda un poco baja)
    $textRect = New-Object System.Drawing.RectangleF 0, ($s * -0.04), $s, $s
    $g.DrawString("M", $font, [System.Drawing.Brushes]::White, $textRect, $fmt)
    $font.Dispose()
    $fmt.Dispose()

    # Acento inferior: línea fina para sugerir "documento"
    if ($s -ge 32) {
        $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(220, 255, 255, 255)), ([single]([Math]::Max(1, $s/64)))
        $y = $s * 0.82
        $left = $s * 0.28
        $right = $s * 0.72
        $g.DrawLine($pen, [single]$left, [single]$y, [single]$right, [single]$y)
        $pen.Dispose()
    }

    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += ,$ms.ToArray()
    $ms.Dispose()
    $bmp.Dispose()
}

# Construir el archivo .ico
$fs = [System.IO.File]::Create($OutputPath)
$bw = New-Object System.IO.BinaryWriter $fs
try {
    # Header: reserved(2) | type=1 icon(2) | count(2)
    $bw.Write([UInt16]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]$sizes.Count)

    $offset = 6 + ($sizes.Count * 16)
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $s = $sizes[$i]
        $w = if ($s -ge 256) { [byte]0 } else { [byte]$s }
        $h = $w
        $bw.Write($w)              # width
        $bw.Write($h)              # height
        $bw.Write([byte]0)         # palette colors
        $bw.Write([byte]0)         # reserved
        $bw.Write([UInt16]1)       # planes
        $bw.Write([UInt16]32)      # bits per pixel
        $bw.Write([UInt32]$pngs[$i].Length)
        $bw.Write([UInt32]$offset)
        $offset += $pngs[$i].Length
    }

    foreach ($png in $pngs) {
        $bw.Write($png)
    }
}
finally {
    $bw.Close()
    $fs.Close()
}

$kb = [Math]::Round((Get-Item $OutputPath).Length / 1KB, 1)
Write-Host ("Icono generado: {0} ({1} KB, {2} tamanos)" -f $OutputPath, $kb, $sizes.Count) -ForegroundColor Green
