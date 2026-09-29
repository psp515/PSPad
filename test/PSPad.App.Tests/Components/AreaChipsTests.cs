using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using PSPad.App.Components;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class AreaChipsTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();

    [Fact]
    public void EveryAreaIsAChipLinkInPositionOrder()
    {
        AppTestHost.Arrange(this, User, new DateOnly(2026, 9, 12));
        var work = NewArea("Work", 1);
        var home = NewArea("Home", 0);

        var chips = Render(work, home);

        var links = chips.FindAll("a.mud-chip");
        Assert.Equal($"/areas/{home.Id}", links[0].GetAttribute("href"));
        Assert.Equal($"/areas/{work.Id}", links[1].GetAttribute("href"));
    }

    [Fact]
    public void TheCurrentAreaIsFilledAndMarkedAsTheCurrentPage()
    {
        AppTestHost.Arrange(this, User, new DateOnly(2026, 9, 12));
        var home = NewArea("Home", 0);
        var work = NewArea("Work", 1);

        var chips = Render<AreaChips>(parameters => parameters
            .Add(p => p.Areas, new[] { home, work })
            .Add(p => p.Current, work.Id));

        var current = chips.Find("[aria-current='page']");
        Assert.Equal($"/areas/{work.Id}", current.GetAttribute("href"));
        Assert.Contains("mud-chip-filled", current.ClassName);
        Assert.DoesNotContain("mud-chip-filled", chips.Find($"a[href='/areas/{home.Id}']").ClassName);
    }

    [Fact]
    public void DeletedAreasHaveNoChip()
    {
        AppTestHost.Arrange(this, User, new DateOnly(2026, 9, 12));
        var home = NewArea("Home", 0);
        var gone = NewArea("Gone", 1);
        gone.ApplyAll(Area.Decide(gone, new DeleteArea(Guid.NewGuid(), User, gone.Id), DateTimeOffset.UnixEpoch));

        var chips = Render(home, gone);

        Assert.DoesNotContain("Gone", chips.Markup);
    }

    [Fact]
    public void NewAreaOpensTheNewAreaPanel()
    {
        AppTestHost.Arrange(this, User, new DateOnly(2026, 9, 12));
        var navigation = Services.GetRequiredService<BunitNavigationManager>();

        var chips = Render(NewArea("Home", 0));
        chips.Find(".pspad-new-area-chip").Click();

        Assert.EndsWith("?area=new", navigation.Uri);
    }

    IRenderedComponent<AreaChips> Render(params Area[] areas) =>
        Render<AreaChips>(parameters => parameters
            .Add(p => p.Areas, areas)
            .Add(p => p.Current, areas[0].Id));

    static Area NewArea(string name, int position)
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(
            null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), name, position), DateTimeOffset.UnixEpoch));
        return area;
    }
}
