using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using MarkLocal.Core;
using MarkLocal.Models;

namespace MarkLocal.Views;

public partial class RecoveryDialog : Window
{
    public DraftSnapshot? SelectedDraft { get; private set; }
    public List<DraftSnapshot> DraftsToDiscard { get; } = new();

    private readonly ObservableCollection<DraftViewModel> _items = new();

    public RecoveryDialog(IEnumerable<DraftSnapshot> drafts, string draftsDirectory)
    {
        InitializeComponent();
        foreach (var d in drafts)
        {
            _items.Add(new DraftViewModel(d));
        }
        DraftList.ItemsSource = _items;
        if (_items.Count > 0) DraftList.SelectedIndex = 0;
        HeaderText.Text = _items.Count == 1
            ? Loc.T("views.recovery.header.single")
            : Loc.T("views.recovery.header.multiple", _items.Count);
        FolderHint.Text = Loc.T("views.recovery.folderHint", draftsDirectory);
    }

    private void OnRecover(object sender, RoutedEventArgs e)
    {
        if (DraftList.SelectedItem is not DraftViewModel vm) return;
        SelectedDraft = vm.Snapshot;
        DialogResult = true;
        Close();
    }

    private void OnDiscardOne(object sender, RoutedEventArgs e)
    {
        if (DraftList.SelectedItem is not DraftViewModel vm) return;
        DraftsToDiscard.Add(vm.Snapshot);
        _items.Remove(vm);
        if (_items.Count == 0)
        {
            DialogResult = false;
            Close();
            return;
        }
        DraftList.SelectedIndex = 0;
        HeaderText.Text = _items.Count == 1
            ? Loc.T("views.recovery.remaining.single")
            : Loc.T("views.recovery.remaining.multiple", _items.Count);
    }

    private void OnDiscardAll(object sender, RoutedEventArgs e)
    {
        DraftsToDiscard.AddRange(_items.Select(v => v.Snapshot));
        _items.Clear();
        DialogResult = false;
        Close();
    }

    private void OnKeepAll(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        OnRecover(sender, e);
    }

    private class DraftViewModel
    {
        public DraftSnapshot Snapshot { get; }
        public DraftViewModel(DraftSnapshot snapshot) { Snapshot = snapshot; }
        public string Title => string.IsNullOrWhiteSpace(Snapshot.Title)
            ? (string.IsNullOrEmpty(Snapshot.OriginalPath) ? Loc.T("views.recovery.untitled") : Path.GetFileName(Snapshot.OriginalPath))
            : Snapshot.Title!;
        public string LocalModifiedText => Snapshot.LastModifiedUtc.ToLocalTime().ToString("g");
        public string SizeText
        {
            get
            {
                double kb = Snapshot.ContentLength / 1024d;
                if (kb < 1) return $"{Snapshot.ContentLength} B";
                return $"{kb:N1} KB";
            }
        }
        public string OriginalPathText => string.IsNullOrEmpty(Snapshot.OriginalPath)
            ? Loc.T("views.recovery.unsavedDocument")
            : Snapshot.OriginalPath!;
    }
}
