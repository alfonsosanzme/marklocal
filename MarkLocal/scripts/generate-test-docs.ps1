#requires -Version 5.1
<#
.SYNOPSIS
    Genera documentos Markdown sinteticos de varios tamanos para probar el rendimiento de MarkLocal.

.DESCRIPTION
    Crea archivos .md con encabezados, parrafos, listas, bloques de codigo y tablas
    hasta alcanzar los tamanos indicados (por defecto 50 KB, 250 KB, 1 MB y 5 MB).

.PARAMETER OutputDirectory
    Carpeta de salida. Por defecto: .\test-docs en el directorio del script.

.PARAMETER SizesKB
    Lista de tamanos objetivo en KB. Por defecto: 50, 250, 1024, 5120.

.EXAMPLE
    .\generate-test-docs.ps1
    .\generate-test-docs.ps1 -OutputDirectory C:\Temp\md -SizesKB "100,500,2048"
#>

[CmdletBinding()]
param(
    [string]$OutputDirectory,
    [string]$SizesKB = "50,250,1024,5120"
)

$ErrorActionPreference = "Stop"
$ScriptDirectory = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $ScriptDirectory "test-docs" }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$SizesList = @(
    $SizesKB -split '[,\s]+' |
        Where-Object { $_ -match '^\d+$' } |
        ForEach-Object { [int]$_ }
)
if ($SizesList.Count -eq 0) {
    throw "No se reconoció ningún tamaño en -SizesKB '$SizesKB'. Usa por ejemplo: 50,250,1024"
}

$paragraphs = @(
    "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Vivamus a ipsum vel ligula commodo aliquam.",
    "Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam.",
    "Quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat duis aute irure.",
    "Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.",
    "Curabitur pretium tincidunt lacus. Nulla gravida orci a odio. Nullam varius, turpis et commodo pharetra."
)

$codeBlock = @'
```csharp
public static int Fibonacci(int n)
{
    if (n < 2) return n;
    int a = 0, b = 1;
    for (int i = 2; i <= n; i++)
    {
        (a, b) = (b, a + b);
    }
    return b;
}
```
'@

$table = @'
| Columna A | Columna B | Columna C | Columna D |
| --- | --- | --- | --- |
| valor 1 | valor 2 | valor 3 | valor 4 |
| dato 1  | dato 2  | dato 3  | dato 4  |
| 1.234   | 5.678   | 9.012   | 3.456   |
'@

function New-Section {
    param([int]$Index)
    $rand = Get-Random -Minimum 0 -Maximum $paragraphs.Length
    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine("## Seccion $Index")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine($paragraphs[$rand])
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("### Detalles")
    [void]$sb.AppendLine("")
    for ($i = 1; $i -le 6; $i++) {
        $extra = $paragraphs[(($i + $Index) % $paragraphs.Length)]
        [void]$sb.AppendLine("- Punto $i de la seccion ${Index} -- $extra")
    }
    [void]$sb.AppendLine("")
    if ($Index % 5 -eq 0) {
        [void]$sb.AppendLine($codeBlock)
        [void]$sb.AppendLine("")
    }
    if ($Index % 8 -eq 0) {
        [void]$sb.AppendLine($table)
        [void]$sb.AppendLine("")
    }
    return $sb.ToString()
}

foreach ($size in $SizesList) {
    $target = Join-Path $OutputDirectory "doc-${size}KB.md"
    if (Test-Path $target) { Remove-Item $target -Force }

    $sw = [System.IO.StreamWriter]::new($target, $false, [System.Text.UTF8Encoding]::new($false))
    try {
        $sw.WriteLine("# Documento de prueba de ${size} KB")
        $sw.WriteLine("")
        $sw.WriteLine("Generado por generate-test-docs.ps1 para medir rendimiento del editor, parser y vista previa.")
        $sw.WriteLine("")
        $targetBytes = $size * 1024
        $section = 1
        while ($sw.BaseStream.Length -lt $targetBytes) {
            $sw.Write((New-Section -Index $section))
            $section++
        }
    }
    finally {
        $sw.Close()
        $sw.Dispose()
    }
    $actualKB = [math]::Round((Get-Item $target).Length / 1KB, 1)
    Write-Host ("Generado {0} ({1} KB)" -f $target, $actualKB) -ForegroundColor Green
}

Write-Host ""
Write-Host ("Documentos disponibles en: {0}" -f $OutputDirectory) -ForegroundColor Cyan
Write-Host "Abrelos con MarkLocal y observa:" -ForegroundColor Cyan
Write-Host "  - Tiempo desde la apertura hasta la primera previsualizacion."
Write-Host "  - Fluidez al escribir mientras se renderiza la preview."
Write-Host "  - Memoria del proceso MarkLocal en el Administrador de Tareas."
