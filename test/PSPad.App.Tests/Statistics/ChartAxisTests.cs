using PSPad.App.Statistics;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Statistics;

[UnitTest]
public class ChartAxisTests
{
    static readonly DateOnly Last = new(2026, 3, 10);

    [Theory]
    [InlineData(30, 7)]
    [InlineData(90, 8)]
    [InlineData(365, 8)]
    public void LabelsAreThinnedToAReadableCountKeepingTheFirstAndLastDay(int days, int visible)
    {
        var labels = ChartAxis.Labels(Days(days), day => day);

        Assert.Equal(days, labels.Length);
        Assert.Equal(visible, labels.Count(label => label.Length > 0));
        Assert.NotEqual("", labels[0]);
        Assert.NotEqual("", labels[^1]);
    }

    [Fact]
    public void UpToNinetyDaysTheLabelsAreMonthAndDay()
    {
        var labels = ChartAxis.Labels(Days(90), day => day);

        Assert.All(labels.Where(label => label.Length > 0), label => Assert.Matches(@"^\d{2}-\d{2}$", label));
        Assert.Equal("03-10", labels[^1]);
    }

    [Fact]
    public void AYearOfDaysIsLabelledByMonthAndYear()
    {
        var labels = ChartAxis.Labels(Days(365), day => day);

        Assert.All(labels.Where(label => label.Length > 0), label => Assert.Matches(@"^[A-Za-z]{3} \d{4}$", label));
        Assert.EndsWith(" 2026", labels[^1]);
    }

    [Fact]
    public void AYearOfWeeksIsLabelledByTheDaysItSpansNotByItsPointCount()
    {
        var weeks = Enumerable.Range(0, 53).Select(week => Last.AddDays(-7 * (52 - week))).ToArray();

        var labels = ChartAxis.Labels(weeks, week => week, formatOverRange: weeks[^1].DayNumber - weeks[0].DayNumber);

        Assert.InRange(labels.Count(label => label.Length > 0), 4, 8);
        Assert.All(labels.Where(label => label.Length > 0), label => Assert.Matches(@"^[A-Za-z]{3} \d{4}$", label));
    }

    [Fact]
    public void NoPointsMeansNoLabels() =>
        Assert.Empty(ChartAxis.Labels(Array.Empty<DateOnly>(), day => day));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(7, 2)]
    [InlineData(40, 10)]
    [InlineData(230, 50)]
    public void TheYAxisStepIsARoundNumberForTheLargestValue(int max, int step) =>
        Assert.Equal(step, ChartAxis.Step(max));

    static DateOnly[] Days(int count) =>
        [.. Enumerable.Range(0, count).Select(index => Last.AddDays(index - (count - 1)))];
}
