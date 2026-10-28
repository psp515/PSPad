using PSPad.Abstractions;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.References;

[UnitTest]
public class SharedReferenceTests
{
    static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid Member = Guid.Parse("22222222-2222-2222-2222-222222222222");
    static readonly Guid Stranger = Guid.Parse("33333333-3333-3333-3333-333333333333");
    static readonly Guid ListId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    static readonly Guid ItemId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    static readonly DateTimeOffset Now = new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);
    static readonly ListAccess Access = new(Owner, Member);

    [Fact]
    public void AMemberCreatesAnItemOwnedByTheListOwner()
    {
        var created = Assert.IsType<ReferenceItemCreated>(Assert.Single(ReferenceItem.Decide(
            null, new CreateReferenceItem(Guid.NewGuid(), Member, ItemId, ListId, "PLA Black", 0), Now, Access)));
        Assert.Equal(Owner, created.UserId);
    }

    [Fact]
    public void AMemberRenamesStarsAddsEditsMovesRemovesAFieldAndDeletesTheOwnersItem()
    {
        var item = OwnedItem();
        var renamed = Assert.Single(ReferenceItem.Decide(
            item, new RenameReferenceItem(Guid.NewGuid(), Member, item.Id, "PETG Grey"), Now, Access));
        Assert.Equal(Owner, renamed.UserId);

        Assert.Single(ReferenceItem.Decide(
            item, new StarReferenceItem(Guid.NewGuid(), Member, item.Id, true), Now, Access));

        var fieldId = Guid.NewGuid();
        var added = Assert.Single(ReferenceItem.Decide(
            item, new AddReferenceField(Guid.NewGuid(), Member, item.Id, fieldId, "Colour", "black", null), Now, Access));
        Assert.Equal(Owner, added.UserId);
        item.ApplyAll([added]);

        Assert.Single(ReferenceItem.Decide(
            item, new EditReferenceField(Guid.NewGuid(), Member, item.Id, fieldId, "Colour", "white", null), Now, Access));

        var otherFieldId = Guid.NewGuid();
        item.ApplyAll(ReferenceItem.Decide(
            item, new AddReferenceField(Guid.NewGuid(), Member, item.Id, otherFieldId, "Weight", "1kg", null), Now, Access));
        Assert.Single(ReferenceItem.Decide(
            item, new MoveReferenceField(Guid.NewGuid(), Member, item.Id, otherFieldId, 0), Now, Access));

        Assert.Single(ReferenceItem.Decide(
            item, new RemoveReferenceField(Guid.NewGuid(), Member, item.Id, fieldId), Now, Access));

        Assert.Single(ReferenceItem.Decide(item, new DeleteReferenceItem(Guid.NewGuid(), Member, item.Id), Now, Access));
    }

    [Fact]
    public void WithoutAccessAMemberIsStillAStranger() =>
        Assert.Throws<DomainRejectedException>(() =>
            ReferenceItem.Decide(OwnedItem(), new RenameReferenceItem(Guid.NewGuid(), Member, ItemId, "X"), Now));

    [Fact]
    public void AccessToAnotherOwnersListDoesNotReachThisItem() =>
        Assert.Throws<DomainRejectedException>(() =>
            ReferenceItem.Decide(
                OwnedItem(), new RenameReferenceItem(Guid.NewGuid(), Member, ItemId, "X"), Now,
                new ListAccess(Stranger, Member)));

    static ReferenceItem OwnedItem()
    {
        var item = new ReferenceItem();
        item.ApplyAll(ReferenceItem.Decide(
            null, new CreateReferenceItem(Guid.NewGuid(), Owner, ItemId, ListId, "PLA Black", 0), Now));
        return item;
    }

    static TaskList ListWithMember()
    {
        var list = new TaskList();
        list.Apply(new TaskListCreated(ListId, Owner, Now, Guid.NewGuid(), "Filaments", ListKind.Reference));
        list.Apply(new TaskListShared(list.Id, Owner, Now, "k3Jv9s2mQ0x7b1nR4tYw8eZa", "Łukasz"));
        list.Apply(new TaskListJoined(list.Id, Owner, Now, Member, "Anna"));
        return list;
    }
}
