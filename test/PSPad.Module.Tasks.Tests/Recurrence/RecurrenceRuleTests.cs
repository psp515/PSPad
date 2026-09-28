using PSPad.Abstractions;
using PSPad.Module.Tasks.Recurrence;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Recurrence;

[UnitTest]
public class RecurrenceRuleTests
{
    static readonly DateOnly Start = new(2026, 9, 7);

    [Fact]
    public void ADailyRuleOccursEveryDayFromItsStart()
    {
        var rule = RecurrenceRule.Daily(Start);

        Assert.True(rule.OccursOn(Start));
        Assert.True(rule.OccursOn(Start.AddDays(1)));
        Assert.False(rule.OccursOn(Start.AddDays(-1)));
    }

    [Fact]
    public void AWeeklyRuleOccursOnlyOnItsDays()
    {
        var rule = RecurrenceRule.Weekly(Start, DayOfWeek.Monday, DayOfWeek.Thursday);

        Assert.True(rule.OccursOn(new DateOnly(2026, 9, 7)));
        Assert.True(rule.OccursOn(new DateOnly(2026, 9, 10)));
        Assert.False(rule.OccursOn(new DateOnly(2026, 9, 8)));
    }

    [Fact]
    public void AWeeklyRuleWithNoDaysIsRejected()
    {
        Assert.Throws<DomainRejectedException>(() => RecurrenceRule.Weekly(Start));
    }

    [Fact]
    public void AMonthlyRuleOccursOnItsDayOfTheMonth()
    {
        var rule = RecurrenceRule.MonthlyOnDay(Start, 15);

        Assert.True(rule.OccursOn(new DateOnly(2026, 9, 15)));
        Assert.True(rule.OccursOn(new DateOnly(2026, 10, 15)));
        Assert.False(rule.OccursOn(new DateOnly(2026, 9, 16)));
    }

    [Fact]
    public void AMonthlyRuleOnADayThatMonthLacksFallsOnTheLastDay()
    {
        var rule = RecurrenceRule.MonthlyOnDay(Start, 31);

        Assert.True(rule.OccursOn(new DateOnly(2026, 11, 30)));
        Assert.False(rule.OccursOn(new DateOnly(2026, 11, 29)));
    }

    [Fact]
    public void AMonthlyDayOutsideOneToThirtyOneIsRejected()
    {
        Assert.Throws<DomainRejectedException>(() => RecurrenceRule.MonthlyOnDay(Start, 0));
        Assert.Throws<DomainRejectedException>(() => RecurrenceRule.MonthlyOnDay(Start, 32));
    }

    [Fact]
    public void ADailyRuleEveryTwoDaysSkipsTheDaysBetween()
    {
        var rule = RecurrenceRule.Daily(Start).EveryNth(2);

        Assert.True(rule.OccursOn(Start));
        Assert.False(rule.OccursOn(Start.AddDays(1)));
        Assert.True(rule.OccursOn(Start.AddDays(2)));
        Assert.True(rule.OccursOn(Start.AddDays(10)));
    }

    [Fact]
    public void AWeeklyRuleEveryThreeWeeksCountsWeeksFromTheStartingWeek()
    {
        var wednesday = new DateOnly(2026, 9, 9);
        var rule = RecurrenceRule.Weekly(wednesday, DayOfWeek.Monday).EveryNth(3);

        Assert.False(rule.OccursOn(new DateOnly(2026, 9, 7)));
        Assert.False(rule.OccursOn(new DateOnly(2026, 9, 14)));
        Assert.False(rule.OccursOn(new DateOnly(2026, 9, 21)));
        Assert.True(rule.OccursOn(new DateOnly(2026, 9, 28)));
        Assert.False(rule.OccursOn(new DateOnly(2026, 10, 5)));
    }

    [Fact]
    public void AWeeklyIntervalCarriesAcrossAYearBoundary()
    {
        var rule = RecurrenceRule.Weekly(new DateOnly(2026, 12, 21), DayOfWeek.Monday).EveryNth(3);

        Assert.True(rule.OccursOn(new DateOnly(2026, 12, 21)));
        Assert.False(rule.OccursOn(new DateOnly(2026, 12, 28)));
        Assert.False(rule.OccursOn(new DateOnly(2027, 1, 4)));
        Assert.True(rule.OccursOn(new DateOnly(2027, 1, 11)));
    }

    [Fact]
    public void AMonthlyRuleEveryTwoMonthsClampsToTheMonthsLastDay()
    {
        var rule = RecurrenceRule.MonthlyOnDay(new DateOnly(2026, 9, 1), 31).EveryNth(2);

        Assert.True(rule.OccursOn(new DateOnly(2026, 9, 30)));
        Assert.False(rule.OccursOn(new DateOnly(2026, 10, 31)));
        Assert.True(rule.OccursOn(new DateOnly(2026, 11, 30)));
        Assert.True(rule.OccursOn(new DateOnly(2027, 1, 31)));
        Assert.False(rule.OccursOn(new DateOnly(2027, 2, 28)));
    }

    [Fact]
    public void AStoredIntervalOfZeroReadsAsEveryOne()
    {
        var rule = RecurrenceRule.Daily(Start) with { Interval = 0 };

        Assert.Equal(1, rule.Every);
        Assert.True(rule.OccursOn(Start.AddDays(1)));
    }

    [Fact]
    public void AnIntervalOutsideOneToNinetyNineIsRejected()
    {
        Assert.Throws<DomainRejectedException>(() => RecurrenceRule.Daily(Start).EveryNth(0));
        Assert.Throws<DomainRejectedException>(() => RecurrenceRule.Daily(Start).EveryNth(100));
    }
}
