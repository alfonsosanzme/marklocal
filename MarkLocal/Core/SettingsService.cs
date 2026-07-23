using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarkLocal.Models;

namespace MarkLocal.Core;

public class SettingsService
{
    public string ConfigDirectory { get; }
    public string ConfigFilePath { get; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AppSettings Settings { get; private set; } = new();

    public event EventHandler? SettingsChanged;

    public SettingsService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MarkLocal"))
    {
    }

    public SettingsService(string configDirectory)
    {
        ConfigDirectory = configDirectory;
        ConfigFilePath = Path.Combine(ConfigDirectory, "settings.json");
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded != null)
                {
                    Settings = loaded;
                }
            }
        }
        catch
        {
            Settings = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            var json = JsonSerializer.Serialize(Settings, JsonOptions);
            File.WriteAllText(ConfigFilePath, json);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
        }
    }

    public void NotifyChanged() => SettingsChanged?.Invoke(this, EventArgs.Empty);
}
