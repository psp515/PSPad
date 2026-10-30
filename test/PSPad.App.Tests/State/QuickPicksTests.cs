using PSPad.App.Resources;
using PSPad.App.State;
using PSPad.App.Tests.Localization;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
[Collection("Culture")]
public class QuickPicksTests : IDisposable
{
    static readonly DateOnly Thursday = new(2026, 9, 24);

    readonly CultureScope _culture = new("en-GB");

    public void Dispose() => _culture.Dispose();

    [Fact]
    public void QuickPicksOfferTodayTomorrowInTwoDaysAndNextMonday()
    {
        var picks = Picks(Thursday);

        Assert.Equal(["Today", "Tomorrow", "In 2 days", "Next week"], picks.Select(pick => pick.Label));
        Assert.Equal(
            [Thursday, Thursday.AddDays(1), Thursday.AddDays(2), new DateOnly(2026, 9, 28)],
            picks.Select(pick => pick.Day));
    }

    [Fact]
    public void OnAMondayNextWeekIsTheFollowingMonday()
    {
        var monday = new DateOnly(2026, 9, 28);

        Assert.Equal(monday.AddDays(7), Picks(monday)[3].Day);
    }

    [Fact]
    public void NextWeekFollowsTheCulturesFirstDayOfTheWeek()
    {
        CultureScope.Use("en-US");

        Assert.Equal(new DateOnly(2026, 9, 27), Picks(Thursday)[3].Day);
    }

    static IReadOnlyList<QuickPick> Picks(DateOnly today) =>
        QuickPicks.For(today, TestLocalizers.For<SharedStrings>(), TestLocalizers.For<TasksStrings>());
}
