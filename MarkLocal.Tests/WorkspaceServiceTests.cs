using System;
using System.IO;
using System.Linq;
using MarkLocal.Core;
using MarkLocal.Models;
using Xunit;

namespace MarkLocal.Tests;

public class WorkspaceServiceTests
{
    [Fact]
    public void Open_NonExistingPath_ReturnsFalse()
    {
        var svc = new WorkspaceService();
        Assert.False(svc.Open(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())));
        Assert.False(svc.HasWorkspace);
    }

    [Fact]
    public void Open_ValidFolder_RaisesEventAndSetsRoot()
    {
        var svc = new WorkspaceService();
        string folder = TestFactory.CreateTempDir();
        bool raised = false;
        svc.WorkspaceChanged += (_, _) => raised = true;

        Assert.True(svc.Open(folder));
        Assert.True(svc.HasWorkspace);
        Assert.Equal(folder, svc.RootPath, ignoreCase: true);
        Assert.True(raised);
    }

    [Fact]
    public void Close_AfterOpen_ClearsRoot()
    {
        var svc = new WorkspaceService();
        svc.Open(TestFactory.CreateTempDir());
        svc.Close();
        Assert.False(svc.HasWorkspace);
        Assert.Null(svc.RootPath);
    }

    [Fact]
    public void BuildRoot_ListsMarkdownAndImages_InOrder_FoldersFirst()
    {
        string folder = TestFactory.CreateTempDir();
        File.WriteAllText(Path.Combine(folder, "b.md"), "# B");
        File.WriteAllText(Path.Combine(folder, "a.md"), "# A");
        File.WriteAllText(Path.Combine(folder, "ignorar.zip"), "skip");
        File.WriteAllBytes(Path.Combine(folder, "foto.png"), new byte[] { 1 });
        Directory.CreateDirectory(Path.Combine(folder, "sub"));
        File.WriteAllText(Path.Combine(folder, "sub", "x.md"), "# x");

        var svc = new WorkspaceService();
        svc.Open(folder);
        var root = svc.BuildRoot();
        Assert.NotNull(root);
        Assert.Equal(WorkspaceNodeKind.Folder, root!.Kind);
        Assert.Equal(4, root.Children.Count); // sub, a.md, b.md, foto.png

        Assert.Equal(WorkspaceNodeKind.Folder, root.Children[0].Kind);
        Assert.Equal("sub", root.Children[0].Name);
        Assert.Equal("a.md", root.Children[1].Name);
        Assert.Equal("b.md", root.Children[2].Name);
        Assert.Equal("foto.png", root.Children[3].Name);
        Assert.DoesNotContain(root.Children, n => n.Name == "ignorar.zip");
    }

    [Fact]
    public void BuildRoot_SkipsIgnoredFolders()
    {
        string folder = TestFactory.CreateTempDir();
        Directory.CreateDirectory(Path.Combine(folder, ".git"));
        Directory.CreateDirectory(Path.Combine(folder, "node_modules"));
        Directory.CreateDirectory(Path.Combine(folder, "bin"));
        Directory.CreateDirectory(Path.Combine(folder, "obj"));
        Directory.CreateDirectory(Path.Combine(folder, "docs"));
        File.WriteAllText(Path.Combine(folder, ".git", "head.md"), "no");
        File.WriteAllText(Path.Combine(folder, "docs", "intro.md"), "ok");

        var svc = new WorkspaceService();
        svc.Open(folder);
        var root = svc.BuildRoot();
        Assert.NotNull(root);
        Assert.Single(root!.Children, c => c.Name == "docs");
        Assert.DoesNotContain(root.Children, c => c.Name == ".git");
        Assert.DoesNotContain(root.Children, c => c.Name == "node_modules");
        Assert.DoesNotContain(root.Children, c => c.Name == "bin");
        Assert.DoesNotContain(root.Children, c => c.Name == "obj");
    }

    [Fact]
    public void PopulateChildren_PerformsLazyLoadOnSubfolder()
    {
        string folder = TestFactory.CreateTempDir();
        string sub = Path.Combine(folder, "sub");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "uno.md"), "#1");
        File.WriteAllText(Path.Combine(sub, "dos.md"), "#2");

        var svc = new WorkspaceService();
        svc.Open(folder);
        var root = svc.BuildRoot();
        Assert.NotNull(root);
        var subNode = root!.Children.Single();
        Assert.False(subNode.HasLoadedChildren);
        Assert.Empty(subNode.Children);

        svc.PopulateChildren(subNode);
        Assert.True(subNode.HasLoadedChildren);
        Assert.Equal(2, subNode.Children.Count);
        Assert.Equal("dos.md", subNode.Children[0].Name);
        Assert.Equal("uno.md", subNode.Children[1].Name);
    }

    [Theory]
    [InlineData("notes.md", true)]
    [InlineData("notes.markdown", true)]
    [InlineData("readme.txt", true)]
    [InlineData("foto.png", false)]
    [InlineData("README", false)]
    public void IsMarkdownFile_DetectsByExtension(string name, bool expected)
    {
        Assert.Equal(expected, WorkspaceService.IsMarkdownFile(name));
    }

    [Theory]
    [InlineData("notes.md", true)]
    [InlineData("foto.png", true)]
    [InlineData("foto.JPG", true)]
    [InlineData("data.zip", false)]
    [InlineData("Makefile", false)]
    public void IsSupportedFile_AcceptsTextAndImages(string name, bool expected)
    {
        Assert.Equal(expected, WorkspaceService.IsSupportedFile(name));
    }
}
