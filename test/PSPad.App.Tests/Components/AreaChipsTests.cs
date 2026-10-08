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
    public void TheCurrentAreaIsTintedAndMarkedAsTheCurrentPage()
    {
        AppTestHost.Arrange(this, User, new DateOnly(2026, 9, 12));
        var home = NewArea("Home", 0);
        var work = NewArea("Work", 1);

        var chips = Render<AreaChips>(parameters => parameters
            .Add(p => p.Areas, new[] { home, work })
            .Add(p => p.Current, work.Id));

        var current = chips.Find("[aria-current='page']");
        Assert.Equal($"/areas/{work.Id}", current.GetAttribute("href"));
        Assert.Contains("pspad-chip-current", current.ClassName);
        var other = chips.Find($"a[href='/areas/{home.Id}']");
        Assert.DoesNotContain("pspad-chip-current", other.ClassName);
        Assert.Null(other.GetAttribute("aria-current"));
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
    public void ASharedWithMeChipAppearsOnlyWhenSomethingIsShared()
    {
        AppTestHost.Arrange(this, User, new DateOnly(2026, 9, 12));
        var home = NewArea("Home", 0);

        var without = Render<AreaChips>(parameters => parameters
            .Add(p => p.Areas, new[] { home })
            .Add(p => p.Current, home.Id)
            .Add(p => p.HasShared, false));
        Assert.DoesNotContain("Shared with me", without.Markup);

        var with = Render<AreaChips>(parameters => parameters
            .Add(p => p.Areas, new[] { home })
            .Add(p => p.Current, home.Id)
            .Add(p => p.HasShared, true));
        var chip = with.Find($"a[href='{PSPad.App.State.SharedWithMe.Href}']");
        Assert.Contains("Shared with me", chip.TextContent);
    }

    [Fact]
    public void TheSharedWithMeChipIsMarkedCurrentOnItsOwnRoute()
    {
        AppTestHost.Arrange(this, User, new DateOnly(2026, 9, 12));
        var home = NewArea("Home", 0);

        var chips = Render<AreaChips>(parameters => parameters
            .Add(p => p.Areas, new[] { home })
            .Add(p => p.Current, PSPad.App.State.SharedWithMe.AreaId)
            .Add(p => p.HasShared, true));

        var current = chips.Find("[aria-current='page']");
        Assert.Equal(PSPad.App.State.SharedWithMe.Href, current.GetAttribute("href"));
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
