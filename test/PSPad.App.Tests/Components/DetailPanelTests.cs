using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PSPad.App.Components;
using PSPad.App.State.Viewport;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class DetailPanelTests : Bunit.TestContext
{
    public DetailPanelTests() => AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));

    [Fact]
    public void ClosedItRendersNoContent()
    {
        var panel = Render<DetailPanel>(parameters => parameters
            .Add(p => p.Open, false)
            .Add(p => p.ChildContent, (RenderFragment)(builder => builder.AddContent(0, "Body text"))));

        Assert.DoesNotContain("Body text", panel.Markup);
    }

    [Fact]
    public void OpenItShowsTitleHeaderAndContent()
    {
        var panel = Render<DetailPanel>(parameters => parameters
            .Add(p => p.Open, true)
            .Add(p => p.Title, "Task")
            .Add(p => p.Header, (RenderFragment)(builder => builder.AddContent(0, "Header text")))
            .Add(p => p.ChildContent, (RenderFragment)(builder => builder.AddContent(0, "Body text"))));

        Assert.Contains("Task", panel.Markup);
        Assert.Contains("Header text", panel.Markup);
        Assert.Contains("Body text", panel.Markup);
    }

    [Fact]
    public void TheCloseButtonSitsBeforeTheTitleAndRaisesOnClose()
    {
        var closed = false;

        var panel = Render<DetailPanel>(parameters => parameters
            .Add(p => p.Open, true)
            .Add(p => p.Title, "Task")
            .Add(p => p.OnClose, () => closed = true));

        var markup = panel.Markup;
        Assert.True(markup.IndexOf("pspad-panel-close", StringComparison.Ordinal)
                    < markup.IndexOf(">Task<", StringComparison.Ordinal));
        panel.Find(".pspad-panel-close").Click();
        Assert.True(closed);
    }

    [Fact]
    public void SaveSitsLeftOfDeleteAndBothRaiseTheirCallbacks()
    {
        var saved = false;
        var deleted = false;

        var panel = Render<DetailPanel>(parameters => parameters
            .Add(p => p.Open, true)
            .Add(p => p.OnSave, () => saved = true)
            .Add(p => p.OnDelete, () => deleted = true));

        var markup = panel.Markup;
        Assert.True(markup.IndexOf("pspad-panel-save", StringComparison.Ordinal)
                    < markup.IndexOf("pspad-panel-delete", StringComparison.Ordinal));
        panel.Find(".pspad-panel-save").Click();
        panel.Find(".pspad-panel-delete").Click();
        Assert.True(saved);
        Assert.True(deleted);
        Assert.Contains("mud-button-text-error", panel.Find(".pspad-panel-delete").ClassName);
    }

    [Fact]
    public void WithoutCallbacksThereIsNoActionBar()
    {
        var panel = Render<DetailPanel>(parameters => parameters.Add(p => p.Open, true));

        Assert.Empty(panel.FindAll(".pspad-panel-save"));
        Assert.Empty(panel.FindAll(".pspad-panel-delete"));
    }

    [Fact]
    public void SaveIsDisabledUntilItCanSave()
    {
        var panel = Render<DetailPanel>(parameters => parameters
            .Add(p => p.Open, true)
            .Add(p => p.CanSave, false)
            .Add(p => p.OnSave, () => { }));

        Assert.True(panel.Find(".pspad-panel-save").HasAttribute("disabled"));
    }

    [Fact]
    public void OnADesktopViewportItKeepsAFixedColumnWidth()
    {
        var panel = Render<DetailPanel>(parameters => parameters.Add(p => p.Open, true));

        Assert.Contains("360px", panel.Find(".mud-drawer").GetAttribute("style"));
    }

    [Fact]
    public void OnASmallViewportItFillsTheScreen()
    {
        Services.AddSingleton<IViewport>(new AppTestHost.FakeViewport(isDesktop: false));

        var panel = Render<DetailPanel>(parameters => parameters.Add(p => p.Open, true));

        Assert.Contains("100%", panel.Find(".mud-drawer").GetAttribute("style"));
    }

    [Fact]
    public void TheFooterIsPinnedBelowTheScrollingContent()
    {
        var panel = Render<DetailPanel>(parameters => parameters
            .Add(p => p.Open, true)
            .Add(p => p.ChildContent, (RenderFragment)(builder => builder.AddContent(0, "Body text")))
            .Add(p => p.Footer, (RenderFragment)(builder => builder.AddContent(0, "Footer text"))));

        var footer = panel.Find(".pspad-panel-footer");
        Assert.Contains("Footer text", footer.TextContent);
        Assert.True(panel.Markup.IndexOf("Body text", StringComparison.Ordinal)
                    < panel.Markup.IndexOf("Footer text", StringComparison.Ordinal));
    }

    [Fact]
    public void DividersDoNotStretchIntoGaps()
    {
        var panel = Render<DetailPanel>(parameters => parameters
            .Add(p => p.Open, true)
            .Add(p => p.Footer, (RenderFragment)(builder => builder.AddContent(0, "Footer text"))));

        Assert.All(panel.FindAll("hr.mud-divider"), divider => Assert.Contains("flex-grow-0", divider.ClassName));
    }
}
