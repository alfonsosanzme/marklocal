using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using MarkLocal.Core;
using MarkLocal.Models;
using Microsoft.Web.WebView2.Core;

namespace MarkLocal;

public partial class MainWindow : Window
{
    private readonly App _app;
    private readonly DocumentModel _document = new();
    private DispatcherTimer? _previewDebounce;
    private bool _suppressTextChanged;
    private bool _suppressOutlineSelection;
    private bool _previewReady;
    private string _lastRenderedBody = string.Empty;
    private ViewMode _viewMode = ViewMode.Split;
    private GridLength _previewColumnWidth = new(1, GridUnitType.Star);
    private GridLength _editorColumnWidth = new(1, GridUnitType.Star);
    private double _previewFontScale = 1.0;
    private double _editorFontScale = 1.0;
    private const double BaseEditorFontSize = 15;
    private List<HeadingNode> _flatOutline = new();
    private DateTime _lastScrollSyncSent = DateTime.MinValue;
    private DateTime _lastPreviewScrollAppliedUtc = DateTime.MinValue;
    private double _lastKnownPreviewRatio;

    private enum FocusState { None, Simple, Full }
    private FocusState _focusState = FocusState.None;
    private bool _focusMode => _focusState != FocusState.None;
    private DraftSnapshot? _draft;
    private DispatcherTimer? _autoSaveTimer;
    private bool _autoSaveBusy;
    private DateTime _lastAutoSaveUtc = DateTime.MinValue;

