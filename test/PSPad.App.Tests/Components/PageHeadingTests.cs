using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PSPad.App.Components;
using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class PageHeadingTests : Bunit.TestContext
{
    [Fact]
    public void ItHandsTheTitleToTheShell()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));

        Render<PageHeading>(parameters => parameters
            .Add(p => p.Title, "Shopping")
            .Add(p => p.Subtitle, "Home")
            .Add(p => p.BackHref, "/areas/1"));

        var header = Services.GetRequiredService<PageHeader>();
        Assert.Equal("Shopping", header.Title);
        Assert.Equal("Home", header.Subtitle);
        Assert.Equal("/areas/1", header.BackHref);
    }

    [Fact]
    public void ItDrawsTheTitleOnDesktopOnly()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));

        var heading = Render<PageHeading>(parameters => parameters.Add(p => p.Title, "Inbox"));

        var block = heading.Find(".pspad-page-heading");
        Assert.Contains("d-none", block.ClassList);
        Assert.Contains("d-md-flex", block.ClassList);
        Assert.Contains("Inbox", block.TextContent);
    }

    [Fact]
    public void DesktopShowsTheStampWithARefreshButtonOnTheRight()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));

        var heading = Render<PageHeading>(parameters => parameters.Add(p => p.Title, "Inbox"));

        var block = heading.Find(".pspad-page-heading");
        Assert.NotEmpty(block.QuerySelectorAll(".pspad-sync-stamp"));
        Assert.NotEmpty(block.QuerySelectorAll(".pspad-sync-button"));
    }

    [Fact]
    public void PhoneShowsTheStampAndAButtonOnlyForAMouse()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));

        var heading = Render<PageHeading>(parameters => parameters.Add(p => p.Title, "Inbox"));

        var line = heading.Find(".pspad-sync-line");
        Assert.Contains("d-md-none", line.ClassList);
        Assert.NotEmpty(line.QuerySelectorAll(".pspad-sync-stamp"));
        var button = line.QuerySelector(".pspad-fine-pointer-only .pspad-sync-button");
        Assert.NotNull(button);
        Assert.Single(line.QuerySelectorAll(".pspad-sync-button"));
    }

    [Fact]
    public void ABackLinkKeepsItsClassAndLabel()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));

        var heading = Render<PageHeading>(parameters => parameters
            .Add(p => p.Title, "Shopping")
            .Add(p => p.BackHref, "/areas/1")
            .Add(p => p.BackLabel, "Back to area")
            .Add(p => p.BackClass, "pspad-back-to-area"));

        var back = heading.Find(".pspad-back-to-area");
        Assert.Equal("/areas/1", back.GetAttribute("href"));
        Assert.Equal("Back to area", back.GetAttribute("aria-label"));
    }

    [Fact]
    public void AnEmptyTitleDrawsNothingButStillClearsTheShell()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));
        var header = Services.GetRequiredService<PageHeader>();
        header.Set("Old", "Old", "/old");

        var heading = Render<PageHeading>(parameters => parameters.Add(p => p.Title, ""));

        Assert.Empty(heading.FindAll(".pspad-page-heading"));
        Assert.Equal("", header.Title);
        Assert.Null(header.BackHref);
    }
}
