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
    public void PhoneDrawsNoStampLineBecauseTheTopBarCarriesTheButton()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));

        var heading = Render<PageHeading>(parameters => parameters.Add(p => p.Title, "Inbox"));

        Assert.Empty(heading.FindAll(".pspad-sync-line"));
        Assert.Empty(heading.FindAll(".pspad-fine-pointer-only"));
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

    [Fact]
    public void TheTitleIsThePagesOnlyTopLevelHeading()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));

        var heading = Render<PageHeading>(parameters => parameters.Add(p => p.Title, "Inbox"));

        var title = heading.Find(".pspad-page-heading .pspad-page-title");
        Assert.Equal("H1", title.TagName);
        Assert.Contains("mud-typography-h5", title.ClassList);
        Assert.Equal("Inbox", title.TextContent);
    }

    [Fact]
    public void TheTitleIsNotInThePrimaryColour()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));

        var heading = Render<PageHeading>(parameters => parameters.Add(p => p.Title, "Inbox"));

        Assert.DoesNotContain("mud-primary-text", heading.Find(".pspad-page-heading h1").ClassList);
    }

    [Fact]
    public void TheSubtitleSitsUnderTheTitleInOneColumn()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));

        var heading = Render<PageHeading>(parameters => parameters
            .Add(p => p.Title, "Dom")
            .Add(p => p.Subtitle, "3 lists · 7 open tasks"));

        var text = heading.Find(".pspad-page-heading .pspad-page-heading-text");
        Assert.Contains("flex-column", text.ClassList);
        Assert.Equal("H1", text.Children[0].TagName);
        Assert.Contains("pspad-page-subtitle", text.Children[1].ClassList);
        Assert.Equal("3 lists · 7 open tasks", text.Children[1].TextContent.Trim());
    }

    [Fact]
    public void TheRowCentresItsPartsVertically()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));

        var heading = Render<PageHeading>(parameters => parameters.Add(p => p.Title, "Dom"));

        Assert.Contains("align-center", heading.Find(".pspad-page-heading").ClassList);
    }

    [Fact]
    public void AnIconSitsInADecorativeTileBeforeTheTitle()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));

        var heading = Render<PageHeading>(parameters => parameters
            .Add(p => p.Title, "Dom")
            .Add(p => p.Icon, MudBlazor.Icons.Material.Outlined.Folder)
            .Add(p => p.BackHref, "/areas")
            .Add(p => p.BackClass, "pspad-back"));

        var block = heading.Find(".pspad-page-heading");
        var tile = block.QuerySelector(".pspad-page-icon")!;
        Assert.Equal("true", tile.GetAttribute("aria-hidden"));
        Assert.Contains(IconPaths.DistinctivePath(MudBlazor.Icons.Material.Outlined.Folder), tile.InnerHtml);
        Assert.Contains("mud-primary-text", tile.QuerySelector("svg")!.ClassList);
        var order = block.Children.Select(child => child.ClassList.Contains("pspad-back") ? "back"
            : child.ClassList.Contains("pspad-page-icon") ? "icon"
            : child.ClassList.Contains("pspad-page-heading-text") ? "text" : null)
            .Where(part => part is not null).ToList();
        Assert.Equal(["back", "icon", "text"], order);
    }

    [Fact]
    public void NoIconMeansNoTile()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));

        var heading = Render<PageHeading>(parameters => parameters.Add(p => p.Title, "Inbox"));

        Assert.Empty(heading.FindAll(".pspad-page-icon"));
    }

    [Fact]
    public void ItHandsTheIconToTheShell()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));

        Render<PageHeading>(parameters => parameters
            .Add(p => p.Title, "Dom")
            .Add(p => p.Icon, MudBlazor.Icons.Material.Outlined.Folder));

        Assert.Equal(MudBlazor.Icons.Material.Outlined.Folder, Services.GetRequiredService<PageHeader>().Icon);
    }

    [Fact]
    public void NoSubtitleMeansNoSubtitleElement()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));

        var heading = Render<PageHeading>(parameters => parameters.Add(p => p.Title, "Inbox"));

        Assert.Empty(heading.FindAll(".pspad-page-subtitle"));
    }
}
