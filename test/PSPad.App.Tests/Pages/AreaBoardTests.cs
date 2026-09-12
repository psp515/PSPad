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
        Arrange(mine, other, NewList(mine.Id, "Zakupy"), NewList(other.Id, "Sprint"));

        var page = Render<AreaBoard>(parameters => parameters.Add(p => p.AreaId, mine.Id));

        Assert.Contains("Zakupy", page.Markup);
        Assert.DoesNotContain("Sprint", page.Markup);
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

    static TaskList NewList(Guid areaId, string name)
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null,
            new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), areaId, name, 0),
            DateTimeOffset.UnixEpoch));
        return list;
    }
}
