using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace PSPad.App.Markdown;

public static class MarkdownRenderer
{
    static readonly string[] SafeSchemes = ["http", "https", "mailto"];

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
            if (IsSafe(link.Url))
            {
                link.GetAttributes().AddPropertyIfNotExist("target", "_blank");
                link.GetAttributes().AddPropertyIfNotExist("rel", "noopener noreferrer");
            }
            else
            {
                link.ReplaceBy(new LiteralInline(link.IsImage ? "" : PlainText(link)));
            }
        }

        foreach (var autolink in document.Descendants<AutolinkInline>().ToArray())
        {
            if (!IsSafe(autolink.IsEmail ? "mailto:" + autolink.Url : autolink.Url))
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

    static bool IsSafe(string? url) =>
        url is not null &&
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        SafeSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase);

    static string PlainText(ContainerInline container) =>
        string.Concat(container.Descendants<LiteralInline>().Select(literal => literal.Content.ToString()));
}
