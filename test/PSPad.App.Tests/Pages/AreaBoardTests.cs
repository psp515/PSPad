using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PSPad.Abstractions;
using PSPad.App.Pages;
using PSPad.App.State;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class AreaBoardTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();

    [Fact]
    public void ItNamesTheArea()
    {
        var area = NewArea("Dom");
        Arrange(area);

        var page = Render<AreaBoard>(parameters => parameters.Add(p => p.AreaId, area.Id));

        Assert.Contains("Dom", page.Markup);
    }

    [Fact]
    public void ItListsOnlyTheListsOfThatArea()
    {
        var mine = NewArea("Dom");
        var other = NewArea("Praca");
        Arrange(mine, other, NewList(mine.Id, "Zakupy", 0), NewList(other.Id, "Sprint", 0));

        var page = Render<AreaBoard>(parameters => parameters.Add(p => p.AreaId, mine.Id));

        Assert.Contains("Zakupy", page.Markup);
        Assert.DoesNotContain("Sprint", page.Markup);
    }

    [Fact]
    public void ItOrdersListsByPositionNotCreationOrder()
    {
        var area = NewArea("Dom");
        var remont = NewList(area.Id, "Remont", 2);
        var zakupy = NewList(area.Id, "Zakupy", 0);
        var ogrod = NewList(area.Id, "Ogród", 1);
        Arrange(area, remont, zakupy, ogrod);

        var page = Render<AreaBoard>(parameters => parameters.Add(p => p.AreaId, area.Id));

        var markup = page.Markup;
        Assert.True(markup.IndexOf("Zakupy", StringComparison.Ordinal)
            < markup.IndexOf("Ogród", StringComparison.Ordinal));
        Assert.True(markup.IndexOf("Ogród", StringComparison.Ordinal)
            < markup.IndexOf("Remont", StringComparison.Ordinal));
    }

    [Fact]
    public void ADeletedAreasScreenDoesNotRenderItsName()
    {
        var area = NewArea("Dom");
        area.ApplyAll(Area.Decide(
            area, new DeleteArea(Guid.NewGuid(), User, area.Id), DateTimeOffset.UnixEpoch));
        Arrange(area);

        var page = Render<AreaBoard>(parameters => parameters.Add(p => p.AreaId, area.Id));

        Assert.DoesNotContain("Dom", page.Markup);
    }

    [Fact]
    public void ItHidesDeletedLists()
    {
        var area = NewArea("Dom");
        var kept = NewList(area.Id, "Zakupy", 0);
        var removed = NewList(area.Id, "Remont", 1);
        removed.ApplyAll(TaskList.Decide(
            removed, new DeleteTaskList(Guid.NewGuid(), User, removed.Id), DateTimeOffset.UnixEpoch));
        Arrange(area, kept, removed);

        var page = Render<AreaBoard>(parameters => parameters.Add(p => p.AreaId, area.Id));

        Assert.Contains("Zakupy", page.Markup);
        Assert.DoesNotContain("Remont", page.Markup);
    }

    void Arrange(params Aggregate[] documents)
    {
        JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        Services.AddMudServices();

        var replica = new InMemoryReplica();
        foreach (var document in documents)
        {
            replica.SaveAsync(document).GetAwaiter().GetResult();
        }

        Services.AddSingleton<IReplica>(replica);
        Services.AddSingleton<IDocumentStore<Area>>(new ReplicaDocumentStore<Area>(replica));
        Services.AddSingleton<IDocumentStore<TaskList>>(new ReplicaDocumentStore<TaskList>(replica));
        Services.AddSingleton(new AppState { UserId = User, Today = new DateOnly(2026, 9, 12) });
    }

    static Area NewArea(string name)
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(
            null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), name, 0), DateTimeOffset.UnixEpoch));
        return area;
    }

    static TaskList NewList(Guid areaId, string name, int position)
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null,
            new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), areaId, name, position),
            DateTimeOffset.UnixEpoch));
        return list;
    }
}
