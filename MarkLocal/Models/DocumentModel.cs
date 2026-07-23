using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace MarkLocal.Models;

public class DocumentModel : INotifyPropertyChanged
{
    private string? _filePath;
    private string _content = string.Empty;
    private bool _isDirty;
    private string _encodingName = "UTF-8";
    private LineEnding _lineEnding = LineEnding.CRLF;

    public string? FilePath
    {
        get => _filePath;
        set { _filePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); OnPropertyChanged(nameof(IsUntitled)); OnPropertyChanged(nameof(DocumentDirectory)); }
    }

    public string Content
    {
        get => _content;
        set { _content = value; OnPropertyChanged(); }
    }

    public bool IsDirty
    {
        get => _isDirty;
        set { _isDirty = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayName)); }
    }

    public string EncodingName
    {
        get => _encodingName;
        set { _encodingName = value; OnPropertyChanged(); }
    }

    public LineEnding LineEnding
    {
        get => _lineEnding;
        set { _lineEnding = value; OnPropertyChanged(); }
    }

    public bool IsUntitled => string.IsNullOrEmpty(_filePath);

    public string DisplayName
    {
        get
        {
            var name = IsUntitled ? "Sin título.md" : Path.GetFileName(_filePath!);
            return _isDirty ? name + " *" : name;
        }
    }

    public string? DocumentDirectory => IsUntitled ? null : Path.GetDirectoryName(_filePath);

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
