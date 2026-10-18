using System.Globalization;
using PSPad.App.Components;
using PSPad.App.Resources;
using PSPad.App.Tests.Localization;
using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
[Collection("Culture")]
public class RecurrenceLocalizationTests
{
    static readonly DateOnly Start = new(2026, 9, 24);

    [Fact]
    public void TheUnitLabelsAgreeWithTheAmountInPolish()
    {
        var strings = TestLocalizers.For<TasksStrings>();
        var before = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pl-PL");

            Assert.Equal("dzień", RecurrenceEditor.UnitLabel(RecurrenceKind.Daily, 1, strings));
            Assert.Equal("tygodnie", RecurrenceEditor.UnitLabel(RecurrenceKind.Weekly, 2, strings));
            Assert.Equal("tygodni", RecurrenceEditor.UnitLabel(RecurrenceKind.Weekly, 5, strings));
            Assert.Equal("lata", RecurrenceEditor.UnitLabel(RecurrenceKind.Yearly, 3, strings));
            Assert.Equal("miesięcy", LeadTimeRow.UnitLabel(LeadUnit.Months, 6, strings));
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    [Fact]
    public void TheUnitLabelsAreSingularForOneInEnglish()
    {
        var strings = TestLocalizers.For<TasksStrings>();

        Assert.Equal("week", RecurrenceEditor.UnitLabel(RecurrenceKind.Weekly, 1, strings));
        Assert.Equal("weeks", RecurrenceEditor.UnitLabel(RecurrenceKind.Weekly, 2, strings));
    }

    [Fact]
    public void IntervalsAgreeWithTheirCountInPolish()
    {
        var strings = TestLocalizers.For<TasksStrings>();
        var before = CultureInfo.CurrentUICulture;
        var beforeFormat = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("pl-PL");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pl-PL");

            Assert.Equal("Co 2 dni", RecurrenceEditor.Describe(RecurrenceRule.Daily(Start).EveryNth(2), strings));
            Assert.Equal("Co 2 tygodnie: pon., czw.", RecurrenceEditor.Describe(
                RecurrenceRule.Weekly(Start, DayOfWeek.Thursday, DayOfWeek.Monday).EveryNth(2), strings));
            Assert.Equal("Co 5 miesięcy: 15.",
                RecurrenceEditor.Describe(RecurrenceRule.MonthlyOnDay(Start, 15).EveryNth(5), strings));
            Assert.Equal("Wykonano 1 raz · 1 z rzędu", RecurrenceEditor.DescribeTally(new RepeatTally(1, 1), strings));
            Assert.Equal("Wykonano 3 razy", RecurrenceEditor.DescribeTally(new RepeatTally(3, 0), strings));
            Assert.Equal("brak otwartych zadań", OpenTaskCount.Describe(0, strings));
            Assert.Equal("22 otwarte zadania", OpenTaskCount.Describe(22, strings));
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
            CultureInfo.CurrentCulture = beforeFormat;
        }
    }
}
