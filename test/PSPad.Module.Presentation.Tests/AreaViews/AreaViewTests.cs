using PSPad.Abstractions;
using PSPad.Module.Presentation.AreaViews;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Presentation.Tests.AreaViews;

[UnitTest]
public class AreaViewTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid Area = Guid.Parse("22222222-2222-2222-2222-222222222222");
    static readonly DateTimeOffset Now = new(2026, 9, 30, 8, 0, 0, TimeSpan.Zero);
    static readonly Guid A = Guid.NewGuid();
    static readonly Guid B = Guid.NewGuid();
    static readonly Guid C = Guid.NewGuid();

    [Fact]
    public void IdForIsStablePerUserAndArea()
    {
        Assert.Equal(AreaView.IdFor(User, Area), AreaView.IdFor(User, Area));
        Assert.NotEqual(AreaView.IdFor(User, Area), AreaView.IdFor(User, Guid.NewGuid()));
        Assert.NotEqual(AreaView.IdFor(User, Area), AreaView.IdFor(Guid.NewGuid(), Area));
    }

    [Fact]
    public void TheFirstReorderCreatesTheView()
    {
        var reordered = Assert.IsType<ListsReordered>(Assert.Single(
            AreaView.Decide(null, Reorder([A, B, C], C, 0), Now)));

        Assert.Equal(AreaView.IdFor(User, Area), reordered.AggregateId);
        Assert.Equal([C, A, B], reordered.Order);

        var view = new AreaView();
        view.Apply(reordered);

        Assert.Equal(reordered.AggregateId, view.Id);
        Assert.Equal(User, view.UserId);
        Assert.Equal(Area, view.AreaId);
        Assert.Equal([C, A, B], view.Order);
    }

    [Fact]
    public void AMoveThatChangesNothingEmitsNothing()
    {
        Assert.Empty(AreaView.Decide(Existing([A, B]), Reorder([A, B], A, 0), Now));
    }

    [Fact]
    public void AListMissingFromTheOrderIsRejected()
    {
        Assert.Throws<DomainRejectedException>(() => AreaView.Decide(null, Reorder([A, B], C, 0), Now));
    }

    [Fact]
    public void DuplicatesInTheOrderAreRejected()
    {
        Assert.Throws<DomainRejectedException>(() => AreaView.Decide(null, Reorder([A, B, A], B, 0), Now));
    }

    [Fact]
    public void AnEmptyAreaIsRejected()
    {
        var command = new ReorderLists(Guid.NewGuid(), User, Guid.Empty, [A, B], B, 0);

        Assert.Throws<DomainRejectedException>(() => AreaView.Decide(null, command, Now));
    }

    [Fact]
    public void SomebodyElsesViewIsRejected()
    {
        var theirs = new AreaView();
        theirs.Apply(new ListsReordered(AreaView.IdFor(User, Area), Guid.NewGuid(), Now, Area, [A, B]));

        Assert.Throws<DomainRejectedException>(() => AreaView.Decide(theirs, Reorder([A, B], B, 0), Now));
    }

    [Fact]
    public void TheIndexIsClampedToTheEnds()
    {
        var reordered = Assert.IsType<ListsReordered>(Assert.Single(
            AreaView.Decide(null, Reorder([A, B, C], A, 99), Now)));

        Assert.Equal([B, C, A], reordered.Order);
    }

    static ReorderLists Reorder(IReadOnlyList<Guid> order, Guid listId, int toIndex) =>
        new(Guid.NewGuid(), User, Area, order, listId, toIndex);

    static AreaView Existing(IReadOnlyList<Guid> order)
    {
        var view = new AreaView();
        view.Apply(new ListsReordered(AreaView.IdFor(User, Area), User, Now, Area, order));
        return view;
    }
}
