using PSPad.Module.Presentation.AreaViews;
using PSPad.Module.Presentation.ListViews;
using PSPad.Module.Presentation.Tests.Fakes;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Presentation.Tests.ListViews;

[UnitTest]
public class PlaceListHandlerTests
{
    static readonly Guid Me = Guid.NewGuid();
    static readonly Guid Other = Guid.NewGuid();
    static readonly Guid List = Guid.NewGuid();
    static readonly Guid MyArea = Guid.NewGuid();
    static readonly DateTimeOffset Now = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);

    readonly FakeDocumentStore<ListView> _store = new();
    readonly FakeUnitOfWork _work = new();
    readonly FakeViewTargets _targets = new FakeViewTargets().SeesList(Me, List).OwnsArea(Me, MyArea);

    [Fact]
    public async Task AVisibleListIsFiledIntoTheUsersOwnArea()
    {
        var result = await Handler().HandleAsync(new PlaceList(Guid.NewGuid(), Me, List, MyArea), CancellationToken.None);

        Assert.True(result.Accepted);
        var view = Assert.IsType<ListView>(Assert.Single(_work.Staged).Aggregate);
        Assert.Equal(MyArea, view.AreaId);
    }

    [Fact]
    public async Task AVisibleListCanBeUnfiled()
    {
        var result = await Handler().HandleAsync(new PlaceList(Guid.NewGuid(), Me, List, null), CancellationToken.None);

        Assert.True(result.Accepted);
    }

    [Fact]
    public async Task AListTheUserCannotSeeIsRejectedSoItsIdCannotSquatAnotherMembersView()
    {
        var crafted = AreaView.IdFor(AreaView.IdFor(Other, List), Me);

        var result = await Handler().HandleAsync(new PlaceList(Guid.NewGuid(), Me, crafted, MyArea), CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.False(_work.Committed);
    }

    [Fact]
    public async Task AnAreaTheUserDoesNotOwnIsRejected()
    {
        var result = await Handler().HandleAsync(
            new PlaceList(Guid.NewGuid(), Me, List, Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.False(_work.Committed);
    }

    PlaceListHandler Handler() => new(_store, _targets, _work, new FixedClock(Now));
}
