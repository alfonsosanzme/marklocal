using System.Collections.Generic;
using System.IO;
using System.Linq;
using MarkLocal.Core;

namespace MarkLocal.Infrastructure;

public class RecentFilesStore
{
    private const int MaxItems = 10;
    private readonly SettingsService _settings;

    public RecentFilesStore(SettingsService settings)
    {
        _settings = settings;
    }

    public IReadOnlyList<string> GetAll() => _settings.Settings.RecentFiles;

    public void Add(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath)) return;
        var list = _settings.Settings.RecentFiles;
        list.RemoveAll(p => string.Equals(p, fullPath, System.StringComparison.OrdinalIgnoreCase));
        list.Insert(0, fullPath);
        while (list.Count > MaxItems)
        {
            list.RemoveAt(list.Count - 1);
        }
        _settings.Save();
    }

    public void Remove(string fullPath)
    {
        _settings.Settings.RecentFiles.RemoveAll(p => string.Equals(p, fullPath, System.StringComparison.OrdinalIgnoreCase));
        _settings.Save();
    }

    public void PruneMissing()
    {
        var list = _settings.Settings.RecentFiles;
        var stillExists = list.Where(File.Exists).ToList();
        if (stillExists.Count != list.Count)
        {
            list.Clear();
            list.AddRange(stillExists);
            _settings.Save();
        }
    }
}
