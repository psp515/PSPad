using PSPad.Module.Presentation.AreaViews;
using PSPad.Module.Presentation.Tests.Fakes;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Presentation.Tests.AreaViews;

[UnitTest]
public class ReorderListsHandlerTests
{
    static readonly Guid User = Guid.NewGuid();
    static readonly Guid Area = Guid.NewGuid();
    static readonly DateTimeOffset Now = new(2026, 9, 30, 8, 0, 0, TimeSpan.Zero);

    readonly FakeDocumentStore<AreaView> _store = new();
    readonly FakeUnitOfWork _work = new();

    [Fact]
    public async Task TheFirstReorderStagesANewView()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var result = await Handler().HandleAsync(
            new ReorderLists(Guid.NewGuid(), User, Area, [a, b], b, 0), CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.True(_work.Committed);
        var view = Assert.IsType<AreaView>(Assert.Single(_work.Staged).Aggregate);
        Assert.Equal([b, a], view.Order);
    }

    [Fact]
    public async Task AnExistingViewIsReorderedInPlace()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var existing = new AreaView();
        existing.Apply(new ListsReordered(AreaView.IdFor(User, Area), User, Now, Area, [a, b, c]));
        _store.Seed(existing);

        await Handler().HandleAsync(
            new ReorderLists(Guid.NewGuid(), User, Area, [a, b, c], a, 2), CancellationToken.None);

        var view = Assert.IsType<AreaView>(Assert.Single(_work.Staged).Aggregate);
        Assert.Same(existing, view);
        Assert.Equal([b, c, a], view.Order);
    }

    [Fact]
    public async Task ARejectionCommitsNothing()
    {
        var result = await Handler().HandleAsync(
            new ReorderLists(Guid.NewGuid(), User, Area, [Guid.NewGuid()], Guid.NewGuid(), 0),
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.False(_work.Committed);
    }

    ReorderListsHandler Handler() => new(_store, _work, new FixedClock(Now));
}
