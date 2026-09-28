using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace PSPad.App.Markdown;

public static class MarkdownRenderer
{
    static readonly string[] LinkSchemes = ["http", "https", "mailto"];
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
            if (IsSafe(link.Url, link.IsImage ? ImageSchemes : LinkSchemes))
            {
                if (!link.IsImage)
                {
                    link.GetAttributes().AddPropertyIfNotExist("target", "_blank");
                    link.GetAttributes().AddPropertyIfNotExist("rel", "noopener noreferrer");
                }
            }
            else
            {
                link.ReplaceBy(new LiteralInline(""));
            }
        }

        foreach (var autolink in document.Descendants<AutolinkInline>().ToArray())
        {
            var url = autolink.IsEmail ? "mailto:" + autolink.Url : autolink.Url;
            if (IsSafe(url, LinkSchemes))
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

    static bool IsSafe(string? url, string[] schemes) =>
        url is not null &&
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        schemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase);
}
