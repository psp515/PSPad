using PSPad.Abstractions;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.References;

[UnitTest]
public class ReferenceSnapshotMarkTests
{
    static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid Member = Guid.Parse("22222222-2222-2222-2222-222222222222");
    static readonly Guid List = Guid.Parse("33333333-3333-3333-3333-333333333333");
    static readonly Guid SnapshotId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    static readonly DateTimeOffset Now = new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);
    static readonly ListAccess MemberAccess = new(Owner, Member);

    [Fact]
    public void AMarkOnTheItemIsRecorded()
    {
        var item = Item();

        item.ApplyAll(ReferenceItem.Decide(
            item, new MarkReferenceItemFromSnapshot(Guid.NewGuid(), Owner, item.Id, SnapshotId, true), Now));

        Assert.Equal(new SnapshotMark(SnapshotId, null, Now), Assert.Single(item.SnapshotMarks));
    }

    [Fact]
    public void UnmarkingRemovesTheMark()
    {
        var item = Item();
        item.ApplyAll(ReferenceItem.Decide(
            item, new MarkReferenceItemFromSnapshot(Guid.NewGuid(), Owner, item.Id, SnapshotId, true), Now));

        item.ApplyAll(ReferenceItem.Decide(
            item, new MarkReferenceItemFromSnapshot(Guid.NewGuid(), Owner, item.Id, SnapshotId, false), Now));

        Assert.Empty(item.SnapshotMarks);
    }

    [Fact]
    public void AnUnchangedMarkEmitsNothing()
    {
        var item = Item();
        item.ApplyAll(ReferenceItem.Decide(
            item, new MarkReferenceItemFromSnapshot(Guid.NewGuid(), Owner, item.Id, SnapshotId, true), Now));

        Assert.Empty(ReferenceItem.Decide(
            item, new MarkReferenceItemFromSnapshot(Guid.NewGuid(), Owner, item.Id, SnapshotId, true), Now));
    }

    [Fact]
    public void ADeletedItemIsRejected()
    {
        var item = Item();
        item.ApplyAll(ReferenceItem.Decide(item, new DeleteReferenceItem(Guid.NewGuid(), Owner, item.Id), Now));

        Assert.Throws<DomainRejectedException>(() => ReferenceItem.Decide(
            item, new MarkReferenceItemFromSnapshot(Guid.NewGuid(), Owner, item.Id, SnapshotId, true), Now));
    }

    [Fact]
    public void ClearingDismissesEveryMark()
    {
        var item = Item();
        item.ApplyAll(ReferenceItem.Decide(
            item, new MarkReferenceItemFromSnapshot(Guid.NewGuid(), Owner, item.Id, SnapshotId, true), Now));

        item.ApplyAll(ReferenceItem.Decide(item, new ClearReferenceItemSnapshotMarks(Guid.NewGuid(), Owner, item.Id), Now));

        Assert.Empty(item.SnapshotMarks);
    }

    [Fact]
    public void ClearingWithNoMarksEmitsNothing()
    {
        var item = Item();

        Assert.Empty(ReferenceItem.Decide(item, new ClearReferenceItemSnapshotMarks(Guid.NewGuid(), Owner, item.Id), Now));
    }

    [Fact]
    public void AMemberClears()
    {
        var item = Item();
        item.ApplyAll(ReferenceItem.Decide(
            item, new MarkReferenceItemFromSnapshot(Guid.NewGuid(), Owner, item.Id, SnapshotId, true), Now));

        var cleared = Assert.Single(ReferenceItem.Decide(
            item, new ClearReferenceItemSnapshotMarks(Guid.NewGuid(), Member, item.Id), Now, MemberAccess));

        Assert.Equal(Owner, cleared.UserId);
    }

    static ReferenceItem Item()
    {
        var item = new ReferenceItem();
        item.ApplyAll(ReferenceItem.Decide(
            null, new CreateReferenceItem(Guid.NewGuid(), Owner, Guid.NewGuid(), List, "PLA Black", 0), Now));
        return item;
    }
}
