using PSPad.Module.Presentation.Ordering;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Presentation.Tests.Ordering;

[UnitTest]
public class ArrangedTests
{
    static readonly DateTimeOffset Start = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void WithNoOrderElementsFollowCreationDate()
    {
        var newer = new Item(Guid.NewGuid(), Start.AddDays(2));
        var older = new Item(Guid.NewGuid(), Start);
        var middle = new Item(Guid.NewGuid(), Start.AddDays(1));

        Assert.Equal([older, middle, newer], Sort([newer, older, middle], []));
    }

    [Fact]
    public void OrderedElementsComeFirstInTheirOrder()
    {
        var first = new Item(Guid.NewGuid(), Start);
        var second = new Item(Guid.NewGuid(), Start.AddDays(1));
        var third = new Item(Guid.NewGuid(), Start.AddDays(2));

        Assert.Equal([third, first, second], Sort([first, second, third], [third.Id, first.Id]));
    }

    [Fact]
    public void UnorderedElementsFollowByCreationDateThenId()
    {
        var low = new Item(Guid.Parse("00000000-0000-0000-0000-000000000001"), Start);
        var high = new Item(Guid.Parse("00000000-0000-0000-0000-000000000002"), Start);
        var ordered = new Item(Guid.NewGuid(), Start.AddDays(5));

        Assert.Equal([ordered, low, high], Sort([high, ordered, low], [ordered.Id]));
    }

    [Fact]
    public void StaleIdsAreIgnored()
    {
        var only = new Item(Guid.NewGuid(), Start);

        Assert.Equal([only], Sort([only], [Guid.NewGuid(), only.Id]));
    }

    [Fact]
    public void AMissingCreationDateSortsFirstAmongTheUnordered()
    {
        var dated = new Item(Guid.NewGuid(), Start);
        var undated = new Item(Guid.NewGuid(), null);

        Assert.Equal([undated, dated], Sort([dated, undated], []));
    }

    static IReadOnlyList<Item> Sort(IEnumerable<Item> items, IReadOnlyList<Guid> order) =>
        Arranged.Sort(items, order, item => item.Id, item => item.CreatedAt);

    sealed record Item(Guid Id, DateTimeOffset? CreatedAt);
}
