using PSPad.Abstractions;
using PSPad.Module.Tasks.References;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.References;

[UnitTest]
public class ReferenceItemTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid Stranger = Guid.Parse("99999999-9999-9999-9999-999999999999");
    static readonly Guid List = Guid.Parse("22222222-2222-2222-2222-222222222222");
    static readonly DateTimeOffset Now = new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);

    public static ReferenceItem Item(string name = "PLA Black")
    {
        var item = new ReferenceItem();
        item.ApplyAll(ReferenceItem.Decide(null, new CreateReferenceItem(Guid.NewGuid(), User, Guid.NewGuid(), List, name, 0), Now));
        return item;
    }

    [Fact]
    public void AnItemIsCreatedInItsListWithNothingElse()
    {
        var item = Item();

        Assert.Equal("PLA Black", item.Name);
        Assert.Equal(List, item.ListId);
        Assert.Equal("", item.Description);
        Assert.False(item.Starred);
        Assert.Empty(item.Fields);
    }

    [Fact]
    public void AnItemNeedsAName() =>
        Assert.Throws<DomainRejectedException>(() =>
            ReferenceItem.Decide(null, new CreateReferenceItem(Guid.NewGuid(), User, Guid.NewGuid(), List, "  ", 0), Now));

    [Fact]
    public void AnItemNeedsAList() =>
        Assert.Throws<DomainRejectedException>(() =>
            ReferenceItem.Decide(null, new CreateReferenceItem(Guid.NewGuid(), User, Guid.NewGuid(), Guid.Empty, "x", 0), Now));

    [Fact]
    public void CreatingTheSameItemTwiceIsRejected()
    {
        var item = Item();

        Assert.Throws<DomainRejectedException>(() =>
            ReferenceItem.Decide(item, new CreateReferenceItem(Guid.NewGuid(), User, item.Id, List, "x", 0), Now));
    }

    [Fact]
    public void RenamingTrimsTheName()
    {
        var item = Item();

        item.ApplyAll(ReferenceItem.Decide(item, new RenameReferenceItem(Guid.NewGuid(), User, item.Id, "  PETG Grey "), Now));

        Assert.Equal("PETG Grey", item.Name);
    }

    [Fact]
    public void DescribingFollowsTheTaskRules()
    {
        var item = Item();

        item.ApplyAll(ReferenceItem.Decide(item, new SetReferenceItemDescription(Guid.NewGuid(), User, item.Id, "Dry 4h at 50 °C\n\n"), Now));

        Assert.Equal("Dry 4h at 50 °C", item.Description);
        Assert.Empty(ReferenceItem.Decide(item, new SetReferenceItemDescription(Guid.NewGuid(), User, item.Id, "Dry 4h at 50 °C"), Now));
    }

    [Fact]
    public void StarringAndUnstarring()
    {
        var item = Item();

        item.ApplyAll(ReferenceItem.Decide(item, new StarReferenceItem(Guid.NewGuid(), User, item.Id, true), Now));
        Assert.True(item.Starred);

        item.ApplyAll(ReferenceItem.Decide(item, new StarReferenceItem(Guid.NewGuid(), User, item.Id, false), Now));
        Assert.False(item.Starred);
    }

    [Fact]
    public void MovingChangesTheList()
    {
        var item = Item();
        var other = Guid.NewGuid();

        item.ApplyAll(ReferenceItem.Decide(item, new MoveReferenceItemToList(Guid.NewGuid(), User, item.Id, other), Now));

        Assert.Equal(other, item.ListId);
    }

    [Fact]
    public void DeletingMarksItDeletedAndLaterCommandsAreRejected()
    {
        var item = Item();
        item.ApplyAll(ReferenceItem.Decide(item, new DeleteReferenceItem(Guid.NewGuid(), User, item.Id), Now));

        Assert.True(item.Deleted);
        Assert.Throws<DomainRejectedException>(() =>
            ReferenceItem.Decide(item, new RenameReferenceItem(Guid.NewGuid(), User, item.Id, "x"), Now));
    }

    [Fact]
    public void SomebodyElsesItemIsRejected() =>
        Assert.Throws<DomainRejectedException>(() =>
            ReferenceItem.Decide(Item(), new RenameReferenceItem(Guid.NewGuid(), Stranger, Guid.NewGuid(), "x"), Now));
}
