using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace PSPad.App.Markdown;

public static class MarkdownRenderer
{
    static readonly string[] ImageSchemes = ["http", "https"];

    static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UsePipeTables()
        .UseTaskLists()
        .UseAutoLinks()
        .Build();

    public static string ToHtml(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return "";
        }

        var document = Markdig.Markdown.Parse(markdown, Pipeline);

        foreach (var link in document.Descendants<LinkInline>().ToArray())
        {
            if (LinkSafety.IsSafe(link.Url, link.IsImage ? ImageSchemes : LinkSafety.LinkSchemes))
            {
                if (!link.IsImage)
                {
                    link.GetAttributes().AddPropertyIfNotExist("target", "_blank");
                    link.GetAttributes().AddPropertyIfNotExist("rel", "noopener noreferrer");
                }
            }
            else
            {
                // ReplaceBy keeps the link's children after the empty literal, so the label shows once
                link.ReplaceBy(new LiteralInline(""));
            }
        }

        foreach (var autolink in document.Descendants<AutolinkInline>().ToArray())
        {
            var url = autolink.IsEmail ? "mailto:" + autolink.Url : autolink.Url;
            if (LinkSafety.IsSafe(url, LinkSafety.LinkSchemes))
            {
                autolink.GetAttributes().AddPropertyIfNotExist("target", "_blank");
                autolink.GetAttributes().AddPropertyIfNotExist("rel", "noopener noreferrer");
            }
            else
            {
                autolink.ReplaceBy(new LiteralInline(autolink.Url));
            }
        }

        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        renderer.Render(document);
        return writer.ToString();
    }
}
