using PSPad.Abstractions;
using PSPad.Module.Presentation.ListViews;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Presentation.Tests.ListViews;

[UnitTest]
public class ListViewTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid ListId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    static readonly Guid AreaId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    static readonly DateTimeOffset Now = new(2026, 9, 30, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IdForIsStablePerUserAndList()
    {
        Assert.Equal(ListView.IdFor(User, ListId), ListView.IdFor(User, ListId));
        Assert.NotEqual(ListView.IdFor(User, ListId), ListView.IdFor(User, Guid.NewGuid()));
        Assert.NotEqual(ListView.IdFor(User, ListId), ListView.IdFor(Guid.NewGuid(), ListId));
    }

    [Fact]
    public void TheFirstPlacementCreatesTheView()
    {
        var placed = Assert.IsType<ListPlaced>(Assert.Single(
            ListView.Decide(null, new PlaceList(Guid.NewGuid(), User, ListId, AreaId), Now)));

        Assert.Equal(ListView.IdFor(User, ListId), placed.AggregateId);

        var view = new ListView();
        view.ApplyAll([placed]);

        Assert.Equal(AreaId, view.AreaId);
        Assert.Equal(ListId, view.ListId);
    }

    [Fact]
    public void ClearingSendsTheListBackToSharedWithMe()
    {
        var existing = Existing(AreaId);

        var placed = Assert.IsType<ListPlaced>(Assert.Single(
            ListView.Decide(existing, new PlaceList(Guid.NewGuid(), User, ListId, null), Now)));

        Assert.Null(placed.AreaId);
    }

    [Fact]
    public void AnUnchangedPlacementEmitsNothing()
    {
        var existing = Existing(AreaId);

        Assert.Empty(ListView.Decide(existing, new PlaceList(Guid.NewGuid(), User, ListId, AreaId), Now));
    }

    [Fact]
    public void SomebodyElsesViewIsRejected()
    {
        var theirs = Existing(AreaId, owner: Guid.NewGuid());

        Assert.Throws<DomainRejectedException>(
            () => ListView.Decide(theirs, new PlaceList(Guid.NewGuid(), User, ListId, AreaId), Now));
    }

    [Fact]
    public void AnEmptyListIdIsRejected()
    {
        var command = new PlaceList(Guid.NewGuid(), User, Guid.Empty, AreaId);

        Assert.Throws<DomainRejectedException>(() => ListView.Decide(null, command, Now));
    }

    static ListView Existing(Guid? areaId, Guid? owner = null)
    {
        var user = owner ?? User;
        var view = new ListView();
        view.Apply(new ListPlaced(ListView.IdFor(user, ListId), user, Now, ListId, areaId));
        return view;
    }
}
