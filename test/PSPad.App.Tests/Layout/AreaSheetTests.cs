using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PSPad.App.Layout;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class AreaSheetTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();

    [Fact]
    public void AnOpenSheetListsEveryArea()
    {
        var sheet = RenderSheet(NewArea("Personal", 0), NewArea("Work", 1));

        Assert.Contains("Personal", sheet.Markup);
        Assert.Contains("Work", sheet.Markup);
    }

    [Fact]
    public void ItOffersAWayToAddAnArea()
    {
        var sheet = RenderSheet(NewArea("Personal", 0));

        Assert.Contains("New area", sheet.Markup);
    }

    [Fact]
    public async Task ChoosingAnAreaReportsIt()
    {
        Area? chosen = null;
        var personal = NewArea("Personal", 0);

        var sheet = RenderSheet(
            parameters => parameters.Add(p => p.OnAreaSelected, area => chosen = area), personal);
        await sheet.FindAll(".mud-list-item")[0].ClickAsync(new());

        Assert.Equal(personal.Id, chosen?.Id);
    }

    IRenderedComponent<AreaSheet> RenderSheet(params Area[] areas) =>
        RenderSheet(_ => { }, areas);

    IRenderedComponent<AreaSheet> RenderSheet(
        Action<ComponentParameterCollectionBuilder<AreaSheet>> extra, params Area[] areas)
    {
        JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        Services.AddMudServices();

        return Render<AreaSheet>(parameters =>
        {
            parameters.Add(p => p.Open, true);
            parameters.Add(p => p.Areas, areas);
            extra(parameters);
        });
    }

    static Area NewArea(string name, int position)
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(
            null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), name, position),
            DateTimeOffset.UnixEpoch));
        return area;
    }
}
