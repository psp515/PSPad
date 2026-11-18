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
    public void PresetsWithoutATimeAreAtNine()
    {
        var presets = ReminderText.Presets(null);

        Assert.Equal([0, 1, 2, 7], presets.Select(p => p.DaysBefore));
        Assert.All(presets, p => Assert.Equal(new TimeOnly(9, 0), p.At));
    }

    [Fact]
    public void TheSameDayPresetIsAnHourBeforeTheTask() =>
        Assert.Equal(new TimeOnly(13, 30), ReminderText.Presets(TaskTime.Of(new TimeOnly(14, 30), null))[0].At);

    [Fact]
    public void TheSameDayPresetStopsAtMidnight() =>
        Assert.Equal(new TimeOnly(0, 0), ReminderText.Presets(TaskTime.Of(new TimeOnly(0, 30), null))[0].At);
}
