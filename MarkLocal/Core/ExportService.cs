using System.IO;
using System.Threading.Tasks;

namespace MarkLocal.Core;

public class ExportService
{
    private readonly MarkdownService _markdown;
    private readonly PreviewService _preview;

    public ExportService(MarkdownService markdown, PreviewService preview)
    {
        _markdown = markdown;
        _preview = preview;
    }

    public async Task ExportHtmlAsync(string markdownContent, string? sourceDirectory, string targetPath, bool darkTheme, double previewFontSize)
    {
        string body = _markdown.ConvertToHtml(markdownContent, sourceDirectory);
        string html = _preview.BuildHtml(body, darkTheme, previewFontSize);
        var targetDir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(targetDir)) Directory.CreateDirectory(targetDir);
        await File.WriteAllTextAsync(targetPath, html);
    }
}
