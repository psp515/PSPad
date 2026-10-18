using PSPad.App.Components;
using PSPad.App.Resources;
using PSPad.App.Tests.Localization;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class DueDateRowTests
{
    static readonly DateOnly Thursday = new(2026, 9, 24);

    [Fact]
    public void QuickPicksOfferTodayTomorrowInTwoDaysAndNextMonday()
    {
        var picks = DueDateRow.QuickPicks(Thursday, TestLocalizers.For<SharedStrings>(), TestLocalizers.For<TasksStrings>());

        Assert.Equal(["Today", "Tomorrow", "In 2 days", "Next week"], picks.Select(pick => pick.Label));
        Assert.Equal(
            [Thursday, Thursday.AddDays(1), Thursday.AddDays(2), new DateOnly(2026, 9, 28)],
            picks.Select(pick => pick.Day));
    }

    [Fact]
    public void OnAMondayNextWeekIsTheFollowingMonday()
    {
        var monday = new DateOnly(2026, 9, 28);

        Assert.Equal(monday.AddDays(7), DueDateRow.QuickPicks(monday, TestLocalizers.For<SharedStrings>(), TestLocalizers.For<TasksStrings>())[3].Day);
    }

    [Theory]
    [InlineData(0, "Today")]
    [InlineData(1, "Tomorrow")]
    [InlineData(-1, "Yesterday")]
    [InlineData(5, "Tue, 29 Sep")]
    [InlineData(120, "Fri, 22 Jan 2027")]
    public void ItDescribesTheDueDayRelativeToToday(int offset, string expected)
    {
        Assert.Equal(expected, DueDateRow.Describe(Thursday.AddDays(offset), Thursday, TestLocalizers.For<SharedStrings>()));
    }
}
