using PSPad.Module.Investments.Reading;
using PSPad.TestInfrastructure;
using static PSPad.Module.Investments.Tests.Given;
using static PSPad.Module.Investments.Tests.Reading.Market;

namespace PSPad.Module.Investments.Tests.Reading;

[UnitTest]
public class AccountValueOnDayTests
{
    [Fact]
    public void ReportsTheValueAndTheDayEachPriceIsFrom()
    {
        var account = AnAccount();
        var operations = new[] {
            Deposit(account, 1000m, Day(1)),
            Buy(account, "DEF.PL", position: "A", quantity: 10m, price: 50m, amount: 500m, Day(2)),
            Buy(account, "ABC.US", position: "B", quantity: 1m, price: 100m, amount: 100m, Day(2)),
            Deposit(account, 700m, Day(25)),
        };

        var value = AccountValueOnDay.For(account, operations, History("DEF.PL", "PLN", (Day(18), 60m)), PlnOnly, Day(20), Utc);

        Assert.Equal(account.Id, value.AccountId);
        Assert.Equal("PLN", value.Currency);
        Assert.Equal(1100m, value.Value);
        Assert.Equal(Day(18), value.PriceDays["DEF.PL"]);
        Assert.Null(value.PriceDays["ABC.US"]);
        Assert.True(value.AnyNoPrice);
        Assert.True(value.AnyStale);
    }

    [Fact]
    public void PricesFromTheDayItselfAreNeitherStaleNorMissing()
    {
        var account = AnAccount();
        var operations = new[] { Buy(account, "DEF.PL", position: "A", quantity: 10m, price: 50m, amount: 500m, Day(2)) };

        var value = AccountValueOnDay.For(account, operations, History("DEF.PL", "PLN", (Day(20), 60m)), PlnOnly, Day(20), Utc);

        Assert.Equal(100m, value.Value);
        Assert.False(value.AnyNoPrice);
        Assert.False(value.AnyStale);
    }

    [Fact]
    public void AnOperationCountsOnTheDayOfTheUsersZone()
    {
        var account = AnAccount();
        var operations = new[] { DepositAt(account, 500m, HalfPastMidnightInWarsaw) };

        var october = AccountValueOnDay.For(account, operations, Prices(), PlnOnly, LastOfOctober, Warsaw);
        var november = AccountValueOnDay.For(account, operations, Prices(), PlnOnly, FirstOfNovember, Warsaw);

        Assert.Equal(0m, october.Value);
        Assert.Equal(500m, november.Value);
    }
}
