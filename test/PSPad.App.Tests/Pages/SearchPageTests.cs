using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PSPad.App.Pages;
using PSPad.App.State.Search;
using PSPad.App.Tests;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class SearchPageTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public void AReferenceListHitShowsTheLibraryBooksIcon()
    {
        var area = NewArea("Dom");
        var list = NewList(area.Id, "Przepisy", ListKind.Reference);
        Arrange(area, list);

        var page = RenderAt("Przepisy");

        Assert.Contains(IconPaths.DistinctivePath(MudBlazor.Icons.Material.Outlined.LibraryBooks), page.Markup);
    }

    [Fact]
    public void ATasksListHitShowsTheChecklistIcon()
    {
        var area = NewArea("Dom");
        var list = NewList(area.Id, "Zakupy", ListKind.Tasks);
        Arrange(area, list);

        var page = RenderAt("Zakupy");

        Assert.Contains(IconPaths.DistinctivePath(MudBlazor.Icons.Material.Outlined.Checklist), page.Markup);
    }

    IRenderedComponent<SearchPage> RenderAt(string q)
    {
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo(navigation.GetUriWithQueryParameter("q", q));
        return Render<SearchPage>();
    }

    void Arrange(params PSPad.Abstractions.Aggregate[] documents)
    {
        AppTestHost.Arrange(this, User, Today, documents);
        Services.AddScoped<ReplicaSearch>();
    }

    static Area NewArea(string name)
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(
            null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), name, 0), DateTimeOffset.UnixEpoch));
        return area;
    }

    static TaskList NewList(Guid areaId, string name, ListKind kind)
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null, new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), areaId, name, kind),
            DateTimeOffset.UnixEpoch));
        return list;
    }
}
