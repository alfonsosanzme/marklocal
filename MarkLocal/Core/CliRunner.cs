using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using MarkLocal.Models;

namespace MarkLocal.Core;

public class CliRunner
{
    public enum Action
    {
        ShowUi,
        ExportHtml,
        ShowHelp,
        ShowVersion
    }

    public class ParsedArgs
    {
        public Action Action { get; set; } = Action.ShowUi;
        public string? InputPath { get; set; }
        public string? OutputPath { get; set; }
        public bool Dark { get; set; }
        public bool ForceTheme { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public static ParsedArgs Parse(string[] args)
    {
        var result = new ParsedArgs();
        if (args.Length == 0) return result;

        int i = 0;
        while (i < args.Length)
        {
            string a = args[i];
            switch (a)
            {
                case "--help" or "-h" or "/?":
                    result.Action = Action.ShowHelp;
                    return result;
                case "--version" or "-v":
                    result.Action = Action.ShowVersion;
                    return result;
                case "--export-html" or "-e":
                    result.Action = Action.ExportHtml;
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                    {
                        result.InputPath = args[++i];
                    }
                    else
                    {
                        result.ErrorMessage = "Falta la ruta del archivo Markdown tras --export-html.";
                        return result;
                    }
                    break;
                case "--output" or "-o":
                    if (i + 1 < args.Length)
                    {
                        result.OutputPath = args[++i];
                    }
                    else
                    {
                        result.ErrorMessage = "Falta la ruta tras --output.";
                        return result;
                    }
                    break;
                case "--theme":
                    if (i + 1 < args.Length)
                    {
                        string theme = args[++i].ToLowerInvariant();
                        if (theme == "dark") { result.Dark = true; result.ForceTheme = true; }
                        else if (theme == "light") { result.Dark = false; result.ForceTheme = true; }
                        else { result.ErrorMessage = "Tema desconocido. Usa light o dark."; return result; }
                    }
                    break;
                default:
                    if (a.StartsWith("-"))
                    {
                        result.ErrorMessage = "Opción desconocida: " + a;
                        return result;
                    }
                    if (result.InputPath == null && result.Action == Action.ShowUi)
                    {
                        result.InputPath = a;
                    }
                    break;
            }
            i++;
        }
        return result;
    }

    public static string GetHelpText() =>
@"MarkLocal — editor y visor Markdown local para Windows.

Uso:
  marklocal.exe                          Abre la app con un documento nuevo.
  marklocal.exe <archivo.md>             Abre el archivo en la interfaz.
  marklocal.exe --export-html <archivo>  Convierte a HTML sin interfaz.
                  [--output ruta.html]   Ruta de salida (por defecto junto al .md).
                  [--theme light|dark]   CSS a aplicar al HTML exportado.
  marklocal.exe --version                Muestra la versión.
  marklocal.exe --help                   Muestra esta ayuda.
";

    public static string GetVersionText() =>
        "MarkLocal " + (typeof(CliRunner).Assembly.GetName().Version?.ToString() ?? "0.1.0");

    public static async Task<int> RunExportHtmlAsync(ParsedArgs parsed, SettingsService settings, MarkdownService markdown, PreviewService preview, ExportService export)
    {
        if (string.IsNullOrEmpty(parsed.InputPath) || !File.Exists(parsed.InputPath))
        {
            MessageBox.Show($"El archivo no existe: {parsed.InputPath}", "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Warning);
            return 2;
        }

        string outputPath = parsed.OutputPath ?? Path.ChangeExtension(parsed.InputPath, ".html");
        bool dark = parsed.ForceTheme && parsed.Dark;

        try
        {
            byte[] bytes = await File.ReadAllBytesAsync(parsed.InputPath);
            Encoding encoding = DocumentService.DetectEncoding(bytes);
            string content = encoding.GetString(bytes).TrimStart('﻿');
            string baseDir = Path.GetDirectoryName(Path.GetFullPath(parsed.InputPath)) ?? Environment.CurrentDirectory;
            await export.ExportHtmlAsync(content, baseDir, outputPath, dark, settings.Settings.PreviewFontSize);
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error al exportar HTML:\n" + ex.Message, "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Error);
            return 3;
        }
    }
}
