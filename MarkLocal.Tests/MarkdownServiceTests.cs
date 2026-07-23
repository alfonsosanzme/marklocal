using System.IO;
using System.Linq;
using MarkLocal.Core;
using Xunit;

namespace MarkLocal.Tests;

public class MarkdownServiceTests
{
    [Fact]
    public void Headings_AreRenderedAsHtmlHeadings()
    {
        var (_, md) = TestFactory.CreateMarkdownPair();
        string html = md.ConvertToHtml("# Título 1\n\n## Sub\n\n### Más", null);
        Assert.Contains("<h1", html);
        Assert.Contains("<h2", html);
        Assert.Contains("<h3", html);
        Assert.Contains("Título 1", html);
    }

    [Fact]
    public void UnorderedAndOrderedLists_AreRendered()
    {
        var (_, md) = TestFactory.CreateMarkdownPair();
        string html = md.ConvertToHtml("- a\n- b\n\n1. x\n2. y", null);
        Assert.Contains("<ul>", html);
        Assert.Contains("<ol>", html);
        Assert.Contains("<li>a</li>", html);
        Assert.Contains("<li>x</li>", html);
    }

    [Fact]
    public void GfmTables_AreRendered()
    {
        var (_, md) = TestFactory.CreateMarkdownPair();
        string source = "| A | B |\n| --- | --- |\n| 1 | 2 |\n";
        string html = md.ConvertToHtml(source, null);
        Assert.Contains("<table>", html);
        Assert.Contains("<th>A</th>", html);
        Assert.Contains("<td>1</td>", html);
    }

    [Fact]
    public void TaskLists_RenderCheckboxes()
    {
        var (_, md) = TestFactory.CreateMarkdownPair();
        string html = md.ConvertToHtml("- [ ] pendiente\n- [x] hecho", null);
        Assert.Contains("type=\"checkbox\"", html);
        Assert.Contains("checked", html);
    }

    [Fact]
    public void RelativeImagePath_IsResolvedAgainstBaseDirectory()
    {
        string baseDir = TestFactory.CreateTempDir();
        string assetsDir = Path.Combine(baseDir, "assets");
        Directory.CreateDirectory(assetsDir);
        string imgPath = Path.Combine(assetsDir, "foto.png");
        File.WriteAllBytes(imgPath, new byte[] { 1, 2, 3 });

        var (_, md) = TestFactory.CreateMarkdownPair();
        string html = md.ConvertToHtml("![alt](assets/foto.png)", baseDir);
        Assert.Contains("<img ", html);
        Assert.Contains("file:///", html);
        Assert.Contains("foto.png", html);
        Assert.DoesNotContain("src=\"assets/foto.png\"", html); // Debe ser reescrito a una ruta absoluta file://
    }

    [Fact]
    public void HttpImage_IsNotRewritten()
    {
        var (_, md) = TestFactory.CreateMarkdownPair();
        string html = md.ConvertToHtml("![](https://example.com/foo.png)", "C:/dir");
        Assert.Contains("https://example.com/foo.png", html);
        Assert.DoesNotContain("file:///", html);
    }

    [Fact]
    public void DataUriImage_IsNotRewritten()
    {
        var (_, md) = TestFactory.CreateMarkdownPair();
        string html = md.ConvertToHtml("![](data:image/png;base64,AAAA)", "C:/dir");
        Assert.Contains("data:image/png;base64,AAAA", html);
    }

    [Fact]
    public void InlineHtml_IsSanitized_ByDefault()
    {
        var (_, md) = TestFactory.CreateMarkdownPair();
        string html = md.ConvertToHtml("<script>alert(1)</script>\n\n<p onclick='x'>hola</p>", null);
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("onclick", html);
    }

