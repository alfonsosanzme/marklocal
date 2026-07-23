using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Ganss.Xss;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkLocal.Models;

namespace MarkLocal.Core;

public class MarkdownService
{
    private readonly MarkdownPipeline _pipeline;
    private readonly HtmlSanitizer _sanitizer;
    private readonly SettingsService _settings;

    public MarkdownService(SettingsService settings)
    {
        _settings = settings;
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseAutoLinks()
            .UseEmojiAndSmiley()
            .UsePipeTables()
            .UseTaskLists()
            .UseGridTables()
            .UseFootnotes()
            .UseAutoIdentifiers()
            .UseGenericAttributes()
            .UseYamlFrontMatter()
            .Build();

        _sanitizer = new HtmlSanitizer();
        _sanitizer.AllowedSchemes.Add("file");
        _sanitizer.AllowedSchemes.Add("data");
        _sanitizer.AllowedSchemes.Add("mailto");
        _sanitizer.AllowedAttributes.Add("class");
        _sanitizer.AllowedAttributes.Add("id");
        _sanitizer.AllowedAttributes.Add("checked");
        _sanitizer.AllowedAttributes.Add("disabled");
        _sanitizer.AllowedTags.Add("input");
    }

    public string ConvertToHtml(string markdown, string? baseDirectory)
    {
        if (string.IsNullOrEmpty(markdown)) return string.Empty;

        string processed = ResolveRelativeImagePaths(markdown, baseDirectory);
        processed = ExpandTocPlaceholders(processed);
        string html = Markdown.ToHtml(processed, _pipeline);

        if (!_settings.Settings.AllowInlineHtml)
        {
            html = _sanitizer.Sanitize(html);
        }
        return html;
    }

    private string ExpandTocPlaceholders(string markdown)
    {
        if (!ContainsTocPlaceholder(markdown)) return markdown;
        string tocHtml = BuildTocHtml(markdown);
        return ReplaceTocPlaceholder(markdown, tocHtml);
    }

    private static bool ContainsTocPlaceholder(string markdown)
    {
        bool inFence = false;
        foreach (var rawLine in markdown.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("```", System.StringComparison.Ordinal) || trimmed.StartsWith("~~~", System.StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }
            if (!inFence && string.Equals(line.Trim(), "[TOC]", System.StringComparison.Ordinal)) return true;
        }
        return false;
    }

