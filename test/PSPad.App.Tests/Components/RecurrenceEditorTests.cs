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
}
