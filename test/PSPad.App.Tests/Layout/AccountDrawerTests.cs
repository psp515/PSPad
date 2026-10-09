using Bunit;
using MudBlazor;
using PSPad.App.Components;
using PSPad.App.Layout;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class AccountDrawerTests : Bunit.TestContext
{
    [Fact]
    public void ItIsATemporaryPhoneOnlyDrawerOnTheRight()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));

        var drawer = RenderOpen();

        var mud = drawer.FindComponent<MudDrawer>().Instance;
        Assert.Equal(Anchor.End, mud.Anchor);
        Assert.Equal(DrawerVariant.Temporary, mud.Variant);
        Assert.Contains("d-md-none", drawer.Find(".mud-drawer").ClassList);
    }

    [Fact]
    public void ItCarriesTheAccountSettingsAppInfoAndFooter()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));

        var drawer = RenderOpen();

        Assert.Contains("Ada Lovelace", drawer.Markup);
        Assert.Contains("ada@example.com", drawer.Markup);
        Assert.NotEmpty(drawer.FindAll("a[href='/settings']"));
        Assert.NotEmpty(drawer.FindAll("a[href='/app-info']"));
        Assert.Single(drawer.FindComponents<SidebarFooter>());
    }

    [Fact]
    public void ItCarriesTheListSnapshotsRowBeforeSettings()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));

        var drawer = RenderOpen();

        Assert.NotEmpty(drawer.FindAll("a[href='/snapshots']"));
        var markup = drawer.Markup;
        Assert.True(markup.IndexOf("/snapshots") < markup.IndexOf("/settings"));
    }

    IRenderedComponent<AccountDrawer> RenderOpen() =>
        Render<AccountDrawer>(parameters => parameters
            .Add(p => p.Open, true)
            .Add(p => p.Email, "ada@example.com")
            .Add(p => p.DisplayName, "Ada Lovelace")
            .Add(p => p.UserId, Guid.NewGuid()));

    [Fact]
    public void StatisticsIsTheFirstRow()
    {
        AppTestHost.Arrange(this, Guid.NewGuid(), new DateOnly(2026, 9, 12));

        var drawer = RenderOpen();

        var first = drawer.FindAll(".mud-nav-link").First();
        Assert.Equal("/statistics", first.GetAttribute("href"));
    }
}
