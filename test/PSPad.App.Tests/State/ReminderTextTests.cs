using PSPad.App.Resources;
using PSPad.App.State;
using PSPad.App.Tests.Localization;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
[Collection("Culture")]
public class ReminderTextTests : IDisposable
{
    static readonly Microsoft.Extensions.Localization.IStringLocalizer<TasksStrings> Strings = TestLocalizers.For<TasksStrings>();

    readonly CultureScope _culture = new("en-GB");

    public void Dispose() => _culture.Dispose();

    [Fact]
    public void NoReminderReadsOff() => Assert.Equal("Off", ReminderText.Describe(null, Strings));

    [Theory]
    [InlineData(0, 9, 0, "On the day at 09:00")]
    [InlineData(1, 18, 0, "1 day before at 18:00")]
    [InlineData(5, 7, 5, "5 days before at 07:05")]
    [InlineData(7, 9, 0, "7 days before at 09:00")]
    public void ItReadsTheDaysAndTime(int days, int hour, int minute, string expected) =>
        Assert.Equal(expected, ReminderText.Describe(TaskReminder.Of(days, new TimeOnly(hour, minute)), Strings));

    [Theory]
    [InlineData(2, "2 dni wcześniej o 09:00")]
    [InlineData(1, "1 dzień wcześniej o 09:00")]
    [InlineData(0, "W dniu terminu o 09:00")]
    public void ItAgreesWithTheCountInPolish(int days, string expected)
    {
        CultureScope.Use("pl-PL");

        Assert.Equal(expected, ReminderText.Describe(TaskReminder.Of(days, new TimeOnly(9, 0)), Strings));
    }

    [Fact]
    public void TheQuickChoicesAreTheDayOfOneTwoAndSevenDaysBefore() =>
        Assert.Equal([0, 1, 2, 7], ReminderText.QuickDays);

    [Theory]
    [InlineData(0, "On the day")]
    [InlineData(1, "1 day")]
    [InlineData(2, "2 days")]
    [InlineData(7, "1 week")]
    public void AQuickChoiceReadsShort(int days, string expected) =>
        Assert.Equal(expected, ReminderText.Quick(days, Strings));

    [Theory]
    [InlineData(1, "1 dzień")]
    [InlineData(2, "2 dni")]
    [InlineData(7, "1 tydzień")]
    public void AQuickChoiceReadsShortInPolish(int days, string expected)
    {
        CultureScope.Use("pl-PL");

        Assert.Equal(expected, ReminderText.Quick(days, Strings));
    }

    [Fact]
    public void ChoosingDaysWithNoReminderStartsAtNine() =>
        Assert.Equal(new TaskReminder(2, new TimeOnly(9, 0)), ReminderText.WithDays(null, 2, null));

    [Fact]
    public void ChoosingDaysWithNoReminderStartsAtTheTasksTime() =>
        Assert.Equal(new TaskReminder(1, new TimeOnly(14, 30)),
            ReminderText.WithDays(null, 1, TaskTime.Of(new TimeOnly(14, 30), null)));

    [Fact]
    public void ChoosingDaysKeepsTheReminderTime() =>
        Assert.Equal(new TaskReminder(7, new TimeOnly(18, 0)),
            ReminderText.WithDays(new TaskReminder(1, new TimeOnly(18, 0)), 7, TaskTime.Of(new TimeOnly(9, 0), null)));

    [Fact]
    public void ChoosingATimeKeepsTheDays() =>
        Assert.Equal(new TaskReminder(3, new TimeOnly(7, 15)),
            ReminderText.WithAt(new TaskReminder(3, new TimeOnly(9, 0)), new TimeOnly(7, 15)));

    [Theory]
    [InlineData("0", 0)]
    [InlineData(" 12 ", 12)]
    [InlineData("99", 99)]
    public void TypedDaysAreRead(string text, int expected)
    {
        Assert.True(ReminderText.TryParseDays(text, out var days));
        Assert.Equal(expected, days);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("100")]
    [InlineData("two")]
    [InlineData("1.5")]
    public void TypedDaysOutsideTheRangeAreRefused(string text) =>
        Assert.False(ReminderText.TryParseDays(text, out _));
}
