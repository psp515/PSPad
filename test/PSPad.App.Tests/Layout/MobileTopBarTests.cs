using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PSPad.App.Layout;
using PSPad.App.State;
using PSPad.App.Sync;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class MobileTopBarTests : Bunit.TestContext
{
    [Fact]
    public void ItIsPhoneOnly()
    {
        Arrange();

        var bar = RenderBar();

        Assert.Contains("d-md-none", bar.Find(".mud-appbar").ClassList);
    }

    [Fact]
    public void ItShowsTheTitleAndFollowsChanges()
    {
        Arrange();
        var header = Services.GetRequiredService<PageHeader>();
        header.Set("Inbox", null, null);
        var bar = RenderBar();

        header.Set("Goals", null, null);

        bar.WaitForAssertion(() => Assert.Contains("Goals", bar.Find(".pspad-top-title").TextContent));
    }

    [Fact]
    public void TheBackArrowAppearsOnlyOnNestedScreens()
    {
        Arrange();
        var header = Services.GetRequiredService<PageHeader>();
        header.Set("Inbox", null, null);
        var bar = RenderBar();
        Assert.Empty(bar.FindAll(".pspad-top-back"));

        header.Set("Shopping", "Home", "/areas/1");

        bar.WaitForAssertion(() =>
            Assert.Equal("/areas/1", bar.Find(".pspad-top-back").GetAttribute("href")));
        Assert.Contains("Home", bar.Find(".pspad-top-title").TextContent);
    }

    [Fact]
    public void TheBackButtonCarriesItsOwnLabel()
    {
        Arrange();
        var header = Services.GetRequiredService<PageHeader>();
        var bar = RenderBar();

        header.Set("Shopping", "Home", "/areas/1", "Back to area");

        bar.WaitForAssertion(() =>
            Assert.Equal("Back to area", bar.Find(".pspad-top-back").GetAttribute("aria-label")));
    }

    [Fact]
    public void TheAvatarRaisesOnAvatar()
    {
        Arrange();
        var raised = 0;

        var bar = Render<MobileTopBar>(parameters => parameters
            .Add(p => p.DisplayName, "Ada Lovelace")
            .Add(p => p.UserId, Guid.NewGuid())
            .Add(p => p.OnAvatar, EventCallback.Factory.Create(this, () => raised++)));
        bar.Find(".pspad-avatar-button").Click();

        Assert.Equal(1, raised);
    }

    [Fact]
    public void TheSyncButtonSitsBeforeTheAvatarAndRunsOneSync()
    {
        var trigger = new GatedSyncTrigger();
        Arrange();
        Services.AddSingleton<ISyncTrigger>(trigger);
        Services.AddSingleton<ISyncStatus>(trigger);

        var bar = RenderBar();
        var order = bar.FindAll(".pspad-sync-button, .pspad-avatar-button").Select(e => e.ClassList.Contains("pspad-sync-button")).ToList();
        bar.Find(".pspad-sync-button").Click();

        Assert.Equal([true, false], order);
        Assert.Equal(1, trigger.Calls);
    }

    [Fact]
    public void TheSyncButtonCarriesTheStampAsItsLabel()
    {
        var trigger = new GatedSyncTrigger { LastSyncedAt = new DateTimeOffset(2026, 9, 11, 23, 58, 0, TimeSpan.Zero) };
        Arrange();
        Services.AddSingleton<ISyncTrigger>(trigger);
        Services.AddSingleton<ISyncStatus>(trigger);

        var bar = RenderBar();

        Assert.Contains("Updated 2 min ago", bar.Find(".pspad-sync-button").GetAttribute("aria-label"));
    }

    [Fact]
    public void AFailedSyncTurnsTheButtonRed()
    {
        var trigger = new GatedSyncTrigger { LastSyncFailed = true };
        Arrange();
        Services.AddSingleton<ISyncTrigger>(trigger);
        Services.AddSingleton<ISyncStatus>(trigger);

        var bar = RenderBar();

        Assert.Contains("mud-error-text", bar.Find(".pspad-sync-button").ClassList);
        Assert.Contains("Couldn't update", bar.Find(".pspad-sync-button").GetAttribute("aria-label"));
    }

    [Fact]
    public void TheAvatarIsThirtySixPixels()
    {
        Arrange();

        var bar = RenderBar();

        var style = bar.Find(".pspad-avatar-button .mud-avatar").GetAttribute("style");
        Assert.Contains("width:36px", style);
        Assert.Contains("height:36px", style);
    }

    [Fact]
    public void ThePageIconSitsInATileBeforeTheTitle()
    {
        Arrange();
        var header = Services.GetRequiredService<PageHeader>();
        var bar = RenderBar();

        header.Set("Dom", "2 lists · 2 open tasks", null, null, MudBlazor.Icons.Material.Outlined.Folder);

        bar.WaitForAssertion(() =>
        {
            var tile = bar.Find(".pspad-top-bar .pspad-page-icon");
            Assert.Equal("true", tile.GetAttribute("aria-hidden"));
            Assert.Contains(IconPaths.DistinctivePath(MudBlazor.Icons.Material.Outlined.Folder), tile.InnerHtml);
            Assert.Equal("pspad-top-title", tile.NextElementSibling!.ClassName);
        });
    }

    [Fact]
    public void NoIconMeansNoTile()
    {
        Arrange();
        Services.GetRequiredService<PageHeader>().Set("Inbox", null, null);

        var bar = RenderBar();

        Assert.Empty(bar.FindAll(".pspad-page-icon"));
    }

    [Fact]
    public void TheTitleIsNotInThePrimaryColour()
    {
        Arrange();
        Services.GetRequiredService<PageHeader>().Set("Inbox", null, null);

        var bar = RenderBar();

        var title = bar.Find(".pspad-top-heading");
        Assert.Equal("Inbox", title.TextContent);
        Assert.DoesNotContain("mud-primary-text", title.ClassList);
    }

    [Fact]
    public void TheSubtitleSitsUnderTheTitleOnOneLine()
    {
        Arrange();
        Services.GetRequiredService<PageHeader>().Set("Dom", "1 list · no open tasks", null);

        var bar = RenderBar();

        var subtitle = bar.Find(".pspad-top-title .pspad-top-subtitle");
        Assert.Equal("1 list · no open tasks", subtitle.TextContent);
        Assert.Contains("pspad-ellipsis", subtitle.ClassList);
        Assert.Contains("pspad-ellipsis", bar.Find(".pspad-top-heading").ClassList);
    }

    void Arrange() => AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));

    IRenderedComponent<MobileTopBar> RenderBar() =>
        Render<MobileTopBar>(parameters => parameters
            .Add(p => p.DisplayName, "Ada Lovelace")
            .Add(p => p.UserId, Guid.NewGuid()));
}
