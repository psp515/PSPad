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

    [Fact]
    public void UnsafeLinkTextIsNotDuplicated() =>
        Assert.Contains("<p>click</p>", MarkdownRenderer.ToHtml("[click](javascript:alert(1))"));

    [Fact]
    public void UnsafeLinkKeepsItsFormattingWithoutDuplication()
    {
        var html = MarkdownRenderer.ToHtml("[**bold** `code`](javascript:alert(1))");

        Assert.Contains("<p><strong>bold</strong> <code>code</code></p>", html);
    }

    [Fact]
    public void UnsafeImageShowsItsAltTextOnce()
    {
        var html = MarkdownRenderer.ToHtml("![alt text](javascript:alert(1))");

        Assert.Contains("<p>alt text</p>", html);
    }

    [Fact]
    public void RelativeLinkTextIsNotDuplicated() =>
        Assert.Contains("<p>x</p>", MarkdownRenderer.ToHtml("[x](/lists/1)"));

    [Fact]
    public void SafeAngleAutolinksOpenInANewTab()
    {
        var html = MarkdownRenderer.ToHtml("<https://example.com>");

        Assert.Contains("href=\"https://example.com\"", html);
        Assert.Contains("target=\"_blank\"", html);
        Assert.Contains("noopener", html);
    }

    [Fact]
    public void SafeEmailAutolinksOpenInANewTab()
    {
        var html = MarkdownRenderer.ToHtml("<a@b.c>");

        Assert.Contains("href=\"mailto:a@b.c\"", html);
        Assert.Contains("target=\"_blank\"", html);
        Assert.Contains("noopener", html);
    }

    [Fact]
    public void UnsafeAngleAutolinksBecomeText() =>
        Assert.DoesNotContain("href=\"javascript", MarkdownRenderer.ToHtml("<javascript:alert(1)>"), StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void ImagesRejectMailtoScheme()
    {
        var html = MarkdownRenderer.ToHtml("![i](mailto:a@b.c)");

        Assert.DoesNotContain("src=\"mailto", html, StringComparison.OrdinalIgnoreCase);
    }
    [Theory]
    [InlineData("![pixel](https://attacker.example/p.gif?u=1)")]
    [InlineData("![pixel](http://attacker.example/p.gif)")]
    [InlineData("![pixel](//attacker.example/p.gif)")]
    [InlineData("![pixel](/p.gif)")]
    [InlineData("![pixel](data:image/gif;base64,R0lGODlhAQABAAAAACw=)")]
    [InlineData("![pixel][p]\n\n[p]: https://attacker.example/p.gif")]
    [InlineData("[![pixel](https://attacker.example/p.gif)](https://example.com)")]
    [InlineData("<img src=\"https://attacker.example/p.gif\">")]
    [InlineData("<iframe src=\"https://attacker.example\"></iframe>")]
    [InlineData("<video src=\"https://attacker.example/v.mp4\"></video>")]
    [InlineData("<audio src=\"https://attacker.example/a.mp3\"></audio>")]
    [InlineData("<object data=\"https://attacker.example/o\"></object>")]
    [InlineData("<embed src=\"https://attacker.example/e\">")]
    [InlineData("<link rel=\"stylesheet\" href=\"https://attacker.example/s.css\">")]
    [InlineData("<style>@import url(https://attacker.example/s.css);</style>")]
    [InlineData("<p style=\"background:url(https://attacker.example/b.png)\">x</p>")]
    [InlineData("<picture><source srcset=\"https://attacker.example/p.gif\"></picture>")]
    [InlineData("| a |\n|---|\n| ![pixel](https://attacker.example/p.gif) |")]
    [InlineData("- [x] ![pixel](https://attacker.example/p.gif)")]
    public void NothingFetchesAResource(string markdown)
    {
        var html = MarkdownRenderer.ToHtml(markdown);

        foreach (var fetching in new[] { "<img", "src=\"", "srcset=\"", "data=\"", "style=\"", "<iframe", "<video", "<audio", "<source", "<object", "<embed", "<link", "<style", "<picture" })
        {
            Assert.DoesNotContain(fetching, html, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void SafeImageBecomesALinkLabelledByItsAltText()
    {
        var html = MarkdownRenderer.ToHtml("![diagram](https://example.com/d.png)");

        Assert.Contains("<a href=\"https://example.com/d.png\"", html);
        Assert.Contains(">diagram</a>", html);
        Assert.Contains("target=\"_blank\"", html);
        Assert.Contains("noopener", html);
    }

    [Fact]
    public void ImageWithoutAltTextIsLabelledByItsUrl() =>
        Assert.Contains(">https://example.com/d.png</a>", MarkdownRenderer.ToHtml("![](https://example.com/d.png)"));

    [Fact]
    public void ImageWithUnsafeSchemeShowsOnlyItsAltText() =>
        Assert.Contains("<p>pixel</p>", MarkdownRenderer.ToHtml("![pixel](data:image/gif;base64,R0lGODlhAQABAAAAACw=)"));

    [Fact]
    public void ImageInsideALinkShowsItsAltTextAsTheLinkLabel() =>
        Assert.Equal("<p><a href=\"https://example.com\" target=\"_blank\" rel=\"noopener noreferrer\">pixel</a></p>\n", MarkdownRenderer.ToHtml("[![pixel](https://attacker.example/p.gif)](https://example.com)"));
}
