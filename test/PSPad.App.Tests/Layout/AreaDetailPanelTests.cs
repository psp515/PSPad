using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using PSPad.App.Layout;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class AreaDetailPanelTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void WithNoAreaItShowsNothing()
    {
        AppTestHost.Arrange(this, User, Today);

        var panel = Render<AreaDetailPanel>();

        Assert.Empty(panel.FindAll(".pspad-area-name-field"));
    }

    [Fact]
    public void ANewAreaOffersOnlyTheNameAndAnAddButton()
    {
        AppTestHost.Arrange(this, User, Today);

        var panel = Render<AreaDetailPanel>(parameters => parameters.Add(p => p.IsNew, true));

        Assert.Contains("New area", panel.Find(".pspad-panel-title").TextContent);
        panel.Find(".pspad-area-name-field");
        Assert.Contains("Add area", panel.Find(".pspad-panel-save").TextContent);
        Assert.True(panel.Find(".pspad-panel-save").HasAttribute("disabled"));
        Assert.Empty(panel.FindAll(".pspad-panel-delete"));
    }

    [Fact]
    public async Task AddingANewAreaCreatesItAfterTheOthersAndOpensIt()
    {
        var existing = NewArea("Dom", 3);
        var replica = AppTestHost.Arrange(this, User, Today, existing);
        var navigation = Services.GetRequiredService<NavigationManager>();

        var panel = Render<AreaDetailPanel>(parameters => parameters.Add(p => p.IsNew, true));
        panel.Find(".pspad-area-name-field input").Input("Praca");
        panel.Find(".pspad-panel-save").Click();

        var areas = await replica.LoadAllAsync<Area>(User);
        var created = Assert.Single(areas, area => area.Name == "Praca");
        Assert.True(created.Position > existing.Position);
        Assert.EndsWith($"/areas/{created.Id}", navigation.Uri);
    }

    [Fact]
    public async Task EnterAddsTheNewArea()
    {
        var replica = AppTestHost.Arrange(this, User, Today);

        var panel = Render<AreaDetailPanel>(parameters => parameters.Add(p => p.IsNew, true));
        panel.Find(".pspad-area-name-field input").Input("Praca");
        panel.Find(".pspad-area-name-field input").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        var areas = await replica.LoadAllAsync<Area>(User);
        Assert.Contains(areas, area => area.Name == "Praca");
    }

    [Fact]
    public async Task ABlankNameAddsNothing()
    {
        var replica = AppTestHost.Arrange(this, User, Today);

        var panel = Render<AreaDetailPanel>(parameters => parameters.Add(p => p.IsNew, true));
        panel.Find(".pspad-area-name-field input").Input("   ");
        panel.Find(".pspad-area-name-field input").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        Assert.Empty(await replica.LoadAllAsync<Area>(User));
    }

    [Fact]
    public async Task AnExistingAreaRenamesLiveWithoutASaveButton()
    {
        var area = NewArea("Dom", 0);
        var replica = AppTestHost.Arrange(this, User, Today, area);

        var panel = Render<AreaDetailPanel>(parameters => parameters.Add(p => p.AreaId, (Guid?)area.Id));
        Assert.Empty(panel.FindAll(".pspad-panel-save"));
        panel.Find(".pspad-area-name-field input").Change("Domownicy");

        var stored = await replica.LoadAsync<Area>(area.Id);
        Assert.Equal("Domownicy", stored!.Name);
    }

    [Fact]
    public async Task DeletingAnAreaAsksFirstThenGoesHome()
    {
        var area = NewArea("Dom", 0);
        var replica = AppTestHost.Arrange(this, User, Today, area);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/areas/{area.Id}?area={area.Id}");

        var panel = RenderWithOverlays(area.Id);
        Assert.Contains("Delete area", panel.Find(".pspad-panel-delete").TextContent);
        panel.Find(".pspad-panel-delete").Click();
        panel.FindAll("div.mud-dialog button").Last().Click();

        var stored = await replica.LoadAsync<Area>(area.Id);
        Assert.True(stored!.Deleted);
        Assert.Equal(navigation.BaseUri, navigation.Uri);
    }

    [Fact]
    public async Task CancellingTheDeleteKeepsTheArea()
    {
        var area = NewArea("Dom", 0);
        var replica = AppTestHost.Arrange(this, User, Today, area);

        var panel = RenderWithOverlays(area.Id);
        panel.Find(".pspad-panel-delete").Click();
        panel.FindAll("div.mud-dialog button").First().Click();

        var stored = await replica.LoadAsync<Area>(area.Id);
        Assert.False(stored!.Deleted);
    }

    [Fact]
    public void ClosingInvokesOnClose()
    {
        AppTestHost.Arrange(this, User, Today);
        var closed = false;

        var panel = Render<AreaDetailPanel>(parameters => parameters
            .Add(p => p.IsNew, true)
            .Add(p => p.OnClose, () => closed = true));
        panel.Find(".pspad-panel-close").Click();

        Assert.True(closed);
    }

    IRenderedComponent<ContainerFragment> RenderWithOverlays(Guid areaId) => Render(builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<MudDialogProvider>(1);
        builder.CloseComponent();
        builder.OpenComponent<AreaDetailPanel>(2);
        builder.AddAttribute(3, nameof(AreaDetailPanel.AreaId), (Guid?)areaId);
        builder.CloseComponent();
    });

    static Area NewArea(string name, int position)
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(
            null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), name, position), DateTimeOffset.UnixEpoch));
        return area;
    }
}
