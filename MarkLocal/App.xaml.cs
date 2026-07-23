using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using MarkLocal.Core;
using MarkLocal.Infrastructure;
using MarkLocal.Models;

namespace MarkLocal;

public partial class App : Application
{
    public SettingsService Settings { get; private set; } = null!;
    public DocumentService Documents { get; private set; } = null!;
    public MarkdownService Markdown { get; private set; } = null!;
    public PreviewService Preview { get; private set; } = null!;
    public ImageAssetService Images { get; private set; } = null!;
    public ExportService Export { get; private set; } = null!;
    public RecentFilesStore Recent { get; private set; } = null!;
    public RecoveryService Recovery { get; private set; } = null!;
    public WorkspaceService Workspace { get; private set; } = null!;
    public TemplateService Templates { get; private set; } = null!;
    public UpdateService Updates { get; private set; } = null!;

    public bool IsPortable { get; private set; }
    public string? PortableRoot { get; private set; }
    public string WebView2UserDataDirectory { get; private set; } = string.Empty;

    public static new App Current => (App)Application.Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ResolveStoragePaths(out string settingsDir, out string draftsDir, out string templatesDir, out string webView2Dir);
        WebView2UserDataDirectory = webView2Dir;

        Settings = new SettingsService(settingsDir);
        Loc.Initialize(Settings.Settings.Language);
        Documents = new DocumentService();
        Markdown = new MarkdownService(Settings);
        Preview = new PreviewService(Settings);
        Images = new ImageAssetService(Settings);
        Export = new ExportService(Markdown, Preview);
        Recent = new RecentFilesStore(Settings);
        Recovery = new RecoveryService(draftsDir);
        Workspace = new WorkspaceService();
        Templates = new TemplateService(templatesDir);
        Templates.EnsureDefaultTemplates();
        Updates = new UpdateService(Settings);

        var parsed = CliRunner.Parse(e.Args);
        if (parsed.ErrorMessage != null)
        {
            MessageBox.Show(parsed.ErrorMessage + "\n\n" + CliRunner.GetHelpText(),
                "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown(1);
            return;
        }
        switch (parsed.Action)
        {
            case CliRunner.Action.ShowHelp:
                MessageBox.Show(CliRunner.GetHelpText(), Loc.T("core.app.helpTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown(0);
                return;
            case CliRunner.Action.ShowVersion:
                MessageBox.Show(CliRunner.GetVersionText(), "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown(0);
                return;
            case CliRunner.Action.ExportHtml:
                _ = RunExportThenExit(parsed);
                return;
        }

        ApplyTheme(ResolveTheme(Settings.Settings.Theme));
        Settings.SettingsChanged += (_, _) => ApplyTheme(ResolveTheme(Settings.Settings.Theme));

        var window = new MainWindow();
        MainWindow = window;
        if (!string.IsNullOrEmpty(parsed.InputPath))
        {
            window.PendingStartupPath = parsed.InputPath;
        }
        window.Show();
    }

    private async Task RunExportThenExit(CliRunner.ParsedArgs parsed)
    {
        int code = await CliRunner.RunExportHtmlAsync(parsed, Settings, Markdown, Preview, Export);
        Shutdown(code);
    }

    public static bool ResolveTheme(ThemePreference preference)
    {
        if (preference == ThemePreference.Dark) return true;
        if (preference == ThemePreference.Light) return false;
        return IsSystemUsingDarkTheme();
    }

    public static bool IsSystemUsingDarkTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int intValue) return intValue == 0;
        }
        catch
        {
        }
        return false;
    }

