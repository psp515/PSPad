using Bunit;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Microsoft.AspNetCore.Components.Web;
using PSPad.App.Layout;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Layout;

[UnitTest]
public class ListDetailPanelTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void WithNoAreaItShowsNothing()
    {
        AppTestHost.Arrange(this, User, Today);

        var panel = Render<ListDetailPanel>();

        Assert.Empty(panel.FindAll(".pspad-list-name-field"));
    }

    [Fact]
    public void ANewListOffersTheNameAndAnAddButtonNamingItsArea()
    {
        var area = NewArea("Dom");
        AppTestHost.Arrange(this, User, Today, area);

        var panel = Render<ListDetailPanel>(parameters => parameters.Add(p => p.NewInArea, area.Id));

        panel.WaitForAssertion(() => Assert.Contains("New list · Dom", panel.Find(".pspad-panel-title").TextContent));
        panel.Find(".pspad-list-name-field");
        Assert.Contains("Add list", panel.Find(".pspad-panel-save").TextContent);
        Assert.True(panel.Find(".pspad-panel-save").HasAttribute("disabled"));
        Assert.Empty(panel.FindAll(".pspad-panel-delete"));
    }

    [Fact]
    public async Task AddingANewListCreatesItAfterTheOthersInThatAreaAndCloses()
    {
        var area = NewArea("Dom");
        var existing = NewList(area.Id, "Zakupy");
        var replica = AppTestHost.Arrange(this, User, Today, area, existing);
        var closed = false;

        var panel = Render<ListDetailPanel>(parameters => parameters
            .Add(p => p.NewInArea, area.Id)
            .Add(p => p.OnClose, () => closed = true));
        panel.Find(".pspad-list-name-field input").Input("Ogród");
        panel.Find(".pspad-panel-save").Click();

        var lists = await replica.LoadAllAsync<TaskList>(User);
        var created = Assert.Single(lists, list => list.Name == "Ogród");
        Assert.Equal(area.Id, created.AreaId);
        Assert.True(created.CreatedAt > existing.CreatedAt);
        Assert.True(closed);
    }

    [Fact]
    public async Task EnterAddsTheNewList()
    {
        var area = NewArea("Dom");
        var replica = AppTestHost.Arrange(this, User, Today, area);

        var panel = Render<ListDetailPanel>(parameters => parameters.Add(p => p.NewInArea, area.Id));
        panel.Find(".pspad-list-name-field input").Input("Ogród");
        panel.Find(".pspad-list-name-field input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Contains(await replica.LoadAllAsync<TaskList>(User), list => list.Name == "Ogród");
    }

    [Fact]
    public void TheNameComesFirstAndTheKindBelowIt()
    {
        var area = NewArea("Dom");
        AppTestHost.Arrange(this, User, Today, area);

        var panel = Render<ListDetailPanel>(parameters => parameters.Add(p => p.NewInArea, area.Id));

        var name = panel.Find(".pspad-list-name-field input");
        Assert.Equal("List name", name.GetAttribute("placeholder"));
        Assert.Empty(panel.FindAll(".pspad-list-name-field .mud-input-outlined"));
        var markup = panel.Markup;
        Assert.True(markup.IndexOf("pspad-list-name-field", StringComparison.Ordinal)
            < markup.IndexOf("pspad-list-kind", StringComparison.Ordinal));
    }

    [Fact]
    public void AnExistingListShowsItsNameKindAndArea()
    {
        var area = NewArea("Dom");
        var list = NewList(area.Id, "Przepisy", ListKind.Reference);
        AppTestHost.Arrange(this, User, Today, area, list);

        var panel = RenderWithOverlays(list.Id);

        Assert.Equal("Przepisy", panel.Find(".pspad-list-name-field input").GetAttribute("value"));
        Assert.Contains("Reference list", panel.Find(".pspad-list-kind-readonly").TextContent);
        Assert.Equal("Dom", panel.Find(".pspad-list-area input").GetAttribute("value"));
        Assert.Empty(panel.FindAll(".pspad-list-kind .mud-toggle-item"));
        Assert.Empty(panel.FindAll(".pspad-panel-save"));
    }

    [Fact]
    public async Task RenamingAnExistingListSavesAsItChanges()
    {
        var area = NewArea("Dom");
        var list = NewList(area.Id, "Zakupy", ListKind.Tasks);
        var replica = AppTestHost.Arrange(this, User, Today, area, list);

        var panel = RenderWithOverlays(list.Id);
        panel.Find(".pspad-list-name-field input").Change("Zakupy tygodniowe");

        Assert.Equal("Zakupy tygodniowe", (await replica.LoadAsync<TaskList>(list.Id))!.Name);
    }

    [Fact]
    public async Task PickingAnotherAreaMovesTheList()
    {
        var home = NewArea("Dom");
        var work = NewArea("Praca");
        var list = NewList(home.Id, "Zakupy", ListKind.Tasks);
        var replica = AppTestHost.Arrange(this, User, Today, home, work, list);

        var panel = RenderWithOverlays(list.Id);
        panel.Find(".pspad-list-area .mud-select-input").MouseDown();
        panel.WaitForAssertion(() => Assert.Contains(panel.FindAll(".mud-list-item"),
            option => option.TextContent.Trim() == "Praca"));
        panel.FindAll(".mud-list-item").First(option => option.TextContent.Trim() == "Praca").Click();

        Assert.Equal(work.Id, (await replica.LoadAsync<TaskList>(list.Id))!.AreaId);
    }

    [Fact]
    public async Task DeletingFromThePanelAsksFirstThenClosesOnAnotherScreen()
    {
        var area = NewArea("Dom");
        var list = NewList(area.Id, "Zakupy", ListKind.Tasks);
        var replica = AppTestHost.Arrange(this, User, Today, area, list);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/areas/{area.Id}?list={list.Id}");

        var panel = RenderWithOverlays(list.Id);
        Assert.Contains("Delete list", panel.Find(".pspad-panel-delete").TextContent);
        panel.Find(".pspad-panel-delete").Click();
        panel.FindAll("div.mud-dialog button").Last().Click();

        Assert.True((await replica.LoadAsync<TaskList>(list.Id))!.Deleted);
        Assert.EndsWith($"/areas/{area.Id}", navigation.Uri);
    }

    [Fact]
    public void DeletingFromTheListsOwnScreenGoesToItsArea()
    {
        var area = NewArea("Dom");
        var list = NewList(area.Id, "Zakupy", ListKind.Tasks);
        AppTestHost.Arrange(this, User, Today, area, list);
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo($"/lists/{list.Id}?list={list.Id}");

        var panel = RenderWithOverlays(list.Id);
        panel.Find(".pspad-panel-delete").Click();
        panel.FindAll("div.mud-dialog button").Last().Click();

        Assert.EndsWith($"/areas/{area.Id}", navigation.Uri);
    }

    IRenderedComponent<ContainerFragment> RenderWithOverlays(Guid listId) => Render(builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<MudDialogProvider>(1);
        builder.CloseComponent();
        builder.OpenComponent<ListDetailPanel>(2);
        builder.AddAttribute(3, nameof(ListDetailPanel.ListId), (Guid?)listId);
        builder.CloseComponent();
    });

    static TaskList NewList(Guid areaId, string name, ListKind kind)
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null, new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), areaId, name, kind), DateTimeOffset.UnixEpoch));
        return list;
    }

    [Fact]
    public void TheKindTogglesToTasksByDefault()
    {
        var area = NewArea("Dom");
        AppTestHost.Arrange(this, User, Today, area);

        var panel = Render<ListDetailPanel>(parameters => parameters.Add(p => p.NewInArea, area.Id));

        var selected = panel.Find(".pspad-list-kind .mud-toggle-item-selected");
        Assert.Contains("Tasks", selected.TextContent);
    }

    [Fact]
    public async Task ChoosingReferenceAndSavingCreatesAReferenceList()
    {
        var area = NewArea("Dom");
        var replica = AppTestHost.Arrange(this, User, Today, area);

        var panel = Render<ListDetailPanel>(parameters => parameters.Add(p => p.NewInArea, area.Id));
        panel.Find(".pspad-list-name-field input").Input("Przepisy");
        panel.FindAll(".pspad-list-kind .mud-toggle-item")[1].Click();
        panel.Find(".pspad-panel-save").Click();

        var created = Assert.Single(await replica.LoadAllAsync<TaskList>(User));
        Assert.Equal(ListKind.Reference, created.Kind);
    }

    [Fact]
    public async Task LeavingTheKindUntouchedCreatesATasksList()
    {
        var area = NewArea("Dom");
        var replica = AppTestHost.Arrange(this, User, Today, area);

        var panel = Render<ListDetailPanel>(parameters => parameters.Add(p => p.NewInArea, area.Id));
        panel.Find(".pspad-list-name-field input").Input("Zakupy");
        panel.Find(".pspad-panel-save").Click();

        var created = Assert.Single(await replica.LoadAllAsync<TaskList>(User));
        Assert.Equal(ListKind.Tasks, created.Kind);
    }

    [Fact]
    public async Task ABlankNameAddsNothing()
    {
        var area = NewArea("Dom");
        var replica = AppTestHost.Arrange(this, User, Today, area);

        var panel = Render<ListDetailPanel>(parameters => parameters.Add(p => p.NewInArea, area.Id));
        panel.Find(".pspad-list-name-field input").Input("   ");
        panel.Find(".pspad-list-name-field input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Empty(await replica.LoadAllAsync<TaskList>(User));
    }

    static Area NewArea(string name)
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), name, 0),
            DateTimeOffset.UnixEpoch));
        return area;
    }

    static TaskList NewList(Guid areaId, string name)
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(null,
            new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), areaId, name),
            DateTimeOffset.UnixEpoch));
        return list;
    }
}
