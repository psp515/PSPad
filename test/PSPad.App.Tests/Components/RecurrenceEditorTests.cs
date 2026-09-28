using PSPad.App.Components;
using PSPad.Module.Tasks.Recurrence;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class RecurrenceEditorTests
{
    static readonly DateOnly Start = new(2026, 9, 24);

    [Fact]
    public void ItDescribesEachRuleInWords()
    {
        Assert.Equal("Never", RecurrenceEditor.Describe(null));
        Assert.Equal("Daily", RecurrenceEditor.Describe(RecurrenceRule.Daily(Start)));
        Assert.Equal("Weekdays", RecurrenceEditor.Describe(RecurrenceRule.Weekly(Start,
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday)));
        Assert.Equal("Weekly on Mon, Sun", RecurrenceEditor.Describe(
            RecurrenceRule.Weekly(Start, DayOfWeek.Sunday, DayOfWeek.Monday)));
        Assert.Equal("Monthly on day 24", RecurrenceEditor.Describe(RecurrenceRule.MonthlyOnDay(Start, 24)));
    }

    [Fact]
    public void AnIntervalAboveOneIsSpelledOut()
    {
        Assert.Equal("Every 2 days", RecurrenceEditor.Describe(RecurrenceRule.Daily(Start).EveryNth(2)));
        Assert.Equal("Every 3 weeks on Mon, Thu", RecurrenceEditor.Describe(
            RecurrenceRule.Weekly(Start, DayOfWeek.Thursday, DayOfWeek.Monday).EveryNth(3)));
        Assert.Equal("Every 2 months on day 15",
            RecurrenceEditor.Describe(RecurrenceRule.MonthlyOnDay(Start, 15).EveryNth(2)));
    }

    [Theory]
    [InlineData(0, 0, "Not done yet")]
    [InlineData(1, 0, "Done 1 time")]
    [InlineData(4, 0, "Done 4 times")]
    [InlineData(7, 3, "Done 7 times · 3 in a row")]
    public void TheTallyReadsAsDoneCountAndStreak(int total, int streak, string expected)
    {
        Assert.Equal(expected, RecurrenceEditor.DescribeTally(new RepeatTally(total, streak)));
    }
}
