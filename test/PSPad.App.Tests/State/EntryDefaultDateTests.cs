using PSPad.App.State;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class EntryDefaultDateTests
{
    static readonly DateOnly Today = new(2026, 10, 9);

    [Fact]
    public void WithoutAShownMonthItIsToday() =>
        Assert.Equal(Today, EntryDefaultDate.For(null, Today));

    [Fact]
    public void InAPastMonthItIsItsLastDay() =>
        Assert.Equal(new DateOnly(2026, 9, 30), EntryDefaultDate.For(new YearMonth(2026, 9), Today));

    [Fact]
    public void InAFutureMonthItIsItsFirstDay() =>
        Assert.Equal(new DateOnly(2026, 11, 1), EntryDefaultDate.For(new YearMonth(2026, 11), Today));
}
