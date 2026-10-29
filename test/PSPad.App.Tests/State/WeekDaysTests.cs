using PSPad.App.State;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
[Collection("Culture")]
public class WeekDaysTests : IDisposable
{
    static readonly DateOnly Thursday = new(2026, 10, 8);

    readonly CultureScope _culture = new("en-GB");

    public void Dispose() => _culture.Dispose();

    [Fact]
    public void AWeekRunsMondayToSunday() =>
        Assert.Equal(Enumerable.Range(5, 7).Select(day => new DateOnly(2026, 10, day)), WeekDays.WeekOf(Thursday));

    [Fact]
    public void ASundayBelongsToTheWeekThatEndsWithIt() =>
        Assert.Equal(new DateOnly(2026, 10, 5), WeekDays.StartOfWeek(new DateOnly(2026, 10, 11)));

    [Fact]
    public void AWeekAcrossTheNewYearStartsInDecember() =>
        Assert.Equal(new DateOnly(2026, 12, 28), WeekDays.StartOfWeek(new DateOnly(2027, 1, 3)));

    [Fact]
    public void ThePolishWeekStartsOnMonday()
    {
        CultureScope.Use("pl-PL");

        Assert.Equal(new DateOnly(2026, 10, 12), WeekDays.StartOfWeek(new DateOnly(2026, 10, 18)));
    }

    [Fact]
    public void TheWeekStartsOnSundayWhereTheCultureSaysSo()
    {
        CultureScope.Use("en-US");

        Assert.Equal(Enumerable.Range(4, 7).Select(day => new DateOnly(2026, 10, day)), WeekDays.WeekOf(Thursday));
    }

    [Theory]
    [InlineData(-80, 10, 1)]
    [InlineData(80, -10, -1)]
    [InlineData(-47, 0, 0)]
    [InlineData(30, 0, 0)]
    [InlineData(-60, 90, 0)]
    [InlineData(60, -60, 0)]
    public void ASwipeTurnsTheWeekOnlyWhenItIsLongAndMostlySideways(double dx, double dy, int expected) =>
        Assert.Equal(expected, WeekDays.SwipeDirection(dx, dy));
}
