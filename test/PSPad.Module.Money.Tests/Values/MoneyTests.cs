using System.Text.Json;
using PSPad.Abstractions;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Money.Tests.Values;

[UnitTest]
public class MoneyTests
{
    static readonly DateOnly Day = new(2026, 10, 9);

    [Fact]
    public void InPlnRoundsHalfAwayFromZero() =>
        Assert.Equal(4.13m, new Money(1m, "EUR", 4.125m, Day).InPln);

    [Fact]
    public void InPlnOfAPlnAmountIsTheAmount() =>
        Assert.Equal(12.34m, new Money(12.34m, "PLN", 1m, Day).InPln);

    [Fact]
    public void PlnIsForcedToRateOneOnItsOwnDate()
    {
        var money = Money.Require(new Money(5m, "pln", 4.2m, new DateOnly(2020, 1, 1)), Day);

        Assert.Equal(new Money(5m, "PLN", 1m, Day), money);
    }

    [Fact]
    public void AForeignAmountKeepsItsRateAndRateDate()
    {
        var money = Money.Require(new Money(10m, " eur ", 4.25m, new DateOnly(2026, 10, 2)), Day);

        Assert.Equal(new Money(10m, "EUR", 4.25m, new DateOnly(2026, 10, 2)), money);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AnAmountMustBePositive(int amount) =>
        Assert.Equal("An amount has to be more than zero.",
            Assert.Throws<DomainRejectedException>(() => Money.Require(new Money(amount, "PLN", 1m, Day), Day)).Message);

    [Fact]
    public void AnAmountAtTheCapIsAccepted() =>
        Assert.Equal(Money.MaxAmount, Money.Require(new Money(Money.MaxAmount, "PLN", 1m, Day), Day).Amount);

    [Fact]
    public void AnAmountAboveTheCapIsRejected() =>
        Assert.Equal("An amount can be at most 1 000 000 000.",
            Assert.Throws<DomainRejectedException>(() => Money.Require(new Money(Money.MaxAmount + 0.01m, "PLN", 1m, Day), Day)).Message);

    [Fact]
    public void ARateAtTheCapIsAccepted() =>
        Assert.Equal(Money.MaxRate, Money.Require(new Money(10m, "EUR", Money.MaxRate, Day), Day).RateToPln);

    [Fact]
    public void ARateAboveTheCapIsRejected() =>
        Assert.Equal("A rate can be at most 10 000.",
            Assert.Throws<DomainRejectedException>(() => Money.Require(new Money(10m, "EUR", Money.MaxRate + 0.01m, Day), Day)).Message);

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void AForeignRateMustBePositive(int rate) =>
        Assert.Equal("A rate to PLN has to be more than zero.",
            Assert.Throws<DomainRejectedException>(() => Money.Require(new Money(10m, "EUR", rate, Day), Day)).Message);

    [Fact]
    public void AnUnknownCurrencyIsRejected() =>
        Assert.Equal("XXX is not a currency PSPad knows.",
            Assert.Throws<DomainRejectedException>(() => Money.Require(new Money(10m, "xxx", 1m, Day), Day)).Message);

    [Fact]
    public void InPlnIsNeverSerialised()
    {
        var money = new Money(10m, "EUR", 4.25m, Day);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var json = JsonSerializer.Serialize(money, options);

        Assert.DoesNotContain("inPln", json);
        Assert.Equal(money, JsonSerializer.Deserialize<Money>(json, options));
    }
}