    [Fact]
    public void InlineHtml_IsKept_WhenAllowed()
    {
        var (_, md) = TestFactory.CreateMarkdownPair(allowInlineHtml: true);
        string html = md.ConvertToHtml("<div class=\"x\">hola</div>", null);
        Assert.Contains("<div", html);
        Assert.Contains("class=\"x\"", html);
    }

    [Fact]
    public void ExtractOutline_BuildsNestedHierarchy()
    {
        var (_, md) = TestFactory.CreateMarkdownPair();
        string source = "# Uno\n\n## Uno A\n\n## Uno B\n\n### Hijo\n\n# Dos";
        var outline = md.ExtractOutline(source);
        Assert.Equal(2, outline.Count);
        Assert.Equal("Uno", outline[0].Text);
        Assert.Equal(2, outline[0].Children.Count);
        Assert.Equal("Uno A", outline[0].Children[0].Text);
        Assert.Single(outline[0].Children[1].Children);
        Assert.Equal("Hijo", outline[0].Children[1].Children[0].Text);
        Assert.Equal("Dos", outline[1].Text);
    }

    [Fact]
    public void TocPlaceholder_IsExpandedAsHtmlList()
    {
        var (_, md) = TestFactory.CreateMarkdownPair();
        string source = "# Uno\n\n[TOC]\n\n## Sub\n\n## Sub 2\n\n### Subsub";
        string html = md.ConvertToHtml(source, null);
        Assert.Contains("class=\"toc\"", html);
        Assert.Contains("href=\"#uno\"", html);
        Assert.Contains("href=\"#sub\"", html);
        Assert.Contains("href=\"#subsub\"", html);
        Assert.Contains("Sub 2", html);
    }

    [Fact]
    public void TocPlaceholder_InsideCodeBlock_IsLeftAsIs()
    {
        var (_, md) = TestFactory.CreateMarkdownPair();
        string source = "# H\n\n```\n[TOC]\n```\n";
        string html = md.ConvertToHtml(source, null);
        Assert.DoesNotContain("class=\"toc\"", html);
        Assert.Contains("[TOC]", html);
    }

    [Fact]
    public void TocPlaceholder_WithoutHeadings_IsSimplyRemoved()
    {
        var (_, md) = TestFactory.CreateMarkdownPair();
        string html = md.ConvertToHtml("Solo un párrafo.\n\n[TOC]\n", null);
        Assert.DoesNotContain("class=\"toc\"", html);
        Assert.DoesNotContain("[TOC]", html);
        Assert.Contains("Solo un párrafo", html);
    }

    [Fact]
    public void ExtractOutline_AnchorsMatchRenderedHtmlIds()
    {
        var (_, md) = TestFactory.CreateMarkdownPair();
        string source = "# Título con acentos áéí\n\n## Sección número 2 (rara: ¡signos!)";
        var outline = md.ExtractOutline(source);
        string html = md.ConvertToHtml(source, null);
        Assert.Single(outline);
        Assert.Contains($"id=\"{outline[0].Anchor}\"", html);
        Assert.Single(outline[0].Children);
        Assert.Contains($"id=\"{outline[0].Children[0].Anchor}\"", html);
    }

    [Fact]
    public void ExtractOutline_PreservesLineNumbers()
    {
        var (_, md) = TestFactory.CreateMarkdownPair();
        var outline = md.ExtractOutline("texto\n\n# H1\n\nmás\n\n## H2");
        Assert.Equal(3, outline[0].LineNumber);
        Assert.Equal(7, outline[0].Children[0].LineNumber);
    }

    [Theory]
    [InlineData("Título 1", "título-1")]
    [InlineData("Hello World", "hello-world")]
    [InlineData("  espacios  ", "espacios")]
    [InlineData("a--b", "a-b")]
    [InlineData("***muy** raro!", "muy-raro")]
    [InlineData("!!!", "")]
    public void SlugifyAnchor_NormalisesText(string input, string expected)
    {
        Assert.Equal(expected, MarkdownService.SlugifyAnchor(input));
    }
}
