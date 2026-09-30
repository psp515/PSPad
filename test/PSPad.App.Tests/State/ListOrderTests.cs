using PSPad.App.State;
using PSPad.Module.Presentation.AreaViews;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class ListOrderTests
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateTimeOffset Start = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void WithoutAViewListsFollowCreationDate()
    {
        var area = Guid.NewGuid();
        var newer = NewList(area, "Remont", 2);
        var older = NewList(area, "Zakupy", 0);

        Assert.Equal([older, newer], ListOrder.InArea(area, [newer, older], null));
    }

    [Fact]
    public void TheViewDecidesTheOrder()
    {
        var area = Guid.NewGuid();
        var first = NewList(area, "Zakupy", 0);
        var second = NewList(area, "Remont", 1);

        Assert.Equal([second, first], ListOrder.InArea(area, [first, second], View(area, second.Id, first.Id)));
    }

    [Fact]
    public void ListsOfAnotherAreaAndDeletedListsAreLeftOut()
    {
        var area = Guid.NewGuid();
        var kept = NewList(area, "Zakupy", 0);
        var elsewhere = NewList(Guid.NewGuid(), "Sprint", 1);
        var deleted = NewList(area, "Stare", 2);
        deleted.Apply(new TaskListDeleted(deleted.Id, User, Start));

        Assert.Equal([kept], ListOrder.InArea(area, [kept, elsewhere, deleted], null));
    }

    [Fact]
    public void ArrangeGroupsListsByAreaInTheAreasOrder()
    {
        var home = NewArea("Dom");
        var work = NewArea("Praca");
        var sprint = NewList(work.Id, "Sprint", 0);
        var shopping = NewList(home.Id, "Zakupy", 1);
        var repairs = NewList(home.Id, "Remont", 2);
        var orphan = NewList(Guid.NewGuid(), "Sierota", 3);

        var arranged = ListOrder.Arrange(
            [home, work], [sprint, shopping, repairs, orphan], [View(home.Id, repairs.Id, shopping.Id)]);

        Assert.Equal([repairs, shopping, sprint], arranged);
    }

    static AreaView View(Guid areaId, params Guid[] order)
    {
        var view = new AreaView();
        view.Apply(new ListsReordered(AreaView.IdFor(User, areaId), User, Start, areaId, order));
        return view;
    }

    static Area NewArea(string name)
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), name, 0), Start));
        return area;
    }

    static TaskList NewList(Guid areaId, string name, int createdDay)
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null, new CreateTaskList(Guid.NewGuid(), User, Guid.NewGuid(), areaId, name), Start.AddDays(createdDay)));
        return list;
    }
}
