using PSPad.App.State;
using PSPad.Module.Presentation.AreaViews;
using PSPad.Module.Presentation.ListViews;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class ListPlacementTests
{
    static readonly Guid Me = Guid.NewGuid();
    static readonly Guid Owner = Guid.NewGuid();
    static readonly DateTimeOffset Start = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MyListsSitInTheirArea()
    {
        var home = NewArea("Home", 0);
        var list = NewList(Me, home.Id, "Zakupy", 0);

        var placement = ListPlacement.For(Me, [home], [list], [], []);

        Assert.Equal([list], placement.InArea(home.Id));
        Assert.Equal(home.Id, placement.AreaOf(list));
    }

    [Fact]
    public void AnUnfiledMemberListSitsInSharedWithMe()
    {
        var home = NewArea("Home", 0);
        var list = NewMemberList(Owner, Me, "Errands", 0);

        var placement = ListPlacement.For(Me, [home], [list], [], []);

        Assert.Equal([list], placement.InArea(SharedWithMe.AreaId));
        Assert.Equal(SharedWithMe.AreaId, placement.AreaOf(list));
    }

    [Fact]
    public void AFiledMemberListSitsInThatArea()
    {
        var home = NewArea("Home", 0);
        var list = NewMemberList(Owner, Me, "Errands", 0);
        var view = PlaceView(Me, list.Id, home.Id);

        var placement = ListPlacement.For(Me, [home], [list], [], [view]);

        Assert.Equal([list], placement.InArea(home.Id));
        Assert.Equal(home.Id, placement.AreaOf(list));
        Assert.Empty(placement.InArea(SharedWithMe.AreaId));
    }

    [Fact]
    public void AFilingToADeletedAreaFallsBackToSharedWithMe()
    {
        var deleted = NewArea("Gone", 0);
        deleted.Apply(new AreaDeleted(deleted.Id, Me, Start));
        var list = NewMemberList(Owner, Me, "Errands", 0);
        var view = PlaceView(Me, list.Id, deleted.Id);

        var placement = ListPlacement.For(Me, [deleted], [list], [], [view]);

        Assert.Equal([list], placement.InArea(SharedWithMe.AreaId));
        Assert.Equal(SharedWithMe.AreaId, placement.AreaOf(list));
    }

    [Fact]
    public void AViewOnMyOwnListIsIgnored()
    {
        var home = NewArea("Home", 0);
        var work = NewArea("Work", 1);
        var list = NewList(Me, home.Id, "Zakupy", 0);
        var view = PlaceView(Me, list.Id, work.Id);

        var placement = ListPlacement.For(Me, [home, work], [list], [], [view]);

        Assert.Equal(home.Id, placement.AreaOf(list));
        Assert.Equal([list], placement.InArea(home.Id));
        Assert.Empty(placement.InArea(work.Id));
    }

    [Fact]
    public void AreaOrderStillComesFromTheAreaView()
    {
        var home = NewArea("Home", 0);
        var mine = NewList(Me, home.Id, "Zakupy", 0);
        var filed = NewMemberList(Owner, Me, "Errands", 1);
        var view = PlaceView(Me, filed.Id, home.Id);
        var order = OrderView(Me, home.Id, filed.Id, mine.Id);

        var placement = ListPlacement.For(Me, [home], [mine, filed], [order], [view]);

        Assert.Equal([filed, mine], placement.InArea(home.Id));
    }

    [Fact]
    public void AllListsAreasInPositionOrderThenShared()
    {
        var home = NewArea("Home", 0);
        var work = NewArea("Work", 1);
        var shopping = NewList(Me, home.Id, "Zakupy", 0);
        var sprint = NewList(Me, work.Id, "Sprint", 1);
        var shared = NewMemberList(Owner, Me, "Errands", 2);

        var placement = ListPlacement.For(Me, [home, work], [shopping, sprint, shared], [], []);

        Assert.Equal([shopping, sprint, shared], placement.All);
    }

    [Fact]
    public void HasSharedOnlyWithUnfiledMemberLists()
    {
        var home = NewArea("Home", 0);
        var mine = NewList(Me, home.Id, "Zakupy", 0);

        var withoutShared = ListPlacement.For(Me, [home], [mine], [], []);
        Assert.False(withoutShared.HasShared);

        var shared = NewMemberList(Owner, Me, "Errands", 1);
        var withShared = ListPlacement.For(Me, [home], [mine, shared], [], []);
        Assert.True(withShared.HasShared);
    }

    [Fact]
    public void TheVirtualAreaIsNamedSharedWithMe() => Assert.Equal("Shared with me", Empty().AreaName(SharedWithMe.AreaId));

    static ListPlacement Empty() => ListPlacement.For(Me, [], [], [], []);

    static Area NewArea(string name, int position)
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(null, new CreateArea(Guid.NewGuid(), Me, Guid.NewGuid(), name, position), Start));
        return area;
    }

    static TaskList NewList(Guid owner, Guid areaId, string name, int createdDay)
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null, new CreateTaskList(Guid.NewGuid(), owner, Guid.NewGuid(), areaId, name), Start.AddDays(createdDay)));
        return list;
    }

    static TaskList NewMemberList(Guid owner, Guid member, string name, int createdDay)
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null, new CreateTaskList(Guid.NewGuid(), owner, Guid.NewGuid(), Guid.NewGuid(), name),
            Start.AddDays(createdDay)));
        list.Apply(new TaskListShared(list.Id, owner, Start.AddDays(createdDay), "k3Jv9s2mQ0x7b1nR4tYw8eZa", "Owner"));
        list.Apply(new TaskListJoined(list.Id, owner, Start.AddDays(createdDay), member, "Member"));
        return list;
    }

    static ListView PlaceView(Guid me, Guid listId, Guid? areaId)
    {
        var view = new ListView();
        view.ApplyAll(ListView.Decide(null, new PlaceList(Guid.NewGuid(), me, listId, areaId), Start));
        return view;
    }

    static AreaView OrderView(Guid me, Guid areaId, params Guid[] order)
    {
        var view = new AreaView();
        view.Apply(new ListsReordered(AreaView.IdFor(me, areaId), me, Start, areaId, order));
        return view;
    }
}
