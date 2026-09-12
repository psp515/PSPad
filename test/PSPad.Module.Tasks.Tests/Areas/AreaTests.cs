using PSPad.Abstractions;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Tests.Fakes;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Areas;

[UnitTest]
public class AreaTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreatingAnAreaNamesItAndTakesTheNextPosition()
    {
        var command = new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), "Home", 2);

        var events = Area.Decide(null, command, Now);

        var created = Assert.IsType<AreaCreated>(Assert.Single(events));
        Assert.Equal("Home", created.Name);
        Assert.Equal(2, created.Position);
        Assert.Equal(User, created.UserId);
    }

    [Fact]
    public void CreatingAnAreaWithABlankNameIsRejected()
    {
        var command = new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), "   ", 0);

        var rejection = Assert.Throws<DomainRejectedException>(
            () => Area.Decide(null, command, Now));

        Assert.Contains("name", rejection.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RenamingToTheSameNameProducesNoEvent()
    {
        var area = Existing("Home");

        var events = Area.Decide(area, new RenameArea(Guid.NewGuid(), User, area.Id, "Home"), Now);

        Assert.Empty(events);
    }

    [Fact]
    public void DeletingAnAreaMarksItDeleted()
    {
        var area = Existing("Home");

        var events = Area.Decide(area, new DeleteArea(Guid.NewGuid(), User, area.Id), Now);
        area.ApplyAll(events);

        Assert.IsType<AreaDeleted>(Assert.Single(events));
        Assert.True(area.Deleted);
    }

    [Fact]
    public void ActingOnSomebodyElsesAreaIsRejected()
    {
        var area = Existing("Home");
        var intruder = new RenameArea(Guid.NewGuid(), Guid.NewGuid(), area.Id, "Theirs");

        Assert.Throws<DomainRejectedException>(() => Area.Decide(area, intruder, Now));
    }

    [Fact]
    public async Task TheHandlerStagesTheAreaAndCommits()
    {
        var store = new FakeDocumentStore<Area>();
        var work = new FakeUnitOfWork();
        var handler = new CreateAreaHandler(store, work, new FixedClock(Now));

        var result = await handler.HandleAsync(
            new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), "Home", 0), CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.True(work.Committed);
        Assert.IsType<AreaCreated>(Assert.Single(work.Events));
    }

    [Fact]
    public async Task ARejectedCommandCommitsNothing()
    {
        var store = new FakeDocumentStore<Area>();
        var work = new FakeUnitOfWork();
        var handler = new CreateAreaHandler(store, work, new FixedClock(Now));

        var result = await handler.HandleAsync(
            new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), " ", 0), CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.False(work.Committed);
        Assert.NotNull(result.Rejection);
    }

    static Area Existing(string name)
    {
        var area = new Area();
        area.Apply(new AreaCreated(Guid.NewGuid(), User, Now, name, 0));
        return area;
    }
}
