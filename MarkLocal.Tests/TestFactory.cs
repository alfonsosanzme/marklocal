using System;
using System.IO;
using MarkLocal.Core;

namespace MarkLocal.Tests;

internal static class TestFactory
{
    static TestFactory()
    {
        // Los tests validan textos del idioma base; cargar el inglés una sola vez.
        Loc.Initialize("en");
    }

    /// <summary>Fuerza la ejecución del constructor estático (inicializa Loc).</summary>
    public static void EnsureLocalization() { }

    public static SettingsService CreateIsolatedSettings()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "MarkLocal.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        return new SettingsService(tmp);
    }

    public static (SettingsService Settings, MarkdownService Markdown) CreateMarkdownPair(
        bool allowInlineHtml = false)
    {
        var settings = CreateIsolatedSettings();
        settings.Settings.AllowInlineHtml = allowInlineHtml;
        return (settings, new MarkdownService(settings));
    }

    public static string CreateTempDir()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "MarkLocal.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        return tmp;
    }
}