    public void ApplyTheme(bool dark)
    {
        var resources = Resources;
        if (dark)
        {
            SetBrush(resources, "WindowBackgroundBrush",    0x1E, 0x1E, 0x20);
            SetBrush(resources, "EditorBackgroundBrush",    0x1B, 0x1B, 0x1F);
            SetBrush(resources, "EditorForegroundBrush",    0xE6, 0xE6, 0xE6);
            SetBrush(resources, "ChromeBackgroundBrush",    0x25, 0x25, 0x29);
            SetBrush(resources, "ChromeForegroundBrush",    0xE6, 0xE6, 0xE6);
            SetBrush(resources, "ChromeHoverBrush",         0x35, 0x3A, 0x46);
            SetBrush(resources, "ChromeActiveBrush",        0x3F, 0x4A, 0x5E);
            SetBrush(resources, "AccentBrush",              0x6F, 0xA8, 0xFF);
            SetBrush(resources, "MutedForegroundBrush",     0xA6, 0xA6, 0xAE);
            SetBrush(resources, "DividerBrush",             0x3A, 0x3A, 0x40);
            SetBrush(resources, "StatusBarBackgroundBrush", 0x16, 0x16, 0x1A);
            SetBrush(resources, "PopupBackgroundBrush",     0x2A, 0x2A, 0x2F);
            SetBrush(resources, "DisabledForegroundBrush",  0x70, 0x70, 0x78);
        }
        else
        {
            SetBrush(resources, "WindowBackgroundBrush",    0xF8, 0xF8, 0xF8);
            SetBrush(resources, "EditorBackgroundBrush",    0xFF, 0xFF, 0xFF);
            SetBrush(resources, "EditorForegroundBrush",    0x1B, 0x1B, 0x1B);
            SetBrush(resources, "ChromeBackgroundBrush",    0xF1, 0xF1, 0xF1);
            SetBrush(resources, "ChromeForegroundBrush",    0x1B, 0x1B, 0x1B);
            SetBrush(resources, "ChromeHoverBrush",         0xE2, 0xE8, 0xF5);
            SetBrush(resources, "ChromeActiveBrush",        0xD0, 0xDC, 0xEF);
            SetBrush(resources, "AccentBrush",              0x2A, 0x6F, 0xBE);
            SetBrush(resources, "MutedForegroundBrush",     0x55, 0x55, 0x55);
            SetBrush(resources, "DividerBrush",             0xD8, 0xD8, 0xD8);
            SetBrush(resources, "StatusBarBackgroundBrush", 0xE9, 0xE9, 0xE9);
            SetBrush(resources, "PopupBackgroundBrush",     0xFF, 0xFF, 0xFF);
            SetBrush(resources, "DisabledForegroundBrush",  0xA0, 0xA0, 0xA0);
        }
        ThemeChanged?.Invoke(this, dark);
    }

    private static void SetBrush(ResourceDictionary resources, string key, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        resources[key] = brush;
    }

    /// <summary>
    /// Decide dónde van settings, drafts, plantillas y WebView2 según el modo.
    /// En modo portable, todo cuelga de <c>&lt;exeDir&gt;\Data</c>.
    /// En modo instalado, settings/templates en %AppData%\MarkLocal y drafts/WebView2 en %LocalAppData%\MarkLocal.
    /// </summary>
    private void ResolveStoragePaths(out string settingsDir, out string draftsDir, out string templatesDir, out string webView2Dir)
    {
        string exeDir = AppContext.BaseDirectory;
        string flagPath = Path.Combine(exeDir, "portable.flag");
        IsPortable = File.Exists(flagPath);
        if (IsPortable)
        {
            string root = Path.Combine(exeDir, "Data");
            PortableRoot = root;
            settingsDir  = Path.Combine(root, "config");
            draftsDir    = Path.Combine(root, "drafts");
            templatesDir = Path.Combine(root, "templates");
            webView2Dir  = Path.Combine(root, "WebView2");
            TryCreate(root);
            TryCreate(settingsDir);
            TryCreate(draftsDir);
            TryCreate(templatesDir);
            TryCreate(webView2Dir);
        }
        else
        {
            PortableRoot = null;
            string roamingRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MarkLocal");
            string localRoot   = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MarkLocal");
            settingsDir  = roamingRoot;
            templatesDir = Path.Combine(roamingRoot, "templates");
            draftsDir    = Path.Combine(localRoot, "drafts");
            webView2Dir  = Path.Combine(localRoot, "WebView2");
        }
    }

    private static void TryCreate(string path)
    {
        try { Directory.CreateDirectory(path); } catch { }
    }

    public event EventHandler<bool>? ThemeChanged;
}
