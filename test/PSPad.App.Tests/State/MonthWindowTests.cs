using PSPad.App.State;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class MonthWindowTests
{
    static readonly YearMonth October = new(2026, 10);

    [Fact]
    public void BeforeItIsMeasuredTheWindowHoldsTwelveMonthsNewestFirst() =>
        Assert.Equal(
            [
                October, new(2026, 9), new(2026, 8), new(2026, 7), new(2026, 6), new(2026, 5), new(2026, 4), new(2026, 3),
                new(2026, 2), new(2026, 1), new(2025, 12), new(2025, 11)
            ],
            MonthWindow.NewestFirst(October, October, MonthWindow.DefaultCount));

    [Theory]
    [InlineData(1200, 20)]
    [InlineData(599, 10)]
    [InlineData(300, 5)]
    [InlineData(60, 3)]
    public void AsManyWholeMonthsFitAsTheWidthAllows(double width, int months) =>
        Assert.Equal(months, MonthWindow.Fitting(width));

    [Fact]
    public void AnOlderMonthAnchorsTheWindowAtItsOldEnd()
    {
        var months = MonthWindow.NewestFirst(new YearMonth(2024, 6), October, 12);

        Assert.Equal(new YearMonth(2025, 5), months[0]);
        Assert.Equal(new YearMonth(2024, 6), months[^1]);
    }

    [Fact]
    public void AMonthJustOutsideTheWindowShiftsItByOnlyWhatItNeeds()
    {
        var months = MonthWindow.NewestFirst(new YearMonth(2025, 10), October, 12);

        Assert.Equal(new YearMonth(2026, 9), months[0]);
        Assert.Equal(new YearMonth(2025, 10), months[^1]);
    }

    [Fact]
    public void AFutureMonthLeadsTheWindow()
    {
        var months = MonthWindow.NewestFirst(new YearMonth(2027, 1), October, 12);

        Assert.Equal(12, months.Count);
        Assert.Equal(new YearMonth(2027, 1), months[0]);
        Assert.Contains(October, months);
    }
}
