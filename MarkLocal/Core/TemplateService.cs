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

        WriteIfMissing("Apuntes.md", DefaultTemplates.Apuntes);
        WriteIfMissing("Reunion.md", DefaultTemplates.Reunion);
        WriteIfMissing("README del proyecto.md", DefaultTemplates.Readme);
        WriteIfMissing("Articulo.md", DefaultTemplates.Articulo);
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

internal static class DefaultTemplates
{
    public const string Apuntes =
@"# Título de los apuntes

> Fecha: {{date}}

## Resumen

Una o dos frases sobre el contenido.

## Conceptos clave

- Concepto 1
- Concepto 2
- Concepto 3

## Notas

Texto libre.

## Referencias

- Fuente 1
- Fuente 2
";

    public const string Reunion =
@"# Reunión — {{date}}

## Asistentes

- Persona 1
- Persona 2

## Agenda

1. Punto 1
2. Punto 2

## Acuerdos

- Acuerdo 1
- Acuerdo 2

## Tareas

- [ ] Tarea 1 — responsable
- [ ] Tarea 2 — responsable

## Próxima reunión

Fecha y lugar.
";

    public const string Readme =
@"# Nombre del proyecto

Una frase que explique de qué va.

## Instalación

```bash
# pasos
```

## Uso

```bash
# ejemplo
```

## Configuración

Variables, archivos, opciones.

## Desarrollo

Cómo compilar, probar, contribuir.

## Licencia

MIT (o la que toque).
";

    public const string Articulo =
@"---
title: Título del artículo
date: {{date}}
tags: []
---

# Título del artículo

> Una idea fuerza en una frase.

## Introducción

Plantea el problema.

## Desarrollo

Explica con ejemplos.

## Conclusiones

Cierra y sintetiza.

## Referencias

1. Fuente.
";
}
