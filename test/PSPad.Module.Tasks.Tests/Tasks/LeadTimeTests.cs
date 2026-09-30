using PSPad.Abstractions;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Tasks;

[UnitTest]
public class LeadTimeTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void AnAmountOutsideOneToNinetyNineIsRejected(int amount)
    {
        var rejection = Assert.Throws<DomainRejectedException>(() => LeadTime.Of(amount, LeadUnit.Days));
        Assert.Equal("A lead time must be between 1 and 99.", rejection.Message);
    }

    [Fact]
    public void AnUnknownUnitIsRejected()
    {
        Assert.Throws<DomainRejectedException>(() => LeadTime.Of(1, (LeadUnit)7));
    }

    [Theory]
    [InlineData(3, LeadUnit.Days, 2026, 10, 12)]
    [InlineData(2, LeadUnit.Weeks, 2026, 10, 1)]
    [InlineData(1, LeadUnit.Months, 2026, 9, 15)]
    public void ItIsFirstShownThatFarBeforeTheDay(int amount, LeadUnit unit, int year, int month, int dayOfMonth)
    {
        Assert.Equal(new DateOnly(year, month, dayOfMonth),
            LeadTime.Of(amount, unit).FirstShownFor(new DateOnly(2026, 10, 15)));
    }

    [Fact]
    public void AMonthBeforeTheThirtyFirstClampsToTheMonthsEnd()
    {
        var month = LeadTime.Of(1, LeadUnit.Months);

        Assert.Equal(new DateOnly(2027, 2, 28), month.FirstShownFor(new DateOnly(2027, 3, 31)));
        Assert.Equal(new DateOnly(2028, 2, 29), month.FirstShownFor(new DateOnly(2028, 3, 31)));
    }

    [Fact]
    public void ItShowsOnlyFutureDaysInsideItsWindow()
    {
        var week = LeadTime.Of(1, LeadUnit.Weeks);
        var today = new DateOnly(2026, 9, 12);

        Assert.False(week.Shows(today, today));
        Assert.True(week.Shows(today.AddDays(1), today));
        Assert.True(week.Shows(today.AddDays(7), today));
        Assert.False(week.Shows(today.AddDays(8), today));
    }

    [Fact]
    public void ItsReachCoversEveryDayItShows()
    {
        var today = new DateOnly(2027, 2, 28);
        var month = LeadTime.Of(1, LeadUnit.Months);

        Assert.True(month.Shows(new DateOnly(2027, 3, 31), today));
        Assert.True(month.ReachFrom(today) >= new DateOnly(2027, 3, 31));
    }
}
