using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using PSPad.App.Components;
using PSPad.App.Pages;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class AreasIndexTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();

    [Fact]
    public void ItOpensTheRememberedArea()
    {
        var home = NewArea("Home", 0);
        var work = NewArea("Work", 1);
        AppTestHost.Arrange(this, User, new DateOnly(2026, 9, 12), home, work);
        JSInterop.Setup<string?>("localStorage.getItem", "pspad.last-area").SetResult(work.Id.ToString());
        var navigation = Services.GetRequiredService<BunitNavigationManager>();

        var page = Render<AreasIndex>();

        page.WaitForAssertion(() => Assert.EndsWith($"/areas/{work.Id}", navigation.Uri));
    }

    [Fact]
    public void ItFallsBackToTheFirstAreaWhenTheRememberedOneIsGone()
    {
        var home = NewArea("Home", 0);
        var work = NewArea("Work", 1);
        work.ApplyAll(Area.Decide(work, new DeleteArea(Guid.NewGuid(), User, work.Id), DateTimeOffset.UnixEpoch));
        AppTestHost.Arrange(this, User, new DateOnly(2026, 9, 12), work, home);
        JSInterop.Setup<string?>("localStorage.getItem", "pspad.last-area").SetResult(work.Id.ToString());
        var navigation = Services.GetRequiredService<BunitNavigationManager>();

        var page = Render<AreasIndex>();

        page.WaitForAssertion(() => Assert.EndsWith($"/areas/{home.Id}", navigation.Uri));
    }

    [Fact]
    public void WithNoAreasItOffersToCreateOne()
    {
        AppTestHost.Arrange(this, User, new DateOnly(2026, 9, 12));
        var navigation = Services.GetRequiredService<BunitNavigationManager>();

        var page = Render<AreasIndex>();

        page.WaitForAssertion(() => Assert.Single(page.FindComponents<EmptyState>()));
        page.Find(".pspad-empty-state").Click();
        Assert.EndsWith("?area=new", navigation.Uri);
    }

    static Area NewArea(string name, int position)
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(
            null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), name, position), DateTimeOffset.UnixEpoch));
        return area;
    }
}
