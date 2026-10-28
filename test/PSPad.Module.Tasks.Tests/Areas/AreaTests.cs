using PSPad.Abstractions;
using PSPad.Module.Tasks.Areas;
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
    public void ADeletedOrMissingAreaCannotBeRequired()
    {
        var area = Existing("Home");
        area.ApplyAll(Area.Decide(area, new DeleteArea(Guid.NewGuid(), User, area.Id), Now));

        Assert.Contains("area", Assert.Throws<DomainRejectedException>(() => Area.Require(area, User)).Message, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<DomainRejectedException>(() => Area.Require(null, User));
    }

    [Fact]
    public void ALiveAreaIsRequiredOnlyByItsOwner()
    {
        var area = Existing("Home");

        Assert.Same(area, Area.Require(area, User));
        Assert.Throws<DomainRejectedException>(() => Area.Require(area, Guid.NewGuid()));
    }

    static Area Existing(string name)
    {
        var area = new Area();
        area.Apply(new AreaCreated(Guid.NewGuid(), User, Now, name, 0));
        return area;
    }
}
