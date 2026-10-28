using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor.Services;
using PSPad.App.Components;
using PSPad.App.Resources;
using PSPad.App.Tests.Localization;
using PSPad.Module.Tasks.Recurrence;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class RecurrenceEditorTests
{
    static readonly DateOnly Start = new(2026, 9, 24);
    static readonly IStringLocalizer<TasksStrings> Strings = TestLocalizers.For<TasksStrings>();

    [Fact]
    public void ItDescribesEachRuleInWords()
    {
        Assert.Equal("Never", RecurrenceEditor.Describe(null, Strings));
        Assert.Equal("Daily", RecurrenceEditor.Describe(RecurrenceRule.Daily(Start), Strings));
        Assert.Equal("Weekdays", RecurrenceEditor.Describe(RecurrenceRule.Weekly(Start,
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday), Strings));
        Assert.Equal("Weekly on Mon, Sun", RecurrenceEditor.Describe(
            RecurrenceRule.Weekly(Start, DayOfWeek.Sunday, DayOfWeek.Monday), Strings));
        Assert.Equal("Monthly on day 24", RecurrenceEditor.Describe(RecurrenceRule.MonthlyOnDay(Start, 24), Strings));
    }

    [Fact]
    public void AnIntervalAboveOneIsSpelledOut()
    {
        Assert.Equal("Every 2 days", RecurrenceEditor.Describe(RecurrenceRule.Daily(Start).EveryNth(2), Strings));
        Assert.Equal("Every 3 weeks on Mon, Thu", RecurrenceEditor.Describe(
            RecurrenceRule.Weekly(Start, DayOfWeek.Thursday, DayOfWeek.Monday).EveryNth(3), Strings));
        Assert.Equal("Every 2 months on day 15",
            RecurrenceEditor.Describe(RecurrenceRule.MonthlyOnDay(Start, 15).EveryNth(2), Strings));
    }

    [Theory]
    [InlineData(0, 0, "Not done yet")]
    [InlineData(1, 0, "Done 1 time")]
    [InlineData(4, 0, "Done 4 times")]
    [InlineData(7, 3, "Done 7 times · 3 in a row")]
    public void TheTallyReadsAsDoneCountAndStreak(int total, int streak, string expected)
    {
        Assert.Equal(expected, RecurrenceEditor.DescribeTally(new RepeatTally(total, streak), Strings));
    }

    [Fact]
    public void AYearlyRuleNamesItsDayAndMonth()
    {
        var rule = RecurrenceRule.Yearly(new DateOnly(2026, 3, 15));

        Assert.Equal("Yearly on 15 Mar", RecurrenceEditor.Describe(rule, Strings));
        Assert.Equal("Every 2 years on 15 Mar", RecurrenceEditor.Describe(rule.EveryNth(2), Strings));
    }

    [Fact]
    public async Task TheMenuOffersYearlyOnTodayAndNoNever()
    {
        await using var context = new Bunit.TestContext();
        context.JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        var menu = context.Render(builder =>
        {
            builder.OpenComponent<MudBlazor.MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<RecurrenceEditor>(1);
            builder.AddAttribute(2, nameof(RecurrenceEditor.Value), RecurrenceRule.Daily(Start));
            builder.AddAttribute(3, nameof(RecurrenceEditor.Today), Start);
            builder.CloseComponent();
        });

        menu.Find("button").Click();

        var options = menu.FindAll("[role=menuitem]").Select(item => item.TextContent.Trim()).ToArray();
        Assert.Contains("Yearly on 24 Sep", options);
        Assert.DoesNotContain("Never", options);
    }
}
