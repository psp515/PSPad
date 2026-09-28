using PSPad.App.Markdown;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Markdown;

[UnitTest]
public class MarkdownRendererTests
{
    [Fact]
    public void BlankIsNothing() => Assert.Equal("", MarkdownRenderer.ToHtml("  \n"));

    [Fact]
    public void HeadingsListsAndEmphasisRender()
    {
        var html = MarkdownRenderer.ToHtml("## Settings\n- **0.2 mm** layer\n- `215` °C");

        Assert.Contains("<h2", html);
        Assert.Contains("<strong>0.2 mm</strong>", html);
        Assert.Contains("<code>215</code>", html);
    }

    [Fact]
    public void RawHtmlIsEscaped()
    {
        var html = MarkdownRenderer.ToHtml("<script>alert(1)</script>\n\n<img src=x onerror=alert(1)>");

        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("<img", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Theory]
    [InlineData("[click](javascript:alert(1))")]
    [InlineData("[click](data:text/html;base64,PHNjcmlwdD4=)")]
    [InlineData("[click](vbscript:msgbox)")]
    [InlineData("<javascript:alert(1)>")]
    [InlineData("![x](javascript:alert(1))")]
    public void UnsafeLinksBecomeText(string markdown)
    {
        var html = MarkdownRenderer.ToHtml(markdown);

        Assert.DoesNotContain("href=\"javascript", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"data", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"vbscript", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("src=\"javascript", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SafeLinksOpenInANewTab()
    {
        var html = MarkdownRenderer.ToHtml("[docs](https://example.com)");

        Assert.Contains("href=\"https://example.com\"", html);
        Assert.Contains("target=\"_blank\"", html);
        Assert.Contains("noopener", html);
    }

    [Fact]
    public void MailtoLinksSurvive() => Assert.Contains("href=\"mailto:a@b.c\"", MarkdownRenderer.ToHtml("[mail](mailto:a@b.c)"));

    [Fact]
    public void TaskListCheckboxesAreDisabled()
    {
        var html = MarkdownRenderer.ToHtml("- [x] done\n- [ ] todo");

        Assert.Contains("type=\"checkbox\"", html);
        Assert.Contains("disabled", html);
    }

    [Fact]
    public void PipeTablesRender() => Assert.Contains("<table", MarkdownRenderer.ToHtml("| a | b |\n|---|---|\n| 1 | 2 |"));

    [Theory]
    [InlineData("[click]\n\n[click]: javascript:alert(1)")]
    [InlineData("[Click](JavaScript:alert(1))")]
    [InlineData("[Click](  javascript:alert(1))")]
    [InlineData("[Click](java\tscript:alert(1))")]
    [InlineData("[Click](jav&#x09;ascript:alert(1))")]
    [InlineData("[Click](jav&#x61;script:alert(1))")]
    [InlineData("![x](JAVASCRIPT:alert(1))")]
    [InlineData("![x](DATA:text/html,x)")]
    public void UnsafeLinksSurviveNoObfuscation(string markdown)
    {
        var html = MarkdownRenderer.ToHtml(markdown);

        Assert.DoesNotContain("href=\"javascript", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"data", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("src=\"javascript", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("src=\"data", html, StringComparison.OrdinalIgnoreCase);
    }
}
