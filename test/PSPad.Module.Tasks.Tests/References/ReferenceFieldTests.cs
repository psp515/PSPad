using PSPad.Abstractions;
using PSPad.Module.Tasks.References;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.References;

[UnitTest]
public class ReferenceFieldTests
{
    static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly DateTimeOffset Now = new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AFieldIsAppendedWithItsLabelValueAndHint()
    {
        var item = ReferenceItemTests.Item();

        item.ApplyAll(ReferenceItem.Decide(
            item, new AddReferenceField(Guid.NewGuid(), User, item.Id, Guid.NewGuid(), "Colour", "black", null), Now));
        item.ApplyAll(ReferenceItem.Decide(
            item, new AddReferenceField(Guid.NewGuid(), User, item.Id, Guid.NewGuid(), "Left", "350 g", "quantity"), Now));

        Assert.Equal("Left", item.Fields[1].Label);
        Assert.Equal("350 g", item.Fields[1].Value);
        Assert.Equal("quantity", item.Fields[1].Display);
        Assert.Equal(1, item.Fields[1].Position);
    }

    [Fact]
    public void AFieldNeedsALabel()
    {
        var item = ReferenceItemTests.Item();

        Assert.Throws<DomainRejectedException>(() =>
            ReferenceItem.Decide(item, new AddReferenceField(Guid.NewGuid(), User, item.Id, Guid.NewGuid(), "  ", "black", null), Now));
    }

    [Fact]
    public void AFieldMayHaveAnEmptyValue()
    {
        var item = ReferenceItemTests.Item();

        item.ApplyAll(ReferenceItem.Decide(
            item, new AddReferenceField(Guid.NewGuid(), User, item.Id, Guid.NewGuid(), "Colour", "", null), Now));

        Assert.Equal("", item.Fields[0].Value);
    }

    [Fact]
    public void ABlankDisplayHintIsStoredAsNone()
    {
        var item = ReferenceItemTests.Item();

        item.ApplyAll(ReferenceItem.Decide(
            item, new AddReferenceField(Guid.NewGuid(), User, item.Id, Guid.NewGuid(), "Colour", "black", " "), Now));

        Assert.Null(item.Fields[0].Display);
    }

    [Fact]
    public void AddingTheSameFieldIdTwiceIsHarmless()
    {
        var item = ReferenceItemTests.Item();
        var fieldId = Guid.NewGuid();
        item.ApplyAll(ReferenceItem.Decide(
            item, new AddReferenceField(Guid.NewGuid(), User, item.Id, fieldId, "Colour", "black", null), Now));

        Assert.Empty(ReferenceItem.Decide(
            item, new AddReferenceField(Guid.NewGuid(), User, item.Id, fieldId, "Colour", "black", null), Now));
    }

    [Fact]
    public void EditingChangesLabelValueAndHint()
    {
        var item = ReferenceItemTests.Item();
        var fieldId = Guid.NewGuid();
        item.ApplyAll(ReferenceItem.Decide(
            item, new AddReferenceField(Guid.NewGuid(), User, item.Id, fieldId, "Colour", "black", null), Now));

        item.ApplyAll(ReferenceItem.Decide(
            item, new EditReferenceField(Guid.NewGuid(), User, item.Id, fieldId, "Colour", "white", "chip"), Now));

        Assert.Equal("white", item.Fields[0].Value);
        Assert.Equal("chip", item.Fields[0].Display);
    }

    [Fact]
    public void EditingAnUnknownFieldIsRejected()
    {
        var item = ReferenceItemTests.Item();

        Assert.Throws<DomainRejectedException>(() =>
            ReferenceItem.Decide(
                item, new EditReferenceField(Guid.NewGuid(), User, item.Id, Guid.NewGuid(), "Colour", "white", null), Now));
    }

    [Fact]
    public void AnUnchangedEditEmitsNothing()
    {
        var item = ReferenceItemTests.Item();
        var fieldId = Guid.NewGuid();
        item.ApplyAll(ReferenceItem.Decide(
            item, new AddReferenceField(Guid.NewGuid(), User, item.Id, fieldId, "Colour", "black", null), Now));

        Assert.Empty(ReferenceItem.Decide(
            item, new EditReferenceField(Guid.NewGuid(), User, item.Id, fieldId, "Colour", "black", null), Now));
    }

    [Fact]
    public void MovingAFieldReordersDensely()
    {
        var item = ReferenceItemTests.Item();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        item.ApplyAll(ReferenceItem.Decide(item, new AddReferenceField(Guid.NewGuid(), User, item.Id, first, "A", "1", null), Now));
        item.ApplyAll(ReferenceItem.Decide(item, new AddReferenceField(Guid.NewGuid(), User, item.Id, second, "B", "2", null), Now));
        item.ApplyAll(ReferenceItem.Decide(item, new AddReferenceField(Guid.NewGuid(), User, item.Id, third, "C", "3", null), Now));

        item.ApplyAll(ReferenceItem.Decide(item, new MoveReferenceField(Guid.NewGuid(), User, item.Id, third, 0), Now));

        Assert.Equal([third, first, second], item.Fields.Select(field => field.Id).ToArray());
        Assert.Equal([0, 1, 2], item.Fields.Select(field => field.Position).ToArray());
    }

    [Fact]
    public void RemovingAFieldKeepsPositionsDense()
    {
        var item = ReferenceItemTests.Item();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        item.ApplyAll(ReferenceItem.Decide(item, new AddReferenceField(Guid.NewGuid(), User, item.Id, first, "A", "1", null), Now));
        item.ApplyAll(ReferenceItem.Decide(item, new AddReferenceField(Guid.NewGuid(), User, item.Id, second, "B", "2", null), Now));
        item.ApplyAll(ReferenceItem.Decide(item, new AddReferenceField(Guid.NewGuid(), User, item.Id, third, "C", "3", null), Now));

        item.ApplyAll(ReferenceItem.Decide(item, new RemoveReferenceField(Guid.NewGuid(), User, item.Id, second), Now));

        Assert.Equal([first, third], item.Fields.Select(field => field.Id).ToArray());
        Assert.Equal([0, 1], item.Fields.Select(field => field.Position).ToArray());
    }

    [Fact]
    public void RemovingAnUnknownFieldIsRejected()
    {
        var item = ReferenceItemTests.Item();

        Assert.Throws<DomainRejectedException>(() =>
            ReferenceItem.Decide(item, new RemoveReferenceField(Guid.NewGuid(), User, item.Id, Guid.NewGuid()), Now));
    }
}
