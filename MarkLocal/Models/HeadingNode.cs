using System.Collections.ObjectModel;

namespace MarkLocal.Models;

public class HeadingNode
{
    public int Level { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Anchor { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public ObservableCollection<HeadingNode> Children { get; } = new();
}
