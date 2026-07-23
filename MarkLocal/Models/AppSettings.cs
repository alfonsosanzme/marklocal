using System.Collections.Generic;

namespace MarkLocal.Models;

public enum ThemePreference
{
    System,
    Light,
    Dark
}

public enum LineEnding
{
    LF,
    CRLF
}

public enum ViewMode
{
    Editor,
    Split,
    Reader
}

public class AppSettings
{
    public ThemePreference Theme { get; set; } = ThemePreference.System;
    public string FontFamily { get; set; } = "Consolas";
    public double FontSize { get; set; } = 15;
    public double PreviewFontSize { get; set; } = 16;
    public bool WordWrap { get; set; } = true;
    public LineEnding DefaultLineEnding { get; set; } = LineEnding.CRLF;
    public bool AutoSave { get; set; } = false;
    public int AutoSaveIntervalSeconds { get; set; } = 30;
    public bool CopyImagesToAssets { get; set; } = true;
    public string AssetsFolderName { get; set; } = "assets";
    public bool AutoPairBrackets { get; set; } = true;
    public bool ShowOutline { get; set; } = true;
    public bool ShowLineNumbers { get; set; } = true;
    public bool AllowInlineHtml { get; set; } = false;
    public ViewMode DefaultViewMode { get; set; } = ViewMode.Split;
    public int PreviewDebounceMs { get; set; } = 300;
    public string? CustomCssPath { get; set; }
    public string? WorkspacePath { get; set; }
    public string? UpdateFeedUrl { get; set; }
    public string Language { get; set; } = "auto";
    public List<string> RecentFiles { get; set; } = new();
}