    private static string ReplaceTocPlaceholder(string markdown, string tocHtml)
    {
        var sb = new StringBuilder();
        bool inFence = false;
        foreach (var rawLine in markdown.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("```", System.StringComparison.Ordinal) || trimmed.StartsWith("~~~", System.StringComparison.Ordinal))
            {
                inFence = !inFence;
                sb.AppendLine(line);
                continue;
            }
            if (!inFence && string.Equals(line.Trim(), "[TOC]", System.StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(tocHtml)) sb.AppendLine(tocHtml);
            }
            else
            {
                sb.AppendLine(line);
            }
        }
        return sb.ToString();
    }

    private string BuildTocHtml(string markdown)
    {
        var document = Markdown.Parse(markdown, _pipeline);
        var headings = new List<(int Level, string Text, string Id)>();
        foreach (var block in document.Descendants<HeadingBlock>())
        {
            string text = ExtractInlineText(block.Inline);
            string? id = block.GetAttributes().Id;
            if (string.IsNullOrEmpty(id)) id = SlugifyAnchor(text);
            if (string.IsNullOrEmpty(id)) continue;
            headings.Add((block.Level, text, id));
        }
        if (headings.Count == 0) return string.Empty;

        int minLevel = headings.Min(h => h.Level);
        var sb = new StringBuilder();
        sb.AppendLine("<div class=\"toc\">");
        int currentLevel = minLevel - 1;
        foreach (var h in headings)
        {
            while (currentLevel < h.Level) { sb.AppendLine("<ul>"); currentLevel++; }
            while (currentLevel > h.Level) { sb.AppendLine("</ul>"); currentLevel--; }
            sb.Append("<li><a href=\"#")
              .Append(WebUtility.HtmlEncode(h.Id))
              .Append("\">")
              .Append(WebUtility.HtmlEncode(h.Text))
              .AppendLine("</a></li>");
        }
        while (currentLevel >= minLevel) { sb.AppendLine("</ul>"); currentLevel--; }
        sb.AppendLine("</div>");
        return sb.ToString();
    }

    public IReadOnlyList<HeadingNode> ExtractOutline(string markdown)
    {
        var flat = new List<HeadingNode>();
        if (string.IsNullOrEmpty(markdown)) return flat;

        var document = Markdown.Parse(markdown, _pipeline);
        foreach (var heading in document.Descendants<HeadingBlock>())
        {
            string text = ExtractInlineText(heading.Inline);
            // El id real que emitirá el HTML lo genera la extensión AutoIdentifiers de
            // Markdig; usar el nuestro rompería la correspondencia con el preview.
            string? anchor = heading.GetAttributes().Id;
            if (string.IsNullOrEmpty(anchor)) anchor = SlugifyAnchor(text);
            flat.Add(new HeadingNode
            {
                Level = heading.Level,
                Text = text,
                Anchor = anchor,
                LineNumber = heading.Line + 1
            });
        }
        return BuildHierarchy(flat);
    }

    private static string ExtractInlineText(ContainerInline? container)
    {
        if (container == null) return string.Empty;
        var sb = new StringBuilder();
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    sb.Append(literal.Content.ToString());
                    break;
                case CodeInline code:
                    sb.Append(code.Content);
                    break;
                case EmphasisInline emph:
                    sb.Append(ExtractInlineText(emph));
                    break;
                case LinkInline link:
                    sb.Append(ExtractInlineText(link));
                    break;
                default:
                    sb.Append(inline.ToString());
                    break;
            }
        }
        return sb.ToString().Trim();
    }

    private static List<HeadingNode> BuildHierarchy(IReadOnlyList<HeadingNode> flat)
    {
        var root = new List<HeadingNode>();
        var stack = new Stack<HeadingNode>();
        foreach (var node in flat)
        {
            while (stack.Count > 0 && stack.Peek().Level >= node.Level) stack.Pop();
            if (stack.Count == 0) root.Add(node);
            else stack.Peek().Children.Add(node);
            stack.Push(node);
        }
        return root;
    }

    public static string SlugifyAnchor(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var sb = new StringBuilder();
        foreach (char c in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (c == ' ' || c == '-' || c == '_') sb.Append('-');
        }
        string slug = sb.ToString();
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return slug.Trim('-');
    }

    private static string ResolveRelativeImagePaths(string markdown, string? baseDirectory)
    {
        if (string.IsNullOrEmpty(baseDirectory)) return markdown;
        var sb = new StringBuilder(markdown.Length);
        int i = 0;
        while (i < markdown.Length)
        {
            if (markdown[i] == '!' && i + 1 < markdown.Length && markdown[i + 1] == '[')
            {
                int closeBracket = markdown.IndexOf(']', i + 2);
                if (closeBracket > 0 && closeBracket + 1 < markdown.Length && markdown[closeBracket + 1] == '(')
                {
                    int closeParen = FindMatchingParen(markdown, closeBracket + 1);
                    if (closeParen > 0)
                    {
                        string altText = markdown.Substring(i + 2, closeBracket - (i + 2));
                        string urlSection = markdown.Substring(closeBracket + 2, closeParen - (closeBracket + 2));
                        string newUrl = RewriteImageUrl(urlSection, baseDirectory);
                        sb.Append("![").Append(altText).Append("](").Append(newUrl).Append(')');
                        i = closeParen + 1;
                        continue;
                    }
                }
            }
            sb.Append(markdown[i]);
            i++;
        }
        return sb.ToString();
    }

    private static int FindMatchingParen(string text, int openParenIndex)
    {
        int depth = 0;
        for (int i = openParenIndex; i < text.Length; i++)
        {
            if (text[i] == '(') depth++;
            else if (text[i] == ')')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    private static string RewriteImageUrl(string urlSection, string baseDirectory)
    {
        string trimmed = urlSection.Trim();
        string url = trimmed;
        string title = string.Empty;
        int spaceIdx = trimmed.IndexOf(' ');
        if (spaceIdx > 0)
        {
            url = trimmed.Substring(0, spaceIdx);
            title = " " + trimmed.Substring(spaceIdx + 1);
        }

        if (url.StartsWith("http://") || url.StartsWith("https://") || url.StartsWith("data:") || url.StartsWith("file:"))
        {
            return urlSection;
        }
        if (Path.IsPathRooted(url))
        {
            return "file:///" + url.Replace("\\", "/") + title;
        }
        string combined = Path.GetFullPath(Path.Combine(baseDirectory, url));
        return "file:///" + combined.Replace("\\", "/") + title;
    }
}
