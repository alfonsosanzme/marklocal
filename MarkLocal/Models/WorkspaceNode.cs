using System.Collections.ObjectModel;

namespace MarkLocal.Models;

public enum WorkspaceNodeKind
{
    Folder,
    File
}

public class WorkspaceNode
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public WorkspaceNodeKind Kind { get; set; }
    public ObservableCollection<WorkspaceNode> Children { get; } = new();
    public bool HasLoadedChildren { get; set; }
}
