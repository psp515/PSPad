using Microsoft.Extensions.Localization;
using PSPad.App.Resources;
using PSPad.App.State;
using PSPad.App.Tests.Localization;
using PSPad.Module.Tasks.Recurrence;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
[Collection("Culture")]
public class RecurrenceTextTests : IDisposable
{
    static readonly DateOnly Start = new(2026, 9, 24);
    static readonly IStringLocalizer<TasksStrings> Strings = TestLocalizers.For<TasksStrings>();

    readonly CultureScope _culture = new("en-GB");

    public void Dispose() => _culture.Dispose();

    [Fact]
    public void ItDescribesEachRuleInWords()
    {
        Assert.Equal("Never", RecurrenceText.Describe(null, Strings));
        Assert.Equal("Daily", RecurrenceText.Describe(RecurrenceRule.Daily(Start), Strings));
        Assert.Equal("Weekdays", RecurrenceText.Describe(RecurrenceRule.Weekly(Start,
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday), Strings));
        Assert.Equal("Weekly on Mon, Sun", RecurrenceText.Describe(
            RecurrenceRule.Weekly(Start, DayOfWeek.Sunday, DayOfWeek.Monday), Strings));
        Assert.Equal("Monthly on day 24", RecurrenceText.Describe(RecurrenceRule.MonthlyOnDay(Start, 24), Strings));
    }

    [Fact]
    public void AnIntervalAboveOneIsSpelledOut()
    {
        Assert.Equal("Every 2 days", RecurrenceText.Describe(RecurrenceRule.Daily(Start).EveryNth(2), Strings));
        Assert.Equal("Every 3 weeks on Mon, Thu", RecurrenceText.Describe(
            RecurrenceRule.Weekly(Start, DayOfWeek.Thursday, DayOfWeek.Monday).EveryNth(3), Strings));
        Assert.Equal("Every 2 months on day 15",
            RecurrenceText.Describe(RecurrenceRule.MonthlyOnDay(Start, 15).EveryNth(2), Strings));
    }

    [Theory]
    [InlineData(0, 0, "Not done yet")]
    [InlineData(1, 0, "Done 1 time")]
    [InlineData(4, 0, "Done 4 times")]
    [InlineData(7, 3, "Done 7 times · 3 in a row")]
    public void TheTallyReadsAsDoneCountAndStreak(int total, int streak, string expected) =>
        Assert.Equal(expected, RecurrenceText.DescribeTally(new RepeatTally(total, streak), Strings));

    [Fact]
    public void AYearlyRuleNamesItsDayAndMonth()
    {
        var rule = RecurrenceRule.Yearly(new DateOnly(2026, 3, 15));

        Assert.Equal("Yearly on 15 Mar", RecurrenceText.Describe(rule, Strings));
        Assert.Equal("Every 2 years on 15 Mar", RecurrenceText.Describe(rule.EveryNth(2), Strings));
    }

    [Fact]
    public void TheUnitLabelsAreSingularForOneInEnglish()
    {
        Assert.Equal("week", RecurrenceText.UnitLabel(RecurrenceKind.Weekly, 1, Strings));
        Assert.Equal("weeks", RecurrenceText.UnitLabel(RecurrenceKind.Weekly, 2, Strings));
    }

    [Fact]
    public void TheUnitLabelsAgreeWithTheAmountInPolish()
    {
        CultureScope.Use("pl-PL");

        Assert.Equal("dzień", RecurrenceText.UnitLabel(RecurrenceKind.Daily, 1, Strings));
        Assert.Equal("tygodnie", RecurrenceText.UnitLabel(RecurrenceKind.Weekly, 2, Strings));
        Assert.Equal("tygodni", RecurrenceText.UnitLabel(RecurrenceKind.Weekly, 5, Strings));
        Assert.Equal("lata", RecurrenceText.UnitLabel(RecurrenceKind.Yearly, 3, Strings));
    }

    [Fact]
    public void IntervalsAndTalliesAgreeWithTheirCountInPolish()
    {
        CultureScope.Use("pl-PL");

        Assert.Equal("Co 2 dni", RecurrenceText.Describe(RecurrenceRule.Daily(Start).EveryNth(2), Strings));
        Assert.Equal("Co 2 tygodnie: pon., czw.", RecurrenceText.Describe(
            RecurrenceRule.Weekly(Start, DayOfWeek.Thursday, DayOfWeek.Monday).EveryNth(2), Strings));
        Assert.Equal("Co 5 miesięcy, 15. dnia", RecurrenceText.Describe(RecurrenceRule.MonthlyOnDay(Start, 15).EveryNth(5), Strings));
        Assert.Equal("Wykonano 1 raz · 1 z rzędu", RecurrenceText.DescribeTally(new RepeatTally(1, 1), Strings));
        Assert.Equal("Wykonano 3 razy", RecurrenceText.DescribeTally(new RepeatTally(3, 0), Strings));
    }
}
