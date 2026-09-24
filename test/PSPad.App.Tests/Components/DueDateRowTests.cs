using PSPad.App.Components;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Components;

[UnitTest]
public class DueDateRowTests
{
    static readonly DateOnly Thursday = new(2026, 9, 24);

    [Fact]
    public void QuickPicksOfferTodayTomorrowInTwoDaysAndNextMonday()
    {
        var picks = DueDateRow.QuickPicks(Thursday);

        Assert.Equal(["Today", "Tomorrow", "In 2 days", "Next week"], picks.Select(pick => pick.Label));
        Assert.Equal(
            [Thursday, Thursday.AddDays(1), Thursday.AddDays(2), new DateOnly(2026, 9, 28)],
            picks.Select(pick => pick.Day));
    }

    [Fact]
    public void OnAMondayNextWeekIsTheFollowingMonday()
    {
        var monday = new DateOnly(2026, 9, 28);

        Assert.Equal(monday.AddDays(7), DueDateRow.QuickPicks(monday)[3].Day);
    }

    [Theory]
    [InlineData(null, "No due date")]
    [InlineData(0, "Today")]
    [InlineData(1, "Tomorrow")]
    [InlineData(-1, "Yesterday")]
    [InlineData(5, "Tue, 29 Sep")]
    [InlineData(120, "Fri, 22 Jan 2027")]
    public void ItDescribesTheDueDayRelativeToToday(int? offset, string expected)
    {
        DateOnly? day = offset is null ? null : Thursday.AddDays(offset.Value);

        Assert.Equal(expected, DueDateRow.Describe(day, Thursday));
    }
}
