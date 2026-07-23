using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MarkLocal.Core;

public class TemplateInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

public class TemplateService
{
    public string TemplatesDirectory { get; }

    public TemplateService()
        : this(System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MarkLocal", "templates"))
    {
    }

    public TemplateService(string templatesDirectory)
    {
        TemplatesDirectory = templatesDirectory;
    }

    public void EnsureDefaultTemplates()
    {
        try { Directory.CreateDirectory(TemplatesDirectory); }
        catch { return; }

        if (Directory.EnumerateFiles(TemplatesDirectory, "*.md").Any()) return;

        // Nombre de archivo y contenido en el idioma activo. Loc.T(clave) SIN argumentos
        // no pasa por string.Format, así que los tokens de llaves dobles {{date}}, {{year}}…
        // del contenido se conservan intactos hasta ExpandTokens.
        WriteIfMissing(Loc.T("core.templates.notes.filename"),   Loc.T("core.templates.notes.content"));
        WriteIfMissing(Loc.T("core.templates.meeting.filename"), Loc.T("core.templates.meeting.content"));
        WriteIfMissing(Loc.T("core.templates.readme.filename"),  Loc.T("core.templates.readme.content"));
        WriteIfMissing(Loc.T("core.templates.article.filename"), Loc.T("core.templates.article.content"));
    }

    public IReadOnlyList<TemplateInfo> GetAvailable()
    {
        try
        {
            if (!Directory.Exists(TemplatesDirectory)) return Array.Empty<TemplateInfo>();
            return Directory.EnumerateFiles(TemplatesDirectory, "*.md")
                .Select(f => new TemplateInfo
                {
                    Name = System.IO.Path.GetFileNameWithoutExtension(f),
                    Path = f
                })
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<TemplateInfo>();
        }
    }

    public static string ExpandTokens(string content)
    {
        if (string.IsNullOrEmpty(content)) return content;
        var now = DateTime.Now;
        return content
            .Replace("{{date}}", now.ToString("yyyy-MM-dd"))
            .Replace("{{datetime}}", now.ToString("yyyy-MM-dd HH:mm"))
            .Replace("{{year}}", now.Year.ToString())
            .Replace("{{time}}", now.ToString("HH:mm"));
    }

    private void WriteIfMissing(string fileName, string content)
    {
        string target = System.IO.Path.Combine(TemplatesDirectory, fileName);
        if (File.Exists(target)) return;
        try
        {
            File.WriteAllText(target, content, new UTF8Encoding(false));
        }
        catch
        {
        }
    }
}
