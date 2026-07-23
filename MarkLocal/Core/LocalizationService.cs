using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace MarkLocal.Core;

/// <summary>
/// Localización basada en archivos JSON planos (clave → texto) en Assets/i18n/&lt;código&gt;/*.json.
/// Todos los archivos .json de la carpeta del idioma se fusionan en un único diccionario,
/// lo que permite mantener los textos separados por área (main, views, core…).
/// El inglés actúa siempre como fallback para claves que falten en otros idiomas.
/// El idioma se fija al arrancar; cambiarlo requiere reiniciar la aplicación.
/// </summary>
public static class Loc
{
    private static Dictionary<string, string> _strings = new();
    private static Dictionary<string, string> _fallback = new();

    public const string AutoLanguage = "auto";

    public static string CurrentLanguage { get; private set; } = "en";

    /// <summary>Idiomas disponibles: código de carpeta y nombre nativo para el selector.</summary>
    public static readonly IReadOnlyList<(string Code, string NativeName)> Available = new[]
    {
        ("en", "English"),
        ("es", "Español"),
        ("de", "Deutsch"),
        ("fr", "Français"),
        ("zh-Hans", "中文（简体）")
    };

    public static void Initialize(string? settingValue)
    {
        string lang = ResolveLanguage(settingValue);
        _fallback = LoadLanguage("en");
        _strings = lang == "en" ? _fallback : LoadLanguage(lang);
        CurrentLanguage = lang;
    }

    /// <summary>Texto localizado. Si la clave no existe, cae al inglés; si tampoco, devuelve la clave.</summary>
    public static string T(string key)
    {
        if (_strings.TryGetValue(key, out var s)) return s;
        if (_fallback.TryGetValue(key, out var f)) return f;
        return key;
    }

    /// <summary>Texto localizado con argumentos de formato ({0}, {1}…).</summary>
    public static string T(string key, params object?[] args)
    {
        try { return string.Format(CultureInfo.CurrentCulture, T(key), args); }
        catch (FormatException) { return T(key); }
    }

    public static string ResolveLanguage(string? settingValue)
    {
        if (!string.IsNullOrWhiteSpace(settingValue)
            && !string.Equals(settingValue, AutoLanguage, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var (code, _) in Available)
            {
                if (string.Equals(code, settingValue, StringComparison.OrdinalIgnoreCase)) return code;
            }
        }

        // Automático: casar la cultura del sistema con los idiomas disponibles.
        var ui = CultureInfo.CurrentUICulture;
        string two = ui.TwoLetterISOLanguageName;
        if (two == "zh") return "zh-Hans";
        foreach (var (code, _) in Available)
        {
            if (string.Equals(code, two, StringComparison.OrdinalIgnoreCase)) return code;
        }
        return "en";
    }

    private static Dictionary<string, string> LoadLanguage(string code)
    {
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            string dir = Path.Combine(AppContext.BaseDirectory, "Assets", "i18n", code);
            if (!Directory.Exists(dir)) return merged;
            foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(file);
                    int start = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
                    var span = new ReadOnlySpan<byte>(bytes, start, bytes.Length - start);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(span);
                    if (dict == null) continue;
                    foreach (var kv in dict) merged[kv.Key] = kv.Value;
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
        return merged;
    }
}
