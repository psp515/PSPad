using Bunit;
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
