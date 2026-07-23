using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MarkLocal.Models;

namespace MarkLocal.Core;

public class WorkspaceService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown", ".txt",
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".svg", ".ico"
    };

    private static readonly HashSet<string> IgnoredFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".hg", ".svn", "node_modules", "bin", "obj", ".vs", ".idea", "__pycache__"
    };

    public string? RootPath { get; private set; }
    public bool HasWorkspace => !string.IsNullOrEmpty(RootPath);

    public event EventHandler? WorkspaceChanged;

    public bool Open(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath)) return false;
        RootPath = Path.GetFullPath(folderPath);
        WorkspaceChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Close()
    {
        if (!HasWorkspace) return;
        RootPath = null;
        WorkspaceChanged?.Invoke(this, EventArgs.Empty);
    }

    public WorkspaceNode? BuildRoot()
    {
        if (!HasWorkspace) return null;
        var rootInfo = new DirectoryInfo(RootPath!);
        if (!rootInfo.Exists) return null;

        var root = new WorkspaceNode
        {
            Name = rootInfo.Name,
            FullPath = rootInfo.FullName,
            Kind = WorkspaceNodeKind.Folder
        };
        PopulateChildren(root);
        root.HasLoadedChildren = true;
        return root;
    }

    public void PopulateChildren(WorkspaceNode folder)
    {
        if (folder.Kind != WorkspaceNodeKind.Folder) return;
        folder.Children.Clear();
        if (!Directory.Exists(folder.FullPath))
        {
            folder.HasLoadedChildren = true;
            return;
        }

        IEnumerable<DirectoryInfo> dirs;
        IEnumerable<FileInfo> files;
        try
        {
            var info = new DirectoryInfo(folder.FullPath);
            dirs = info.EnumerateDirectories();
            files = info.EnumerateFiles();
        }
        catch (Exception)
        {
            folder.HasLoadedChildren = true;
            return;
        }

        foreach (var d in dirs
                     .Where(d => !d.Name.StartsWith(".", StringComparison.Ordinal) || d.Name.Equals(".github", StringComparison.OrdinalIgnoreCase))
                     .Where(d => !IgnoredFolderNames.Contains(d.Name))
                     .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
        {
            folder.Children.Add(new WorkspaceNode
            {
                Name = d.Name,
                FullPath = d.FullName,
                Kind = WorkspaceNodeKind.Folder
            });
        }

        foreach (var f in files
                     .Where(f => SupportedExtensions.Contains(f.Extension))
                     .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
        {
            folder.Children.Add(new WorkspaceNode
            {
                Name = f.Name,
                FullPath = f.FullName,
                Kind = WorkspaceNodeKind.File
            });
        }
        folder.HasLoadedChildren = true;
    }

    public static bool IsSupportedFile(string path)
    {
        string ext = Path.GetExtension(path);
        return SupportedExtensions.Contains(ext);
    }

    public static bool IsMarkdownFile(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".md" or ".markdown" or ".txt";
    }
}
