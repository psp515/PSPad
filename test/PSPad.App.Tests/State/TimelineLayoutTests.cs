using PSPad.App.State;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Today;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class TimelineLayoutTests
{
    [Fact]
    public void TwoOverlappingTasksShareTheWidthInLanes()
    {
        var layout = TimelineLayout.Of([Entry("A", 9, 0, 10, 0), Entry("B", 9, 30, 10, 30), Entry("C", 11, 0, 12, 0)]);

        Assert.Equal([("A", 0, 2), ("B", 1, 2), ("C", 0, 1)], Lanes(layout));
        Assert.Empty(layout.Groups);
    }

    [Fact]
    public void ShortTasksThatWouldTouchOnScreenTakeSeparateLanes()
    {
        var layout = TimelineLayout.Of([Entry("Pay rent", 13, 0, 13, 10), Entry("Vitamins", 13, 15, null, null)]);

        Assert.Equal([("Pay rent", 0, 2), ("Vitamins", 1, 2)], Lanes(layout));
    }

    [Fact]
    public void AShortBlockBesideAnotherIsNarrowAndALoneOneIsNot()
    {
        var layout = TimelineLayout.Of(
            [Entry("Pay rent", 13, 0, 13, 10), Entry("Vitamins", 13, 15, null, null), Entry("Gym", 18, 0, 18, 30)]);

        Assert.Equal([("Pay rent", 0, 2), ("Vitamins", 1, 2), ("Gym", 0, 1)], Lanes(layout));
    }

    [Fact]
    public void ThreeOverlappingTasksBecomeOneGroupSortedByStartThenName()
    {
        var layout = TimelineLayout.Of([Entry("Standup", 9, 0, 9, 15), Entry("Call", 9, 0, 9, 30), Entry("Review", 9, 0, 10, 0)]);

        Assert.Empty(layout.Blocks);
        var group = Assert.Single(layout.Groups);
        Assert.Equal(["Call", "Review", "Standup"], group.Entries.Select(entry => entry.Name));
        Assert.Equal((0, 1, 81), (group.Lane, group.Lanes, group.Needs));
        Assert.Equal(540, group.Start);
    }

    [Fact]
    public void AGroupMakesTheCrossLengthTallEnoughForItsLines()
    {
        var layout = TimelineLayout.Of(
            [Entry("A", 14, 0, 14, 30), Entry("B", 14, 0, 14, 30), Entry("C", 14, 10, 14, 40), Entry("D", 14, 20, 15, 0)]);

        var group = Assert.Single(layout.Groups);
        Assert.Equal((840, 905, 106), (group.Start, group.End, group.Needs));
        Assert.Equal(164, layout.CrossLength);
    }

    [Fact]
    public void ALongTaskKeepsItsColumnWhileTheTasksBesideItAreGroupedInTheOther()
    {
        var layout = TimelineLayout.Of(
        [
            Entry("Workday", 10, 0, 18, 0), Entry("Call", 11, 0, null, null),
            Entry("Review", 16, 0, 17, 0), Entry("Sync", 16, 15, 17, 0), Entry("Demo", 16, 45, 17, 15)
        ]);

        Assert.Equal([("Workday", 0, 2), ("Call", 1, 2)], Lanes(layout));
        var group = Assert.Single(layout.Groups);
        Assert.Equal((1, 2), (group.Lane, group.Lanes));
        Assert.Equal(["Review", "Sync", "Demo"], group.Entries.Select(entry => entry.Name));
    }

    [Fact]
    public void AnEmptyDayHasOnlyTheFrame()
    {
        var layout = TimelineLayout.Of([]);

        Assert.Equal(54, layout.CrossLength);
    }

    static (string Name, int Lane, int Lanes)[] Lanes(TimelineLayout layout) =>
        [.. layout.Blocks.Select(block => (block.Entry.Name, block.Lane, block.Lanes))];

    static TodayEntry Entry(string name, int sh, int sm, int? eh, int? em) =>
        new(Guid.NewGuid(), Guid.NewGuid(), name, false, null, false, false, 0,
            TaskTime.Of(new TimeOnly(sh, sm), eh is { } h ? new TimeOnly(h, em!.Value) : null));
}
