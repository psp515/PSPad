using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class TimelineAxisTests
{
    [Fact]
    public void ThreeOrMoreEmptyHoursFoldIntoOneBand()
    {
        var axis = TimelineAxis.For([(9 * 60, 10 * 60)], null, new HashSet<int>());

        Assert.Equal(
            [(0, 9, true), (9, 10, false), (10, 24, true)],
            axis.Segments.Select(segment => (segment.StartHour, segment.EndHour, segment.Folded)));
    }

    [Fact]
    public void FewerThanThreeEmptyHoursStayAsHours()
    {
        var axis = TimelineAxis.For([(9 * 60, 10 * 60), (12 * 60, 13 * 60)], null, new HashSet<int>());

        Assert.Equal(
            [(0, 9, true), (9, 10, false), (10, 11, false), (11, 12, false), (12, 13, false), (13, 24, true)],
            axis.Segments.Select(segment => (segment.StartHour, segment.EndHour, segment.Folded)));
    }

    [Fact]
    public void AnHourWithATaskWeighsTwiceAnEmptyOne()
    {
        var axis = TimelineAxis.For([(9 * 60, 10 * 60), (12 * 60, 13 * 60)], null, new HashSet<int>());

        Assert.Equal(2, axis.Segments.Single(segment => segment.StartHour == 9).Weight);
        Assert.Equal(1, axis.Segments.Single(segment => segment.StartHour == 10).Weight);
    }

    [Fact]
    public void AnHourWithThreeTasksWeighsMore()
    {
        var axis = TimelineAxis.For([(540, 570), (545, 575), (550, 580)], null, new HashSet<int>());

        Assert.Equal(3, axis.Segments.Single(segment => segment.StartHour == 9).Weight);
    }

    [Fact]
    public void TheCurrentHourIsNeverFolded()
    {
        var axis = TimelineAxis.For([], 14 * 60 + 20, new HashSet<int>());

        Assert.Equal(
            [(0, 14, true), (14, 15, false), (15, 24, true)],
            axis.Segments.Select(segment => (segment.StartHour, segment.EndHour, segment.Folded)));
        Assert.Equal(2, axis.Segments[1].Weight);
    }

    [Fact]
    public void AnEmptyDayIsOneBand()
    {
        var axis = TimelineAxis.For([], null, new HashSet<int>());

        Assert.Equal([(0, 24, true)], axis.Segments.Select(segment => (segment.StartHour, segment.EndHour, segment.Folded)));
    }

    [Fact]
    public void AnUnfoldedBandShowsItsHours()
    {
        var axis = TimelineAxis.For([(9 * 60, 10 * 60)], null, new HashSet<int> { 0 });

        Assert.Equal(Enumerable.Range(0, 10), axis.Segments.TakeWhile(segment => !segment.Folded).Select(segment => segment.StartHour));
        Assert.True(axis.Segments[^1].Folded);
    }

    [Fact]
    public void APositionSharesTheLengthLeftAfterTheBandsByWeight()
    {
        var axis = TimelineAxis.For([(9 * 60, 10 * 60)], null, new HashSet<int>());

        Assert.Equal("calc((100% - 56px) * 0 + 28px)", axis.At(9 * 60));
        Assert.Equal("calc((100% - 56px) * 0.5 + 28px)", axis.At(9 * 60 + 30));
        Assert.Equal("calc((100% - 56px) * 1 + 28px)", axis.At(10 * 60));
        Assert.Equal("calc((100% - 56px) * 1 + 56px)", axis.At(24 * 60));
    }

    [Fact]
    public void ANaturalLengthGrowsWithWhatTheDayHolds()
    {
        var shortDay = TimelineAxis.For([(15 * 60, 16 * 60)], null, new HashSet<int>());
        var longDay = TimelineAxis.For([(7 * 60, 9 * 60), (12 * 60, 18 * 60)], null, new HashSet<int>());

        Assert.Equal(2 * 40 + 2 * TimelineAxis.FoldSize, shortDay.NaturalLength(40));
        Assert.True(longDay.NaturalLength(40) > shortDay.NaturalLength(40) * 3);
    }

    [Fact]
    public void AStretchIsTheDistanceBetweenTwoPositions()
    {
        var axis = TimelineAxis.For([(9 * 60, 10 * 60)], null, new HashSet<int>());

        Assert.Equal("calc((100% - 56px) * 0.5 + 0px)", axis.Stretch(9 * 60, 9 * 60 + 30));
        Assert.Equal("calc((100% - 56px) * 1 + 14px)", axis.Stretch(9 * 60, 17 * 60));
    }
}
