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
}
