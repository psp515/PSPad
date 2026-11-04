using PSPad.Module.Investments.Operations;
using PSPad.TestInfrastructure;
using MoneyValue = PSPad.Module.Money.Money;

namespace PSPad.Module.Investments.Tests.Operations;

[UnitTest]
public class ConversionCounterTests
{
    static readonly DateOnly Day = new(2026, 9, 14);

    [Fact]
    public void APlnCounterAmountHasRateOne()
    {
        var counter = ConversionCounter.Of(new MoneyValue(250m, "USD", 3.9m, Day), 940m, "PLN", Day);

        Assert.Equal(new MoneyValue(940m, "PLN", 1m, Day), counter);
    }

    [Fact]
    public void AForeignCounterAmountTakesTheRateTheConversionImplies()
    {
        var counter = ConversionCounter.Of(new MoneyValue(1000m, "PLN", 1m, Day), 270m, "USD", Day);

        Assert.Equal(new MoneyValue(270m, "USD", 3.703704m, Day), counter);
    }
}