    public string? PendingStartupPath { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        _app = App.Current;
        DataContext = _document;
        Loaded += OnWindowLoaded;
        Closing += OnWindowClosing;
        Drop += OnWindowDrop;
        DragOver += OnWindowDragOver;
        PreviewKeyDown += OnPreviewKeyDown;
        _document.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DocumentModel.FilePath))
            {
                RefreshSiblings();
            }
        };
    }

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        ConfigureEditor();
        BuildRecentMenu();
        BuildTemplatesMenu();
        UpdateThemeMenu();
        if (_app.IsPortable)
        {
            PortableIndicator.Visibility = Visibility.Visible;
            PortableIndicator.ToolTip = Loc.T("main.portable.tooltip", _app.PortableRoot);
        }
        UpdateModeMenu();
        ApplyEditorThemeColors();
        ApplyTitleBarTheme();

        await EnsureWebViewReadyAsync();

        if (!string.IsNullOrEmpty(PendingStartupPath))
        {
            if (Directory.Exists(PendingStartupPath))
            {
                OpenWorkspace(PendingStartupPath);
            }
            else if (File.Exists(PendingStartupPath))
            {
                await LoadDocumentAsync(PendingStartupPath);
            }
        }
        else if (!string.IsNullOrEmpty(_app.Settings.Settings.WorkspacePath)
                 && Directory.Exists(_app.Settings.Settings.WorkspacePath))
        {
            OpenWorkspace(_app.Settings.Settings.WorkspacePath);
        }
        else
        {
            PopulateWorkspaceTree();
        }

        if (string.IsNullOrEmpty(_document.FilePath))
        {
            SetEditorText(GetWelcomeMarkdown());
            _document.IsDirty = false;
        }

        UpdateStatus();
        SchedulePreviewRender(immediate: true);
        ApplyViewMode(_app.Settings.Settings.DefaultViewMode);

        StartAutoSave();
        await CheckForOrphanedDraftsAsync();
        _app.Settings.SettingsChanged += (_, _) => UpdateAutoSaveInterval();
        HookSystemThemeChanges();
    }

    private void HookSystemThemeChanges()
    {
        try
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            var source = System.Windows.Interop.HwndSource.FromHwnd(helper.EnsureHandle());
            source?.AddHook(WndProc);
        }
        catch
        {
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_SETTINGCHANGE = 0x001A;
        if (msg == WM_SETTINGCHANGE)
        {
            string? area = null;
            try { area = System.Runtime.InteropServices.Marshal.PtrToStringUni(lParam); } catch { }
            if (string.Equals(area, "ImmersiveColorSet", StringComparison.Ordinal)
                && _app.Settings.Settings.Theme == ThemePreference.System)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _app.ApplyTheme(App.ResolveTheme(_app.Settings.Settings.Theme));
                    TryLoadMarkdownHighlighting();
                    ApplyEditorThemeColors();
                    ApplyTitleBarTheme();
                    _app.Preview.Invalidate();
                    SchedulePreviewRender(immediate: true);
                }));
            }
        }
        return IntPtr.Zero;
    }

    private void StartAutoSave()
    {
        using var proc = Process.GetCurrentProcess();
        _draft = _app.Recovery.CreateSessionDraft(proc.Id, proc.StartTime);

        _autoSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(ResolveAutoSaveInterval())
        };
        _autoSaveTimer.Tick += async (_, _) => await OnAutoSaveTickAsync();
        _autoSaveTimer.Start();
    }

    private int ResolveAutoSaveInterval()
    {
        int seconds = _app.Settings.Settings.AutoSaveIntervalSeconds;
        return Math.Clamp(seconds, 5, 600);
    }

    private void UpdateAutoSaveInterval()
    {
        if (_autoSaveTimer == null) return;
        _autoSaveTimer.Interval = TimeSpan.FromSeconds(ResolveAutoSaveInterval());
    }

    private async Task OnAutoSaveTickAsync()
    {
        if (_autoSaveBusy || _draft == null) return;
        _autoSaveBusy = true;
        try
        {
            string text = Editor.Document.Text ?? string.Empty;
            bool hasContent = !string.IsNullOrWhiteSpace(text) && text != GetWelcomeMarkdown();

            if (!hasContent)
            {
                _app.Recovery.Delete(_draft);
            }
            else if (_document.IsUntitled || _document.IsDirty)
            {
                _draft.OriginalPath = _document.FilePath;
                _draft.Title = _document.IsUntitled ? Loc.T("main.draft.untitled") : Path.GetFileName(_document.FilePath!);
                try
                {
                    await _app.Recovery.SaveAsync(_draft, text);
                }
                catch
                {
                }
            }
            else
            {
                _app.Recovery.Delete(_draft);
            }

            if (_app.Settings.Settings.AutoSave
                && !_document.IsUntitled
                && _document.IsDirty
                && (DateTime.UtcNow - _lastAutoSaveUtc).TotalSeconds >= ResolveAutoSaveInterval() - 1)
            {
                try
                {
                    bool ok = await SaveToPathAsync(_document.FilePath!);
                    if (ok)
                    {
                        _lastAutoSaveUtc = DateTime.UtcNow;
                    }
                }
                catch
                {
                }
            }
        }
        finally
        {
            _autoSaveBusy = false;
        }
    }

    private async Task CheckForOrphanedDraftsAsync()
    {
        await Task.Yield();
        List<DraftSnapshot> orphans;
        try
        {
            int pid;
            using (var proc = Process.GetCurrentProcess()) { pid = proc.Id; }
            orphans = _app.Recovery.ListOrphans(pid);
        }
        catch
        {
            return;
        }
        if (orphans.Count == 0) return;

        var dialog = new Views.RecoveryDialog(orphans, _app.Recovery.DraftsDirectory) { Owner = this };
        bool? result = dialog.ShowDialog();
        if (result == true && dialog.SelectedDraft != null)
        {
            await OpenDraftAsync(dialog.SelectedDraft);
            foreach (var discarded in dialog.DraftsToDiscard)
            {
                _app.Recovery.DeleteOrphan(discarded);
            }
        }
        else
        {
            foreach (var discarded in dialog.DraftsToDiscard)
            {
                _app.Recovery.DeleteOrphan(discarded);
            }
        }
    }

    private async Task OpenDraftAsync(DraftSnapshot draft)
    {
        if (!await ConfirmDiscardChangesAsync()) return;
        try
        {
            string content = await File.ReadAllTextAsync(draft.ContentPath);
            if (!string.IsNullOrEmpty(draft.OriginalPath) && File.Exists(draft.OriginalPath))
            {
                _document.FilePath = draft.OriginalPath;
                _document.EncodingName = "UTF-8";
                _document.LineEnding = _app.Settings.Settings.DefaultLineEnding;
            }
            else
            {
                _document.FilePath = null;
                _document.EncodingName = "UTF-8";
                _document.LineEnding = _app.Settings.Settings.DefaultLineEnding;
            }
            SetEditorText(content);
            _document.IsDirty = true;
            UpdateStatus();
            SchedulePreviewRender(immediate: true);
            _app.Recovery.DeleteOrphan(draft);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Loc.T("main.msg.recoverDraftError", ex.Message),
                "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ConfigureEditor()
    {
        Editor.Options.ConvertTabsToSpaces = true;
        Editor.Options.IndentationSize = 2;
        Editor.Options.EnableHyperlinks = true;
        Editor.Options.EnableEmailHyperlinks = true;
        Editor.Options.CutCopyWholeLine = true;
        Editor.ShowLineNumbers = _app.Settings.Settings.ShowLineNumbers;
        Editor.WordWrap = _app.Settings.Settings.WordWrap;
        WordWrapItem.IsChecked = _app.Settings.Settings.WordWrap;
        OutlineToggleItem.IsChecked = _app.Settings.Settings.ShowOutline;
        UpdateOutlineVisibility();
        ApplyEditorFontSettings();

        TryLoadMarkdownHighlighting();

        Editor.TextArea.Caret.PositionChanged += (_, _) => UpdateCaretStatus();
        Editor.TextArea.SelectionChanged += (_, _) => UpdateCaretStatus();
        Editor.TextChanged += OnEditorTextChanged;
        Editor.PreviewKeyDown += OnEditorKeyDown;
        Editor.PreviewDrop += OnEditorPreviewDrop;
        Editor.PreviewDragOver += OnEditorPreviewDragOver;
        Editor.AllowDrop = true;
        Editor.TextArea.TextView.ScrollOffsetChanged += OnEditorScrollChanged;
        Editor.TextArea.TextEntering += OnTextEntering;
    }

    private void TryLoadMarkdownHighlighting()
    {
        bool dark = App.ResolveTheme(_app.Settings.Settings.Theme);
        string resourcePath = dark
            ? "pack://application:,,,/Resources/MarkdownHighlighting.Dark.xshd"
            : "pack://application:,,,/Resources/MarkdownHighlighting.xshd";
        try
        {
            var uri = new Uri(resourcePath, UriKind.Absolute);
            var info = Application.GetResourceStream(uri);
            if (info == null) return;
            using var reader = new XmlTextReader(info.Stream);
            var highlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            HighlightingManager.Instance.RegisterHighlighting("Markdown", new[] { ".md", ".markdown" }, highlighting);
            Editor.SyntaxHighlighting = highlighting;
        }
        catch
        {
            Editor.SyntaxHighlighting = null;
        }
    }

    private async Task EnsureWebViewReadyAsync()
    {
        try
        {
            string userDataDir = !string.IsNullOrEmpty(_app.WebView2UserDataDirectory)
                ? _app.WebView2UserDataDirectory
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MarkLocal", "WebView2");
            Directory.CreateDirectory(userDataDir);
            var env = await CoreWebView2Environment.CreateAsync(null, userDataDir);
            await Preview.EnsureCoreWebView2Async(env);
            _previewReady = true;
            Preview.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            Preview.CoreWebView2.Settings.AreDevToolsEnabled = false;
            Preview.CoreWebView2.Settings.IsStatusBarEnabled = false;
            Preview.CoreWebView2.Settings.IsZoomControlEnabled = true;
            Preview.CoreWebView2.Settings.IsBuiltInErrorPageEnabled = false;
            Preview.CoreWebView2.NavigationStarting += OnNavigationStarting;
            Preview.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
            Preview.CoreWebView2.WebMessageReceived += OnPreviewMessageReceived;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                Loc.T("main.msg.webviewInitError", ex.Message),
                "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (e.Uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return;
        if (e.Uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase)) return;
        if (e.IsRedirected) return;
        if (e.NavigationKind == CoreWebView2NavigationKind.NewDocument)
        {
            return;
        }
        if (e.Uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || e.Uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || e.Uri.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
            OpenInDefaultBrowser(e.Uri);
        }
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        OpenInDefaultBrowser(e.Uri);
    }

    private void OnPreviewMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            string json = e.TryGetWebMessageAsString();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("type", out var type)) return;
            string? kind = type.GetString();
            switch (kind)
            {
                case "open-external":
                    if (doc.RootElement.TryGetProperty("href", out var href))
                    {
                        string? url = href.GetString();
                        if (!string.IsNullOrEmpty(url)) OpenInDefaultBrowser(url);
                    }
                    break;
                case "sync-scroll-back":
                    {
                        string? prevA = doc.RootElement.TryGetProperty("prev", out var p) ? p.GetString() : null;
                        string? nextA = doc.RootElement.TryGetProperty("next", out var nx) ? nx.GetString() : null;
                        double frac = doc.RootElement.TryGetProperty("frac", out var fr) && fr.TryGetDouble(out double f) ? f : 0;
                        double ratio2 = doc.RootElement.TryGetProperty("ratio", out var rt) && rt.TryGetDouble(out double r2) ? r2 : 0;
                        _lastKnownPreviewRatio = ratio2;
                        ApplyPreviewSyncBack(prevA, nextA, frac, ratio2);
                    }
                    break;
                case "toggle-task":
                    if (doc.RootElement.TryGetProperty("index", out var idx) && idx.TryGetInt32(out int taskIndex))
                    {
                        ToggleTaskInDocument(taskIndex);
                    }
                    break;
                case "edit-here":
                    {
                        string? anchor = doc.RootElement.TryGetProperty("anchor", out var a) ? a.GetString() : null;
                        JumpToEditFromPreview(anchor);
                    }
                    break;
            }
        }
        catch
        {
        }
    }

    private static readonly System.Text.RegularExpressions.Regex TaskLineRegex =
        new(@"^(\s*(?:[-*+]|\d+[.)])\s+\[)( |x|X)(\])", System.Text.RegularExpressions.RegexOptions.Compiled);

    private void ToggleTaskInDocument(int taskIndex)
    {
        var document = Editor.Document;
        int seen = 0;
        for (int lineNo = 1; lineNo <= document.LineCount; lineNo++)
        {
            var line = document.GetLineByNumber(lineNo);
            string text = document.GetText(line.Offset, line.Length);
            var match = TaskLineRegex.Match(text);
            if (!match.Success) continue;
            if (seen == taskIndex)
            {
                int markOffset = line.Offset + match.Groups[2].Index;
                string current = match.Groups[2].Value;
                string replacement = current == " " ? "x" : " ";
                document.Replace(markOffset, 1, replacement);
                return;
            }
            seen++;
        }
    }

    private void JumpToEditFromPreview(string? anchor)
    {
        if (_viewMode == ViewMode.Reader)
        {
            ApplyViewMode(ViewMode.Split);
        }
        if (!string.IsNullOrEmpty(anchor))
        {
            var heading = _flatOutline.FirstOrDefault(h => string.Equals(h.Anchor, anchor, StringComparison.OrdinalIgnoreCase));
            if (heading != null)
            {
                JumpToHeading(heading);
                return;
            }
        }
        Editor.Focus();
    }

    private static void OpenInDefaultBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
        }
    }

    private void SetEditorText(string text)
    {
        _suppressTextChanged = true;
        Editor.Document.Text = text;
        _document.Content = text;
        _suppressTextChanged = false;
        UpdateStatus();
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_suppressTextChanged) return;
        _document.Content = Editor.Document.Text;
        _document.IsDirty = true;
        UpdateStatus();
        SchedulePreviewRender();
    }

    private void SchedulePreviewRender(bool immediate = false)
    {
        if (_previewDebounce == null)
        {
            _previewDebounce = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(_app.Settings.Settings.PreviewDebounceMs)
            };
            _previewDebounce.Tick += async (_, _) =>
            {
                _previewDebounce!.Stop();
                await RenderPreviewAsync();
            };
        }
        _previewDebounce.Interval = TimeSpan.FromMilliseconds(immediate ? 1 : _app.Settings.Settings.PreviewDebounceMs);
        _previewDebounce.Stop();
        _previewDebounce.Start();
    }

    private async Task RenderPreviewAsync()
    {
        if (!_previewReady) return;
        string markdown = _document.Content;
        string? dir = _document.DocumentDirectory;
        try
        {
            string body = await Task.Run(() => _app.Markdown.ConvertToHtml(markdown, dir));
            var outline = await Task.Run(() => _app.Markdown.ExtractOutline(markdown));
            _flatOutline = FlattenOutline(outline);
            UpdateOutlineTree(outline);
            _lastRenderedBody = body;
            string html = _app.Preview.BuildHtml(body, App.ResolveTheme(_app.Settings.Settings.Theme), _app.Settings.Settings.PreviewFontSize * _previewFontScale);

            // Preservar la posición de lectura tras el re-render.
            double restoreRatio = _viewMode == ViewMode.Reader
                ? _lastKnownPreviewRatio
                : ComputeEditorScrollRatio();
            EventHandler<CoreWebView2NavigationCompletedEventArgs>? restoreHandler = null;
            restoreHandler = (_, _) =>
            {
                Preview.CoreWebView2.NavigationCompleted -= restoreHandler!;
                if (restoreRatio > 0.001)
                {
                    try
                    {
                        string payload = JsonSerializer.Serialize(new { type = "scroll-to-ratio", ratio = restoreRatio });
                        Preview.CoreWebView2?.PostWebMessageAsJson(payload);
                    }
                    catch { }
                }
            };
            Preview.CoreWebView2.NavigationCompleted += restoreHandler;
            Preview.NavigateToString(html);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Render error: " + ex);
        }
    }

    private double ComputeEditorScrollRatio()
    {
        try
        {
            var textView = Editor.TextArea.TextView;
            double scrollable = Math.Max(textView.DocumentHeight - textView.ActualHeight, 1);
            return Math.Clamp(textView.ScrollOffset.Y / scrollable, 0, 1);
        }
        catch
        {
            return 0;
        }
    }

    private void UpdateOutlineTree(IReadOnlyList<HeadingNode> outline)
    {
        _suppressOutlineSelection = true;
        try
        {
            OutlineTree.Items.Clear();
            foreach (var node in outline) OutlineTree.Items.Add(BuildOutlineItem(node));
            OutlineEmptyHint.Visibility = outline.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        finally
        {
            _suppressOutlineSelection = false;
        }
    }

    private static TreeViewItem BuildOutlineItem(HeadingNode node)
    {
        var label = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(node.Text) ? Loc.T("main.outline.untitled") : node.Text,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = node.Level switch { 1 => 14, 2 => 13, 3 => 12.5, _ => 12 },
            FontWeight = node.Level <= 2 ? FontWeights.SemiBold : FontWeights.Normal
        };
        var item = new TreeViewItem
        {
            Header = label,
            Tag = node,
            IsExpanded = true,
            ToolTip = Loc.T("main.outline.tooltip", node.LineNumber, node.Level)
        };
        foreach (var child in node.Children) item.Items.Add(BuildOutlineItem(child));
        return item;
    }

    private static List<HeadingNode> FlattenOutline(IReadOnlyList<HeadingNode> outline)
    {
        var list = new List<HeadingNode>();
        void Walk(IEnumerable<HeadingNode> nodes)
        {
            foreach (var n in nodes)
            {
                list.Add(n);
                Walk(n.Children);
            }
        }
        Walk(outline);
        return list;
    }

    private void OnOutlineSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (_suppressOutlineSelection) return;
        if (e.NewValue is TreeViewItem item && item.Tag is HeadingNode heading)
        {
            JumpToHeading(heading);
        }
    }

    private void JumpToHeading(HeadingNode heading)
    {
        int line = Math.Max(1, heading.LineNumber);
        if (line > Editor.Document.LineCount) line = Editor.Document.LineCount;
        var docLine = Editor.Document.GetLineByNumber(line);
        Editor.ScrollToLine(line);
        Editor.CaretOffset = docLine.Offset;
        Editor.Focus();
        if (_previewReady && !string.IsNullOrEmpty(heading.Anchor))
        {
            string payload = JsonSerializer.Serialize(new { type = "scroll-to-anchor", anchor = heading.Anchor });
            Preview.CoreWebView2?.PostWebMessageAsJson(payload);
        }
    }

    private void UpdateStatus()
    {
        StatusPath.Text = _document.IsUntitled ? Loc.T("main.status.unsaved") : _document.FilePath!;
        StatusDirty.Text = _document.IsDirty ? Loc.T("main.status.modified") : Loc.T("main.status.saved");
        Title = Loc.T("main.window.title", _document.DisplayName);

        string text = _document.Content ?? string.Empty;
        StatusChars.Text = Loc.T("main.status.chars", text.Length);
        StatusWords.Text = Loc.T("main.status.words", CountWords(text));
        StatusEncoding.Text = _document.EncodingName + " · " + (_document.LineEnding == LineEnding.CRLF ? "CRLF" : "LF");
        StatusMode.Text = _viewMode switch
        {
            ViewMode.Editor => Loc.T("main.mode.editor"),
            ViewMode.Reader => Loc.T("main.mode.reader"),
            _ => Loc.T("main.mode.split")
        };
        UpdateCaretStatus();
    }

    private void UpdateCaretStatus()
    {
        var caret = Editor.TextArea.Caret.Position;
        StatusLineCol.Text = Loc.T("main.status.lineCol", caret.Line, caret.Column);
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var separators = new[] { ' ', '\t', '\n', '\r', '\f', '\v', '·', '—', '–' };
        return text.Split(separators, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static string GetWelcomeMarkdown() => Loc.T("main.welcome");

    private async void OnNewClicked(object sender, RoutedEventArgs e) => await NewDocumentAsync();
    private async void OnOpenClicked(object sender, RoutedEventArgs e) => await OpenDocumentAsync();
    private async void OnSaveClicked(object sender, RoutedEventArgs e) => await SaveDocumentAsync();
    private async void OnSaveAsClicked(object sender, RoutedEventArgs e) => await SaveDocumentAsAsync();
    private void OnExitClicked(object sender, RoutedEventArgs e) => Close();

    private async Task<bool> ConfirmDiscardChangesAsync()
    {
        if (!_document.IsDirty) return true;
        var result = MessageBox.Show(this,
            Loc.T("main.msg.confirmDiscard", _document.DisplayName.TrimEnd('*').Trim()),
            "MarkLocal",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);
        if (result == MessageBoxResult.Cancel) return false;
        if (result == MessageBoxResult.Yes)
        {
            return await SaveDocumentAsync();
        }
        return true;
    }

    private async Task NewDocumentAsync()
    {
        if (!await ConfirmDiscardChangesAsync()) return;
        _document.FilePath = null;
        _document.EncodingName = "UTF-8";
        _document.LineEnding = _app.Settings.Settings.DefaultLineEnding;
        SetEditorText(string.Empty);
        _document.IsDirty = false;
        UpdateStatus();
        SchedulePreviewRender(immediate: true);
        Editor.Focus();
    }

    private async Task OpenDocumentAsync()
    {
        if (!await ConfirmDiscardChangesAsync()) return;
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = Loc.T("main.filter.open"),
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
        {
            await LoadDocumentAsync(dialog.FileName);
        }
    }

    private async Task LoadDocumentAsync(string fullPath)
    {
        try
        {
            var (content, encoding, lineEnding) = await _app.Documents.LoadAsync(fullPath);
            _document.FilePath = fullPath;
            _document.EncodingName = encoding.WebName.ToUpperInvariant();
            _document.LineEnding = lineEnding;
            SetEditorText(content);
            _document.IsDirty = false;
            _app.Recent.Add(fullPath);
            BuildRecentMenu();
            UpdateStatus();
            SchedulePreviewRender(immediate: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Loc.T("main.msg.openError", ex.Message), "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task<bool> SaveDocumentAsync()
    {
        if (_document.IsUntitled) return await SaveDocumentAsAsync();
        return await SaveToPathAsync(_document.FilePath!);
    }

    private async Task<bool> SaveDocumentAsAsync()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = Loc.T("main.filter.save"),
            DefaultExt = ".md",
            FileName = _document.IsUntitled ? Loc.T("main.file.untitledMd") : Path.GetFileName(_document.FilePath!)
        };
        if (dialog.ShowDialog(this) != true) return false;
        return await SaveToPathAsync(dialog.FileName);
    }

    private async Task<bool> SaveToPathAsync(string path)
    {
        try
        {
            Encoding encoding = string.Equals(_document.EncodingName, "UTF-8", StringComparison.OrdinalIgnoreCase)
                ? new UTF8Encoding(false)
                : Encoding.GetEncoding(_document.EncodingName);
            string content = Editor.Document.Text;
            await _app.Documents.SaveAsync(path, content, encoding, _document.LineEnding);
            _document.FilePath = path;
            _document.IsDirty = false;
            _app.Recent.Add(path);
            BuildRecentMenu();
            UpdateStatus();
            SchedulePreviewRender();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Loc.T("main.msg.saveError", ex.Message), "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void BuildRecentMenu()
    {
        RecentMenu.Items.Clear();
        var recent = _app.Recent.GetAll();
        if (recent.Count == 0)
        {
            RecentMenu.Items.Add(new MenuItem { Header = Loc.T("main.recent.empty"), IsEnabled = false });
            return;
        }
        foreach (var path in recent)
        {
            var item = new MenuItem { Header = path, Tag = path, ToolTip = path };
            item.Click += async (_, _) => await OpenRecentAsync((string)item.Tag);
            RecentMenu.Items.Add(item);
        }
        RecentMenu.Items.Add(new Separator());
        var clear = new MenuItem { Header = Loc.T("main.recent.clear") };
        clear.Click += (_, _) =>
        {
            _app.Settings.Settings.RecentFiles.Clear();
            _app.Settings.Save();
            BuildRecentMenu();
        };
        RecentMenu.Items.Add(clear);
    }

    private async Task OpenRecentAsync(string path)
    {
        if (!File.Exists(path))
        {
            MessageBox.Show(this, Loc.T("main.msg.recentMissing"), "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Information);
            _app.Recent.Remove(path);
            BuildRecentMenu();
            return;
        }
        if (!await ConfirmDiscardChangesAsync()) return;
        await LoadDocumentAsync(path);
    }

    private async void OnExportHtmlClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "HTML (*.html)|*.html",
            DefaultExt = ".html",
            FileName = (_document.IsUntitled ? Loc.T("main.file.untitled") : Path.GetFileNameWithoutExtension(_document.FilePath!)) + ".html"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            await _app.Export.ExportHtmlAsync(
                Editor.Document.Text,
                _document.DocumentDirectory,
                dialog.FileName,
                App.ResolveTheme(_app.Settings.Settings.Theme),
                _app.Settings.Settings.PreviewFontSize * _previewFontScale);
            MessageBox.Show(this, Loc.T("main.msg.exportHtmlDone"), "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Loc.T("main.msg.exportHtmlError", ex.Message), "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnPrintClicked(object sender, RoutedEventArgs e)
    {
        if (!_previewReady) return;
        try
        {
            await Preview.CoreWebView2.ExecuteScriptAsync("window.print();");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Loc.T("main.msg.printError", ex.Message), "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnExportPdfClicked(object sender, RoutedEventArgs e)
    {
        if (!_previewReady)
        {
            MessageBox.Show(this, Loc.T("main.msg.previewNotReady"),
                "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PDF (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            FileName = (_document.IsUntitled ? Loc.T("main.file.untitled") : Path.GetFileNameWithoutExtension(_document.FilePath!)) + ".pdf"
        };
        if (dialog.ShowDialog(this) != true) return;

        Cursor previous = Cursor;
        Cursor = Cursors.Wait;
        try
        {
            await ExportToPdfAsync(dialog.FileName);
            var result = MessageBox.Show(this,
                Loc.T("main.msg.pdfDone", dialog.FileName),
                "MarkLocal", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (result == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Loc.T("main.msg.exportPdfError", ex.Message), "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Cursor = previous;
        }
    }

    private async Task ExportToPdfAsync(string targetPath)
    {
        string body = await Task.Run(() => _app.Markdown.ConvertToHtml(Editor.Document.Text, _document.DocumentDirectory));
        string html = _app.Preview.BuildHtml(body, useDarkTheme: false, _app.Settings.Settings.PreviewFontSize);

        var navTcs = new TaskCompletionSource<bool>();
        EventHandler<Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs>? handler = null;
        handler = (_, ev) =>
        {
            Preview.CoreWebView2.NavigationCompleted -= handler!;
            navTcs.TrySetResult(ev.IsSuccess);
        };
        Preview.CoreWebView2.NavigationCompleted += handler;
        Preview.NavigateToString(html);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        cts.Token.Register(() => navTcs.TrySetResult(false));
        bool ok = await navTcs.Task;
        if (!ok)
        {
            SchedulePreviewRender(immediate: true);
            throw new InvalidOperationException(Loc.T("main.err.pdfPreviewTimeout"));
        }

        try
        {
            var settings = Preview.CoreWebView2.Environment.CreatePrintSettings();
            settings.Orientation = Microsoft.Web.WebView2.Core.CoreWebView2PrintOrientation.Portrait;
            settings.ShouldPrintBackgrounds = true;
            settings.MarginTop = 0.6;
            settings.MarginBottom = 0.6;
            settings.MarginLeft = 0.6;
            settings.MarginRight = 0.6;
            settings.PageWidth = 8.27;   // A4 en pulgadas
            settings.PageHeight = 11.69;
            settings.ScaleFactor = 1.0;

            bool result = await Preview.CoreWebView2.PrintToPdfAsync(targetPath, settings);
            if (!result)
            {
                throw new InvalidOperationException(Loc.T("main.err.printToPdfFalse"));
            }
        }
        finally
        {
            SchedulePreviewRender(immediate: true);
        }
    }

    private void OnUndoClicked(object sender, RoutedEventArgs e) => Editor.Undo();
    private void OnRedoClicked(object sender, RoutedEventArgs e) => Editor.Redo();
    private void OnCutClicked(object sender, RoutedEventArgs e) => Editor.Cut();
    private void OnCopyClicked(object sender, RoutedEventArgs e) => Editor.Copy();
    private void OnPasteClicked(object sender, RoutedEventArgs e) => Editor.Paste();
    private void OnSelectAllClicked(object sender, RoutedEventArgs e) => Editor.SelectAll();

    private void OnFindClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new Views.FindReplaceDialog(Editor) { Owner = this };
        dialog.Show();
    }

    private void OnBoldClicked(object sender, RoutedEventArgs e) => WrapSelection("**", "**", Loc.T("main.placeholder.bold"));
    private void OnItalicClicked(object sender, RoutedEventArgs e) => WrapSelection("*", "*", Loc.T("main.placeholder.italic"));
    private void OnStrikeClicked(object sender, RoutedEventArgs e) => WrapSelection("~~", "~~", Loc.T("main.placeholder.strike"));
    private void OnInlineCodeClicked(object sender, RoutedEventArgs e) => WrapSelection("`", "`", Loc.T("main.placeholder.code"));

    private void OnCodeBlockClicked(object sender, RoutedEventArgs e)
    {
        string selected = Editor.SelectedText;
        if (string.IsNullOrEmpty(selected))
        {
            InsertAtCaret("```\n\n```", caretOffsetFromEnd: 4);
        }
        else
        {
            ReplaceSelection($"```\n{selected}\n```");
        }
    }

    private void OnH1Clicked(object sender, RoutedEventArgs e) => PrefixCurrentLine("# ");
    private void OnH2Clicked(object sender, RoutedEventArgs e) => PrefixCurrentLine("## ");
    private void OnH3Clicked(object sender, RoutedEventArgs e) => PrefixCurrentLine("### ");
    private void OnQuoteClicked(object sender, RoutedEventArgs e) => PrefixCurrentLine("> ");
    private void OnBulletListClicked(object sender, RoutedEventArgs e) => PrefixCurrentLine("- ");
    private void OnNumberedListClicked(object sender, RoutedEventArgs e) => PrefixCurrentLine("1. ");
    private void OnTaskListClicked(object sender, RoutedEventArgs e) => PrefixCurrentLine("- [ ] ");
    private void OnHorizontalRuleClicked(object sender, RoutedEventArgs e) => InsertAtCaret("\n\n---\n\n");

    private void OnLinkClicked(object sender, RoutedEventArgs e)
    {
        string selected = Editor.SelectedText;
        string label = string.IsNullOrWhiteSpace(selected) ? Loc.T("main.placeholder.link") : selected;
        ReplaceSelection($"[{label}](https://)");
    }

    private void OnImageClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = Loc.T("main.filter.image")
        };
        if (dialog.ShowDialog(this) == true)
        {
            InsertImageFromDisk(dialog.FileName);
        }
    }

    private void OnTableClicked(object sender, RoutedEventArgs e)
    {
        string table = Loc.T("main.snippet.table");
        InsertAtCaret(table);
    }

    private void OnModeEditorClicked(object sender, RoutedEventArgs e) => ApplyViewMode(ViewMode.Editor);
    private void OnModeSplitClicked(object sender, RoutedEventArgs e) => ApplyViewMode(ViewMode.Split);
    private void OnModeReaderClicked(object sender, RoutedEventArgs e) => ApplyViewMode(ViewMode.Reader);

    private void ApplyViewMode(ViewMode mode)
    {
        _viewMode = mode;
        ModeEditorItem.IsChecked = mode == ViewMode.Editor;
        ModeSplitItem.IsChecked = mode == ViewMode.Split;
        ModeReaderItem.IsChecked = mode == ViewMode.Reader;
        if (ToolbarModeEditor != null) ToolbarModeEditor.IsChecked = mode == ViewMode.Editor;
        if (ToolbarModeSplit != null) ToolbarModeSplit.IsChecked = mode == ViewMode.Split;
        if (ToolbarModeReader != null) ToolbarModeReader.IsChecked = mode == ViewMode.Reader;

        switch (mode)
        {
            case ViewMode.Editor:
                EditorColumn.MinWidth = 0;
                PreviewColumn.MinWidth = 0;
                EditorColumn.Width = new GridLength(1, GridUnitType.Star);
                EditorSplitterColumn.Width = new GridLength(0);
                PreviewColumn.Width = new GridLength(0);
                Editor.Visibility = Visibility.Visible;
                EditorPreviewSplitter.Visibility = Visibility.Collapsed;
                Preview.Visibility = Visibility.Collapsed;
                break;
            case ViewMode.Reader:
                EditorColumn.MinWidth = 0;
                PreviewColumn.MinWidth = 0;
                EditorColumn.Width = new GridLength(0);
                EditorSplitterColumn.Width = new GridLength(0);
                PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
                Editor.Visibility = Visibility.Collapsed;
                EditorPreviewSplitter.Visibility = Visibility.Collapsed;
                Preview.Visibility = Visibility.Visible;
                break;
            default:
                EditorColumn.MinWidth = 200;
                PreviewColumn.MinWidth = 200;
                EditorColumn.Width = new GridLength(1, GridUnitType.Star);
                EditorSplitterColumn.Width = GridLength.Auto;
                PreviewColumn.Width = new GridLength(1, GridUnitType.Star);
                Editor.Visibility = Visibility.Visible;
                EditorPreviewSplitter.Visibility = Visibility.Visible;
                Preview.Visibility = Visibility.Visible;
                break;
        }
        UpdateStatus();

        // Recordar el último modo elegido para el próximo arranque.
        if (_app.Settings.Settings.DefaultViewMode != mode)
        {
            _app.Settings.Settings.DefaultViewMode = mode;
            _app.Settings.Save();
        }
    }

    private void OnToggleOutlineClicked(object sender, RoutedEventArgs e)
    {
        _app.Settings.Settings.ShowOutline = OutlineToggleItem.IsChecked;
        _app.Settings.Save();
        UpdateOutlineVisibility();
    }

    private void OnToggleOutlineFromToolbar(object sender, RoutedEventArgs e)
    {
        _app.Settings.Settings.ShowOutline = ToolbarOutlineToggle.IsChecked == true;
        _app.Settings.Save();
        UpdateOutlineVisibility();
    }

    private void UpdateOutlineVisibility()
    {
        bool visible = _app.Settings.Settings.ShowOutline;
        OutlineColumn.MinWidth = visible ? 120 : 0;
        OutlineColumn.Width = visible ? new GridLength(250) : new GridLength(0);
        OutlineSplitterColumn.Width = visible ? GridLength.Auto : new GridLength(0);
        OutlineToggleItem.IsChecked = visible;
        if (ToolbarOutlineToggle != null) ToolbarOutlineToggle.IsChecked = visible;
    }

    private void OnToggleWordWrapClicked(object sender, RoutedEventArgs e)
    {
        Editor.WordWrap = WordWrapItem.IsChecked;
        _app.Settings.Settings.WordWrap = WordWrapItem.IsChecked;
        _app.Settings.Save();
    }

    private void OnZoomInClicked(object sender, RoutedEventArgs e) => ApplyZoom(0.1);
    private void OnZoomOutClicked(object sender, RoutedEventArgs e) => ApplyZoom(-0.1);
    private void OnZoomResetClicked(object sender, RoutedEventArgs e)
    {
        _editorFontScale = 1.0;
        _previewFontScale = 1.0;
        ApplyEditorFontSettings();
        SchedulePreviewRender(immediate: true);
    }

    private void ApplyZoom(double delta)
    {
        _editorFontScale = Math.Clamp(_editorFontScale + delta, 0.6, 2.5);
        _previewFontScale = Math.Clamp(_previewFontScale + delta, 0.6, 2.5);
        ApplyEditorFontSettings();
        SchedulePreviewRender(immediate: true);
    }

    private void ApplyEditorFontSettings()
    {
        try
        {
            Editor.FontFamily = new FontFamily(_app.Settings.Settings.FontFamily);
        }
        catch
        {
            Editor.FontFamily = new FontFamily("Consolas");
        }
        Editor.FontSize = (_app.Settings.Settings.FontSize > 0 ? _app.Settings.Settings.FontSize : BaseEditorFontSize) * _editorFontScale;
    }

    private void OnThemeLightClicked(object sender, RoutedEventArgs e) => ChangeTheme(ThemePreference.Light);
    private void OnThemeDarkClicked(object sender, RoutedEventArgs e) => ChangeTheme(ThemePreference.Dark);
    private void OnThemeSystemClicked(object sender, RoutedEventArgs e) => ChangeTheme(ThemePreference.System);

    private void ChangeTheme(ThemePreference preference)
    {
        _app.Settings.Settings.Theme = preference;
        _app.Settings.Save();
        _app.ApplyTheme(App.ResolveTheme(preference));
        UpdateThemeMenu();
        TryLoadMarkdownHighlighting();
        ApplyEditorThemeColors();
        ApplyTitleBarTheme();
        SchedulePreviewRender(immediate: true);
    }

    private void UpdateThemeMenu()
    {
        var theme = _app.Settings.Settings.Theme;
        ThemeLightItem.IsChecked = theme == ThemePreference.Light;
        ThemeDarkItem.IsChecked = theme == ThemePreference.Dark;
        ThemeSystemItem.IsChecked = theme == ThemePreference.System;
    }

    private void UpdateModeMenu()
    {
        ModeEditorItem.IsChecked = _viewMode == ViewMode.Editor;
        ModeSplitItem.IsChecked = _viewMode == ViewMode.Split;
        ModeReaderItem.IsChecked = _viewMode == ViewMode.Reader;
    }

    private void ApplyEditorThemeColors()
    {
        bool dark = App.ResolveTheme(_app.Settings.Settings.Theme);
        Editor.Background = dark
            ? new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1F))
            : new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
        Editor.Foreground = dark
            ? new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE6))
            : new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1B));
        Editor.TextArea.SelectionBrush = new SolidColorBrush(dark ? Color.FromArgb(0x88, 0x6F, 0xA8, 0xFF) : Color.FromArgb(0x88, 0x2A, 0x6F, 0xBE));
        Editor.LineNumbersForeground = new SolidColorBrush(dark ? Color.FromRgb(0x80, 0x80, 0x90) : Color.FromRgb(0x99, 0x99, 0x99));
    }

    private async void OnPreferencesClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new Views.SettingsWindow(_app.Settings) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _app.Settings.Save();
            _app.ApplyTheme(App.ResolveTheme(_app.Settings.Settings.Theme));
            UpdateThemeMenu();
            TryLoadMarkdownHighlighting();
            ApplyEditorFontSettings();
            ApplyEditorThemeColors();
            ApplyTitleBarTheme();
            Editor.WordWrap = _app.Settings.Settings.WordWrap;
            Editor.ShowLineNumbers = _app.Settings.Settings.ShowLineNumbers;
            WordWrapItem.IsChecked = _app.Settings.Settings.WordWrap;
            UpdateOutlineVisibility();
            OutlineToggleItem.IsChecked = _app.Settings.Settings.ShowOutline;
            _app.Preview.Invalidate();
            SchedulePreviewRender(immediate: true);

            if (!string.IsNullOrEmpty(dialog.RequestOpenInEditorPath))
            {
                if (await ConfirmDiscardChangesAsync())
                {
                    await LoadDocumentAsync(dialog.RequestOpenInEditorPath);
                }
            }
        }
    }

    private async void OnCheckUpdatesClicked(object sender, RoutedEventArgs e)
    {
        Cursor previous = Cursor;
        Cursor = Cursors.Wait;
        try
        {
            Version current = typeof(MainWindow).Assembly.GetName().Version ?? new Version(0, 0, 0);
            var result = await _app.Updates.CheckAsync(current);
            switch (result.Kind)
            {
                case UpdateCheckKind.NotConfigured:
                    var configure = MessageBox.Show(this,
                        Loc.T("main.update.notConfigured"),
                        Loc.T("main.update.captionCheck"), MessageBoxButton.YesNo, MessageBoxImage.Information);
                    if (configure == MessageBoxResult.Yes)
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo(_app.Settings.ConfigDirectory) { UseShellExecute = true });
                        }
                        catch { }
                    }
                    break;
                case UpdateCheckKind.UpToDate:
                    MessageBox.Show(this,
                        Loc.T("main.update.upToDate", current.ToString(3)),
                        Loc.T("main.update.captionUpToDate"), MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case UpdateCheckKind.NewAvailable:
                    string notes = string.IsNullOrWhiteSpace(result.Info?.ReleaseNotesText)
                        ? string.Empty
                        : Loc.T("main.update.notes", result.Info!.ReleaseNotesText);
                    string download = result.Info?.DownloadPageUrl ?? result.Info?.ReleaseNotesUrl ?? string.Empty;
                    var open = MessageBox.Show(this,
                        Loc.T("main.update.newAvailable",
                            current.ToString(3), result.RemoteVersion, notes,
                            string.IsNullOrEmpty(download)
                                ? Loc.T("main.update.visitSite")
                                : Loc.T("main.update.openDownloadPage")),
                        Loc.T("main.update.captionAvailable"),
                        string.IsNullOrEmpty(download) ? MessageBoxButton.OK : MessageBoxButton.YesNo,
                        MessageBoxImage.Information);
                    if (open == MessageBoxResult.Yes && !string.IsNullOrEmpty(download))
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo(download) { UseShellExecute = true });
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(this, Loc.T("main.msg.openBrowserError", ex.Message),
                                "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    break;
                case UpdateCheckKind.InvalidFeed:
                    MessageBox.Show(this,
                        Loc.T("main.update.invalidFeed", result.Message),
                        "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;
                case UpdateCheckKind.Error:
                    MessageBox.Show(this,
                        Loc.T("main.update.checkError", result.Message),
                        "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;
            }
        }
        finally
        {
            Cursor = previous;
        }
    }

    private void OnAboutClicked(object sender, RoutedEventArgs e)
    {
        string version = typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
        string text = Loc.T("main.about.text", version);
        MessageBox.Show(this, text, Loc.T("main.about.caption"), MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                ToggleFullFocusMode();
            }
            else
            {
                ToggleFocusMode();
            }
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape && _focusMode)
        {
            ExitFocusMode();
            e.Handled = true;
            return;
        }
        if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.N: NewDocumentAsyncFireAndForget(); e.Handled = true; return;
                case Key.O: OpenDocumentAsyncFireAndForget(); e.Handled = true; return;
                case Key.S when (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift:
                    SaveDocumentAsAsyncFireAndForget(); e.Handled = true; return;
                case Key.S: SaveDocumentAsyncFireAndForget(); e.Handled = true; return;
                case Key.P when (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift:
                    OnExportPdfClicked(this, new RoutedEventArgs()); e.Handled = true; return;
                case Key.P: OnPrintClicked(this, new RoutedEventArgs()); e.Handled = true; return;
                case Key.OemPlus or Key.Add: ApplyZoom(0.1); e.Handled = true; return;
                case Key.OemMinus or Key.Subtract: ApplyZoom(-0.1); e.Handled = true; return;
                case Key.D0 or Key.NumPad0: OnZoomResetClicked(this, new RoutedEventArgs()); e.Handled = true; return;
            }
        }
    }

    private void OnEditorKeyDown(object sender, KeyEventArgs e)
    {
        var mods = Keyboard.Modifiers;
        if (mods == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.B: OnBoldClicked(this, new RoutedEventArgs()); e.Handled = true; return;
                case Key.I: OnItalicClicked(this, new RoutedEventArgs()); e.Handled = true; return;
                case Key.K: OnLinkClicked(this, new RoutedEventArgs()); e.Handled = true; return;
                case Key.F: OnFindClicked(this, new RoutedEventArgs()); e.Handled = true; return;
                case Key.V:
                    if (TryPasteImageFromClipboard())
                    {
                        e.Handled = true;
                        return;
                    }
                    break;
            }
        }
        if (mods == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            switch (e.Key)
            {
                case Key.C: OnCodeBlockClicked(this, new RoutedEventArgs()); e.Handled = true; return;
                case Key.L: OnTaskListClicked(this, new RoutedEventArgs()); e.Handled = true; return;
            }
        }
        if (mods == (ModifierKeys.Control | ModifierKeys.Alt) && e.Key == Key.T)
        {
            OnTableClicked(this, new RoutedEventArgs());
            e.Handled = true;
            return;
        }
        if (_app.Settings.Settings.AutoPairBrackets
            && mods == ModifierKeys.None
            && e.Key == Key.Back
            && Editor.SelectionLength == 0
            && Editor.CaretOffset > 0
            && Editor.CaretOffset < Editor.Document.TextLength)
        {
            char prev = Editor.Document.GetCharAt(Editor.CaretOffset - 1);
            char next = Editor.Document.GetCharAt(Editor.CaretOffset);
            if (AutoPairs.TryGetValue(prev, out char closer) && closer == next)
            {
                Editor.Document.Remove(Editor.CaretOffset - 1, 2);
                e.Handled = true;
                return;
            }
        }
    }

    private static readonly Dictionary<char, char> AutoPairs = new()
    {
        ['('] = ')',
        ['['] = ']',
        ['{'] = '}',
        ['"'] = '"',
        ['\''] = '\'',
        ['`'] = '`'
    };

    private void OnTextEntering(object? sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        if (!_app.Settings.Settings.AutoPairBrackets) return;
        if (string.IsNullOrEmpty(e.Text) || e.Text.Length != 1) return;

        char c = e.Text[0];
        bool isOpener = AutoPairs.ContainsKey(c);
        bool isCloser = AutoPairs.Values.Contains(c);

        // 1) Envolver selección con un par si se escribe un opener (o una comilla simétrica)
        if (isOpener && Editor.SelectionLength > 0)
        {
            string selected = Editor.SelectedText;
            int start = Editor.SelectionStart;
            char closingForSelection = AutoPairs[c];
            Editor.Document.Replace(start, Editor.SelectionLength, c + selected + closingForSelection);
            Editor.Select(start + 1, selected.Length);
            e.Handled = true;
            return;
        }

        char? rightChar = Editor.CaretOffset < Editor.Document.TextLength
            ? Editor.Document.GetCharAt(Editor.CaretOffset)
            : null;
        char? leftChar = Editor.CaretOffset > 0
            ? Editor.Document.GetCharAt(Editor.CaretOffset - 1)
            : null;

        // 2) "Saltar" el cierre si la app ya insertó automáticamente la pareja
        if (isCloser && rightChar == c)
        {
            Editor.CaretOffset++;
            e.Handled = true;
            return;
        }

        if (isOpener)
        {
            // Para comillas/backticks, no emparejar dentro de una palabra (don't, it's)
            // ni si justo después hay un carácter alfanumérico (evita comerse el cursor).
            bool isSymmetric = c == '"' || c == '\'' || c == '`';
            if (isSymmetric)
            {
                if (leftChar.HasValue && (char.IsLetterOrDigit(leftChar.Value))) return;
                if (rightChar.HasValue && char.IsLetterOrDigit(rightChar.Value)) return;
                if (rightChar == c)
                {
                    Editor.CaretOffset++;
                    e.Handled = true;
                    return;
                }
            }

            Editor.Document.Insert(Editor.CaretOffset, c.ToString() + AutoPairs[c]);
            Editor.CaretOffset--;
            e.Handled = true;
        }
    }

    private async void NewDocumentAsyncFireAndForget() => await NewDocumentAsync();
    private async void OpenDocumentAsyncFireAndForget() => await OpenDocumentAsync();
    private async void SaveDocumentAsyncFireAndForget() => await SaveDocumentAsync();
    private async void SaveDocumentAsAsyncFireAndForget() => await SaveDocumentAsAsync();

    private void PrefixCurrentLine(string prefix)
    {
        var line = Editor.Document.GetLineByOffset(Editor.CaretOffset);
        string text = Editor.Document.GetText(line.Offset, line.Length);
        if (!text.StartsWith(prefix, StringComparison.Ordinal))
        {
            Editor.Document.Insert(line.Offset, prefix);
        }
        Editor.Focus();
    }

    private void WrapSelection(string before, string after, string placeholder)
    {
        var selection = Editor.SelectedText;
        if (string.IsNullOrEmpty(selection))
        {
            int start = Editor.SelectionStart;
            Editor.Document.Insert(start, before + placeholder + after);
            Editor.Select(start + before.Length, placeholder.Length);
        }
        else
        {
            int start = Editor.SelectionStart;
            int length = Editor.SelectionLength;
            Editor.Document.Replace(start, length, before + selection + after);
            Editor.Select(start + before.Length, selection.Length);
        }
        Editor.Focus();
    }

    private void ReplaceSelection(string text)
    {
        int start = Editor.SelectionStart;
        int length = Editor.SelectionLength;
        Editor.Document.Replace(start, length, text);
        Editor.CaretOffset = start + text.Length;
        Editor.Focus();
    }

    private void InsertAtCaret(string text, int caretOffsetFromEnd = 0)
    {
        int offset = Editor.CaretOffset;
        Editor.Document.Insert(offset, text);
        Editor.CaretOffset = offset + text.Length - caretOffsetFromEnd;
        Editor.Focus();
    }

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        string[]? files = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (files == null || files.Length == 0) return;
        e.Handled = true;
        HandleDroppedFiles(files);
    }

    private void OnEditorPreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void OnEditorPreviewDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        string[]? files = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (files == null) return;
        e.Handled = true;
        HandleDroppedFiles(files);
    }

    private async void HandleDroppedFiles(string[] files)
    {
        var images = files.Where(ImageAssetService.IsImageFile).ToList();
        var markdownFiles = files.Where(f => !ImageAssetService.IsImageFile(f) && IsTextFile(f)).ToList();

        if (markdownFiles.Count > 0)
        {
            if (!await ConfirmDiscardChangesAsync()) return;
            await LoadDocumentAsync(markdownFiles[0]);
        }
        foreach (var image in images)
        {
            InsertImageFromDisk(image);
        }
    }

    private static bool IsTextFile(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".md" or ".markdown" or ".txt";
    }

    private void InsertImageFromDisk(string sourcePath)
    {
        try
        {
            string altText = Path.GetFileNameWithoutExtension(sourcePath);
            string url;
            if (_document.IsUntitled)
            {
                url = "file:///" + sourcePath.Replace("\\", "/");
            }
            else
            {
                string finalPath = sourcePath;
                if (_app.Settings.Settings.CopyImagesToAssets)
                {
                    bool insideDocDir = sourcePath.StartsWith(_document.DocumentDirectory!, StringComparison.OrdinalIgnoreCase);
                    if (!insideDocDir)
                    {
                        finalPath = _app.Images.CopyImageToAssets(_document.DocumentDirectory!, sourcePath);
                    }
                }
                url = _app.Images.BuildRelativePath(_document.DocumentDirectory!, finalPath);
            }
            string md = _app.Images.BuildMarkdownImageReference(altText, url);
            InsertAtCaret(md);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Loc.T("main.msg.insertImageError", ex.Message), "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_document.IsDirty)
        {
            e.Cancel = true;
            if (await ConfirmDiscardChangesAsync())
            {
                _document.IsDirty = false;
                _ = Dispatcher.BeginInvoke(new Action(Close), DispatcherPriority.Background);
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        try { _autoSaveTimer?.Stop(); } catch { }
        try { if (_draft != null) _app.Recovery.Delete(_draft); } catch { }
        base.OnClosed(e);
    }

    private void ApplyTitleBarTheme()
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle();
            if (hwnd == IntPtr.Zero) return;
            int useDark = App.ResolveTheme(_app.Settings.Settings.Theme) ? 1 : 0;
            int attr20 = 20;
            int hr = DwmSetWindowAttribute(hwnd, attr20, ref useDark, sizeof(int));
            if (hr != 0)
            {
                int attr19 = 19;
                DwmSetWindowAttribute(hwnd, attr19, ref useDark, sizeof(int));
            }
        }
        catch
        {
        }
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private void OnEditorScrollChanged(object? sender, EventArgs e)
    {
        if (!_previewReady || _viewMode != ViewMode.Split) return;
        var now = DateTime.UtcNow;
        if ((now - _lastPreviewScrollAppliedUtc).TotalMilliseconds < 250) return; // viene del preview
        if ((now - _lastScrollSyncSent).TotalMilliseconds < 60) return;
        _lastScrollSyncSent = now;

        try
        {
            var textView = Editor.TextArea.TextView;
            double scrollY = textView.ScrollOffset.Y;

            // Primera línea visible del editor: es la referencia común de sincronización.
            var firstDocLine = textView.GetDocumentLineByVisualTop(scrollY);
            int firstLine = firstDocLine?.LineNumber ?? 1;

            string payload;
            if (_flatOutline.Count > 0)
            {
                HeadingNode? prev = null;
                HeadingNode? next = null;
                foreach (var h in _flatOutline.OrderBy(h => h.LineNumber))
                {
                    if (h.LineNumber <= firstLine) prev = h;
                    else { next = h; break; }
                }
                int prevLine = prev?.LineNumber ?? 1;
                int nextLine = next?.LineNumber ?? Editor.Document.LineCount + 1;
                double span = Math.Max(nextLine - prevLine, 1);
                double frac = Math.Clamp((firstLine - prevLine) / span, 0, 1);
                payload = JsonSerializer.Serialize(new
                {
                    type = "sync-scroll",
                    prev = prev?.Anchor,
                    next = next?.Anchor,
                    frac
                });
            }
            else
            {
                double scrollable = Math.Max(textView.DocumentHeight - textView.ActualHeight, 1);
                double ratio = Math.Clamp(scrollY / scrollable, 0, 1);
                payload = JsonSerializer.Serialize(new { type = "scroll-to-ratio", ratio });
            }
            Preview.CoreWebView2?.PostWebMessageAsJson(payload);
        }
        catch
        {
        }
    }

    private void ApplyPreviewScrollRatio(double ratio)
    {
        if (_viewMode != ViewMode.Split) return;
        _lastPreviewScrollAppliedUtc = DateTime.UtcNow;
        double scrollable = Math.Max(Editor.ExtentHeight - Editor.ViewportHeight, 1);
        double clamped = Math.Clamp(ratio, 0.0, 1.0);
        Editor.ScrollToVerticalOffset(scrollable * clamped);
    }

    private void ApplyPreviewSyncBack(string? prevAnchor, string? nextAnchor, double frac, double fallbackRatio)
    {
        if (_viewMode != ViewMode.Split) return;
        _lastPreviewScrollAppliedUtc = DateTime.UtcNow;

        if (_flatOutline.Count == 0)
        {
            ApplyPreviewScrollRatio(fallbackRatio);
            return;
        }

        int prevLine = 1;
        int nextLine = Editor.Document.LineCount;
        if (!string.IsNullOrEmpty(prevAnchor))
        {
            var prev = _flatOutline.FirstOrDefault(h => string.Equals(h.Anchor, prevAnchor, StringComparison.OrdinalIgnoreCase));
            if (prev != null) prevLine = prev.LineNumber;
        }
        if (!string.IsNullOrEmpty(nextAnchor))
        {
            var next = _flatOutline.FirstOrDefault(h => string.Equals(h.Anchor, nextAnchor, StringComparison.OrdinalIgnoreCase));
            if (next != null) nextLine = next.LineNumber;
        }
        if (nextLine < prevLine) nextLine = Editor.Document.LineCount;

        double targetLine = prevLine + Math.Clamp(frac, 0, 1) * Math.Max(nextLine - prevLine, 0);
        int line = Math.Clamp((int)Math.Round(targetLine), 1, Editor.Document.LineCount);
        try
        {
            double top = Editor.TextArea.TextView.GetVisualTopByDocumentLine(line);
            Editor.ScrollToVerticalOffset(top);
        }
        catch
        {
            ApplyPreviewScrollRatio(fallbackRatio);
        }
    }

    private void ApplyFocusState(FocusState state)
    {
        _focusState = state;
        bool simple = state == FocusState.Simple;
        bool full = state == FocusState.Full;
        bool any = simple || full;

        FocusModeItem.IsChecked = simple;
        FocusFullModeItem.IsChecked = full;
        if (ToolbarFocusMode != null) ToolbarFocusMode.IsChecked = simple;
        if (ToolbarFocusFullMode != null) ToolbarFocusFullMode.IsChecked = full;

        // Foco (cualquier variante): sin panel lateral ni menú.
        if (any)
        {
            OutlineColumn.MinWidth = 0;
            OutlineColumn.Width = new GridLength(0);
            OutlineSplitterColumn.Width = new GridLength(0);
            MainMenu.Visibility = Visibility.Collapsed;
        }
        else
        {
            MainMenu.Visibility = Visibility.Visible;
            UpdateOutlineVisibility();
        }

        // Foco simple: conserva solo la fila principal de la toolbar (modos y foco);
        // la barra de formato se oculta para dejar "solo lo principal".
        if (MainToolBarTray != null)
        {
            MainToolBarTray.Visibility = full ? Visibility.Collapsed : Visibility.Visible;
        }
        if (FormatToolBar != null)
        {
            FormatToolBar.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
        }
        MainStatusBar.Visibility = full ? Visibility.Collapsed : Visibility.Visible;

        // Foco a pantalla completa: sin bordes y maximizada.
        if (full)
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
        }
        else
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
            if (WindowState == WindowState.Maximized && _previousWindowStateBeforeFocus != WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
        }

        FocusExitHint.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        if (FocusExitHintText != null)
        {
            FocusExitHintText.Text = full
                ? Loc.T("main.focus.fullHint")
                : Loc.T("main.focus.simpleHint");
        }
        if (any) Editor.Focus();
    }

    private WindowState _previousWindowStateBeforeFocus = WindowState.Normal;

    private void EnterFocusMode()
    {
        _previousWindowStateBeforeFocus = WindowState;
        ApplyFocusState(FocusState.Simple);
    }

    private void EnterFullFocusMode()
    {
        _previousWindowStateBeforeFocus = WindowState;
        ApplyFocusState(FocusState.Full);
    }

    private void ExitFocusMode() => ApplyFocusState(FocusState.None);

    private void ToggleFocusMode()
    {
        ApplyFocusState(_focusState == FocusState.Simple ? FocusState.None : FocusState.Simple);
    }

    private void ToggleFullFocusMode()
    {
        if (_focusState != FocusState.Full) _previousWindowStateBeforeFocus = WindowState;
        ApplyFocusState(_focusState == FocusState.Full ? FocusState.None : FocusState.Full);
    }

    private void OnFocusModeClicked(object sender, RoutedEventArgs e) => ToggleFocusMode();
    private void OnFocusFullModeClicked(object sender, RoutedEventArgs e) => ToggleFullFocusMode();

    private bool TryPasteImageFromClipboard()
    {
        try
        {
            if (!Clipboard.ContainsImage()) return false;
            var source = Clipboard.GetImage();
            if (source == null) return false;

            string fileName = $"image-{DateTime.Now:yyyyMMdd-HHmmss}.png";
            string url;
            if (_document.IsUntitled)
            {
                string tmpDir = Path.Combine(Path.GetTempPath(), "MarkLocal");
                Directory.CreateDirectory(tmpDir);
                string tmpPath = Path.Combine(tmpDir, fileName);
                SavePng(source, tmpPath);
                url = "file:///" + tmpPath.Replace("\\", "/");
            }
            else
            {
                string assetsFolder = string.IsNullOrWhiteSpace(_app.Settings.Settings.AssetsFolderName)
                    ? "assets"
                    : _app.Settings.Settings.AssetsFolderName;
                string assetsDir = Path.Combine(_document.DocumentDirectory!, assetsFolder);
                Directory.CreateDirectory(assetsDir);
                string target = Path.Combine(assetsDir, fileName);
                int counter = 1;
                while (File.Exists(target))
                {
                    target = Path.Combine(assetsDir, $"image-{DateTime.Now:yyyyMMdd-HHmmss}-{counter}.png");
                    counter++;
                }
                SavePng(source, target);
                url = _app.Images.BuildRelativePath(_document.DocumentDirectory!, target);
            }

            string md = _app.Images.BuildMarkdownImageReference(Loc.T("main.image.pastedAlt"), url);
            InsertAtCaret(md);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Loc.T("main.msg.pasteImageError", ex.Message), "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private static void SavePng(System.Windows.Media.Imaging.BitmapSource source, string path)
    {
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(source));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private void BuildTemplatesMenu()
    {
        TemplatesMenu.Items.Clear();
        var templates = _app.Templates.GetAvailable();
        if (templates.Count == 0)
        {
            TemplatesMenu.Items.Add(new MenuItem { Header = Loc.T("main.templates.empty"), IsEnabled = false });
        }
        else
        {
            foreach (var t in templates)
            {
                var item = new MenuItem { Header = t.Name, Tag = t, ToolTip = t.Path };
                item.Click += async (_, _) => await NewFromTemplateAsync((TemplateInfo)item.Tag);
                TemplatesMenu.Items.Add(item);
            }
        }
        TemplatesMenu.Items.Add(new Separator());
        var openFolder = new MenuItem { Header = Loc.T("main.templates.openFolder") };
        openFolder.Click += (_, _) =>
        {
            try
            {
                _app.Templates.EnsureDefaultTemplates();
                Process.Start(new ProcessStartInfo(_app.Templates.TemplatesDirectory) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Loc.T("main.msg.openTemplatesFolderError", ex.Message),
                    "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };
        TemplatesMenu.Items.Add(openFolder);

        var reload = new MenuItem { Header = Loc.T("main.templates.reload") };
        reload.Click += (_, _) => BuildTemplatesMenu();
        TemplatesMenu.Items.Add(reload);
    }

    private async Task NewFromTemplateAsync(TemplateInfo template)
    {
        if (!File.Exists(template.Path))
        {
            MessageBox.Show(this, Loc.T("main.msg.templateMissing"), "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Warning);
            BuildTemplatesMenu();
            return;
        }
        if (!await ConfirmDiscardChangesAsync()) return;

        try
        {
            string raw = await File.ReadAllTextAsync(template.Path);
            string expanded = TemplateService.ExpandTokens(raw);
            _document.FilePath = null;
            _document.EncodingName = "UTF-8";
            _document.LineEnding = _app.Settings.Settings.DefaultLineEnding;
            SetEditorText(expanded);
            _document.IsDirty = true;
            UpdateStatus();
            SchedulePreviewRender(immediate: true);
            Editor.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Loc.T("main.msg.templateLoadError", ex.Message),
                "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnInsertFrontMatterClicked(object sender, RoutedEventArgs e)
    {
        string snippet = "---\ntitle: \ndate: " + DateTime.Now.ToString("yyyy-MM-dd") + "\ntags: []\n---\n\n";
        int offset = Editor.CaretOffset;
        Editor.Document.Insert(offset, snippet);
        // dejar el caret en el value de title
        Editor.CaretOffset = offset + "---\ntitle: ".Length;
        Editor.Focus();
    }

    private void OnInsertCalloutNoteClicked(object sender, RoutedEventArgs e)
        => InsertAtCaret(Loc.T("main.snippet.calloutNote"));

    private void OnInsertCalloutWarningClicked(object sender, RoutedEventArgs e)
        => InsertAtCaret(Loc.T("main.snippet.calloutWarning"));

    private void OnInsertTocClicked(object sender, RoutedEventArgs e)
        => InsertAtCaret("\n[TOC]\n\n");

    private void RefreshSiblings()
    {
        string? dir = _document.DocumentDirectory;
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            SiblingsHeader.Visibility = Visibility.Collapsed;
            SiblingsList.Visibility = Visibility.Collapsed;
            SiblingsList.ItemsSource = null;
            return;
        }

        IEnumerable<FileInfo> files;
        try
        {
            files = new DirectoryInfo(dir)
                .EnumerateFiles()
                .Where(f => WorkspaceService.IsMarkdownFile(f.Name));
        }
        catch
        {
            SiblingsHeader.Visibility = Visibility.Collapsed;
            SiblingsList.Visibility = Visibility.Collapsed;
            return;
        }

        string? currentPath = _document.FilePath;
        var items = files
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(f => new SiblingItem
            {
                DisplayName = f.Name,
                FullPath = f.FullName,
                IsCurrent = string.Equals(f.FullName, currentPath, StringComparison.OrdinalIgnoreCase)
            })
            .ToList();

        SiblingsList.ItemsSource = items;
        var current = items.FirstOrDefault(i => i.IsCurrent);
        if (current != null) SiblingsList.SelectedItem = current;

        bool hasAny = items.Count > 0;
        SiblingsHeader.Visibility = hasAny ? Visibility.Visible : Visibility.Collapsed;
        SiblingsList.Visibility = hasAny ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnSiblingDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SiblingsList.SelectedItem is not SiblingItem item) return;
        if (string.Equals(item.FullPath, _document.FilePath, StringComparison.OrdinalIgnoreCase)) return;
        if (!await ConfirmDiscardChangesAsync()) return;
        await LoadDocumentAsync(item.FullPath);
    }

    private async void OnSiblingKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (SiblingsList.SelectedItem is not SiblingItem item) return;
        if (string.Equals(item.FullPath, _document.FilePath, StringComparison.OrdinalIgnoreCase)) return;
        if (await ConfirmDiscardChangesAsync())
        {
            await LoadDocumentAsync(item.FullPath);
            e.Handled = true;
        }
    }

    private class SiblingItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsCurrent { get; set; }
        public FontWeight FontWeight => IsCurrent ? FontWeights.Bold : FontWeights.Normal;
    }

    private void OnRevealInExplorerClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_document.IsUntitled && File.Exists(_document.FilePath!))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_document.FilePath}\"")
                {
                    UseShellExecute = true
                });
                return;
            }
            string? folder = _document.DocumentDirectory ?? _app.Workspace.RootPath;
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
            }
            else
            {
                MessageBox.Show(this, Loc.T("main.msg.saveOrOpenFolder"),
                    "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Loc.T("main.msg.openExplorerError", ex.Message),
                "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnToolBarLoaded(object sender, RoutedEventArgs e)
    {
        // WPF reserva sitio para el botón de overflow aunque todos los items tengan
        // OverflowMode=Never. Lo ocultamos por template, que es la vía estable.
        if (sender is not ToolBar toolBar) return;
        if (toolBar.Template?.FindName("OverflowButton", toolBar) is FrameworkElement overflowButton)
        {
            overflowButton.Visibility = Visibility.Collapsed;
        }
        if (toolBar.Template?.FindName("OverflowGrid", toolBar) is FrameworkElement overflowGrid)
        {
            overflowGrid.Visibility = Visibility.Collapsed;
        }
        if (toolBar.Template?.FindName("MainPanelBorder", toolBar) is FrameworkElement mainBorder)
        {
            mainBorder.Margin = new Thickness(0);
        }
    }

    private void OnOpenDocFolderAsWorkspaceClicked(object sender, RoutedEventArgs e)
    {
        string? folder = _document.DocumentDirectory;
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            MessageBox.Show(this, Loc.T("main.msg.saveFirstForFolder"),
                "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        OpenWorkspace(folder);
    }

    private void OnOpenDocumentFolderClicked(object sender, RoutedEventArgs e)
    {
        string? folder = _document.DocumentDirectory ?? _app.Workspace.RootPath;
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            MessageBox.Show(this, Loc.T("main.msg.saveOrOpenFolder"),
                "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Loc.T("main.msg.openFolderError", ex.Message),
                "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnOpenFolderClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = Loc.T("main.dlg.selectWorkspace")
        };
        if (!string.IsNullOrEmpty(_app.Workspace.RootPath))
        {
            dialog.InitialDirectory = _app.Workspace.RootPath;
        }
        else if (!string.IsNullOrEmpty(_document.DocumentDirectory))
        {
            dialog.InitialDirectory = _document.DocumentDirectory;
        }
        if (dialog.ShowDialog(this) == true)
        {
            OpenWorkspace(dialog.FolderName);
        }
    }

    private void OnCloseWorkspaceClicked(object sender, RoutedEventArgs e)
    {
        _app.Workspace.Close();
        _app.Settings.Settings.WorkspacePath = null;
        _app.Settings.Save();
        PopulateWorkspaceTree();
    }

    private void OnRefreshWorkspaceClicked(object sender, RoutedEventArgs e)
    {
        if (_app.Workspace.HasWorkspace) PopulateWorkspaceTree();
    }

    private void OnWorkspaceUpClicked(object sender, RoutedEventArgs e)
    {
        if (!_app.Workspace.HasWorkspace) return;
        string? parent = Path.GetDirectoryName(_app.Workspace.RootPath!);
        if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
        {
            MessageBox.Show(this, Loc.T("main.msg.atDriveRoot"), "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        OpenWorkspace(parent);
    }

    private void OpenWorkspace(string folderPath)
    {
        if (!_app.Workspace.Open(folderPath))
        {
            MessageBox.Show(this, Loc.T("main.msg.openFolderError", folderPath),
                "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _app.Settings.Settings.WorkspacePath = _app.Workspace.RootPath;
        _app.Settings.Save();
        PopulateWorkspaceTree();
    }

    private void PopulateWorkspaceTree()
    {
        WorkspaceTree.Items.Clear();
        if (!_app.Workspace.HasWorkspace)
        {
            WorkspaceRootLabel.Text = "";
            WorkspaceRootLabel.ToolTip = null;
            WorkspaceEmptyHint.Text = Loc.T("main.ws.noFolder");
            WorkspaceEmptyPanel.Visibility = Visibility.Visible;
            WorkspaceRefreshButton.IsEnabled = false;
            WorkspaceCloseButton.IsEnabled = false;
            WorkspaceUpButton.IsEnabled = false;
            return;
        }
        WorkspaceUpButton.IsEnabled = !string.IsNullOrEmpty(Path.GetDirectoryName(_app.Workspace.RootPath!));

        var root = _app.Workspace.BuildRoot();
        if (root == null)
        {
            WorkspaceRootLabel.Text = Loc.T("main.ws.inaccessible");
            WorkspaceEmptyHint.Text = Loc.T("main.ws.readError");
            WorkspaceEmptyPanel.Visibility = Visibility.Visible;
            WorkspaceRefreshButton.IsEnabled = false;
            WorkspaceCloseButton.IsEnabled = true;
            return;
        }

        WorkspaceRootLabel.Text = root.Name;
        WorkspaceRootLabel.ToolTip = root.FullPath;
        if (root.Children.Count == 0)
        {
            WorkspaceEmptyHint.Text = Loc.T("main.ws.emptyFolder");
            WorkspaceEmptyPanel.Visibility = Visibility.Visible;
        }
        else
        {
            WorkspaceEmptyPanel.Visibility = Visibility.Collapsed;
        }
        WorkspaceRefreshButton.IsEnabled = true;
        WorkspaceCloseButton.IsEnabled = true;

        foreach (var child in root.Children)
        {
            WorkspaceTree.Items.Add(BuildWorkspaceItem(child));
        }
    }

    private TreeViewItem BuildWorkspaceItem(WorkspaceNode node)
    {
        var item = new TreeViewItem
        {
            Header = node.Name,
            Tag = node,
            ToolTip = node.FullPath
        };
        if (node.Kind == WorkspaceNodeKind.Folder)
        {
            item.FontWeight = FontWeights.SemiBold;
            // Placeholder para que aparezca el expander; los hijos se cargan al expandir.
            item.Items.Add(new TreeViewItem { Header = Loc.T("main.ws.loading") });
            item.Expanded += OnWorkspaceFolderExpanded;

            var ctx = new ContextMenu();
            var setRoot = new MenuItem { Header = Loc.T("main.ws.setRoot") };
            setRoot.Click += (_, _) => OpenWorkspace(node.FullPath);
            ctx.Items.Add(setRoot);
            var openExplorer = new MenuItem { Header = Loc.T("main.ws.openInExplorer") };
            openExplorer.Click += (_, _) =>
            {
                try { Process.Start(new ProcessStartInfo(node.FullPath) { UseShellExecute = true }); } catch { }
            };
            ctx.Items.Add(openExplorer);
            item.ContextMenu = ctx;
        }
        else
        {
            string ext = Path.GetExtension(node.FullPath).ToLowerInvariant();
            if (ImageAssetService.IsImageFile(node.FullPath))
            {
                item.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["MutedForegroundBrush"];
            }
            else if (ext is ".txt")
            {
                item.Foreground = (System.Windows.Media.Brush)Application.Current.Resources["MutedForegroundBrush"];
            }
        }
        return item;
    }

    private void OnWorkspaceFolderExpanded(object sender, RoutedEventArgs e)
    {
        if (sender is not TreeViewItem item) return;
        if (item.Tag is not WorkspaceNode node) return;
        if (node.HasLoadedChildren) return;

        _app.Workspace.PopulateChildren(node);
        item.Items.Clear();
        foreach (var child in node.Children)
        {
            item.Items.Add(BuildWorkspaceItem(child));
        }
        if (node.Children.Count == 0)
        {
            item.Items.Add(new TreeViewItem
            {
                Header = Loc.T("main.ws.empty"),
                IsEnabled = false,
                FontStyle = FontStyles.Italic
            });
        }
    }

    private async void OnWorkspaceTreeDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var selected = WorkspaceTree.SelectedItem as TreeViewItem;
        if (selected?.Tag is not WorkspaceNode node) return;
        if (node.Kind != WorkspaceNodeKind.File) return;

        if (WorkspaceService.IsMarkdownFile(node.FullPath))
        {
            if (await ConfirmDiscardChangesAsync())
            {
                await LoadDocumentAsync(node.FullPath);
            }
        }
        else if (ImageAssetService.IsImageFile(node.FullPath))
        {
            if (_document.IsUntitled)
            {
                MessageBox.Show(this,
                    Loc.T("main.msg.saveFirstForImage"),
                    "MarkLocal", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            InsertImageFromDisk(node.FullPath);
        }
    }

    private async void OnWorkspaceTreeKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        var selected = WorkspaceTree.SelectedItem as TreeViewItem;
        if (selected?.Tag is not WorkspaceNode node) return;
        if (node.Kind == WorkspaceNodeKind.File && WorkspaceService.IsMarkdownFile(node.FullPath))
        {
            if (await ConfirmDiscardChangesAsync())
            {
                await LoadDocumentAsync(node.FullPath);
                e.Handled = true;
            }
        }
    }
}
