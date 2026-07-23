using System;
using System.Text.RegularExpressions;
using System.Windows;
using ICSharpCode.AvalonEdit;
using MarkLocal.Core;

namespace MarkLocal.Views;

public partial class FindReplaceDialog : Window
{
    private readonly TextEditor _editor;

    public FindReplaceDialog(TextEditor editor)
    {
        InitializeComponent();
        _editor = editor;
        Loaded += (_, _) => FindBox.Focus();
    }

    private Regex? BuildRegex()
    {
        string pattern = FindBox.Text ?? string.Empty;
        if (string.IsNullOrEmpty(pattern)) return null;
        string escaped = Regex.Escape(pattern);
        if (WholeWordCheck.IsChecked == true) escaped = $"\\b{escaped}\\b";
        var options = MatchCaseCheck.IsChecked == true ? RegexOptions.None : RegexOptions.IgnoreCase;
        try
        {
            return new Regex(escaped, options);
        }
        catch
        {
            return null;
        }
    }

    private void OnFindNext(object sender, RoutedEventArgs e)
    {
        var regex = BuildRegex();
        if (regex == null) return;
        string text = _editor.Document.Text;
        int start = _editor.SelectionStart + _editor.SelectionLength;
        var match = regex.Match(text, Math.Min(start, text.Length));
        if (!match.Success)
        {
            match = regex.Match(text);
            if (!match.Success)
            {
                StatusText.Text = Loc.T("views.find.noMatches");
                return;
            }
            StatusText.Text = Loc.T("views.find.wrapped");
        }
        else
        {
            StatusText.Text = Loc.T("views.find.matchAt", match.Index);
        }
        _editor.Select(match.Index, match.Length);
        _editor.ScrollToLine(_editor.Document.GetLocation(match.Index).Line);
    }

    private void OnReplaceOnce(object sender, RoutedEventArgs e)
    {
        var regex = BuildRegex();
        if (regex == null) return;
        if (_editor.SelectionLength > 0 && regex.IsMatch(_editor.SelectedText))
        {
            int start = _editor.SelectionStart;
            string replacement = ReplaceBox.Text ?? string.Empty;
            _editor.Document.Replace(start, _editor.SelectionLength, replacement);
            _editor.Select(start, replacement.Length);
        }
        OnFindNext(sender, e);
    }

    private void OnReplaceAll(object sender, RoutedEventArgs e)
    {
        var regex = BuildRegex();
        if (regex == null) return;
        string text = _editor.Document.Text;
        string replacement = ReplaceBox.Text ?? string.Empty;
        var matches = regex.Matches(text);
        if (matches.Count == 0)
        {
            StatusText.Text = Loc.T("views.find.noMatches");
            return;
        }
        string updated = regex.Replace(text, replacement);
        _editor.Document.Text = updated;
        StatusText.Text = Loc.T("views.find.replacedCount", matches.Count);
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
