using PSPad.Abstractions;
using PSPad.Module.Money.Values;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Money.Tests.Values;

[UnitTest]
public class CurrenciesTests
{
    [Fact]
    public void PlnComesFirst() => Assert.Equal("PLN", Currencies.All[0]);

    [Theory]
    [InlineData("eur", "EUR")]
    [InlineData(" usd ", "USD")]
    [InlineData("PLN", "PLN")]
    public void AKnownCodeIsNormalised(string input, string expected) =>
        Assert.Equal(expected, Currencies.Require(input));

    [Theory]
    [InlineData("XXX")]
    [InlineData("")]
    [InlineData("EURO")]
    public void AnUnknownCodeIsRejected(string input)
    {
        var rejection = Assert.Throws<DomainRejectedException>(() => Currencies.Require(input));
        Assert.Equal($"{input.Trim().ToUpperInvariant()} is not a currency PSPad knows.", rejection.Message);
    }

    [Fact]
    public void EveryCodeIsThreeUpperCaseLetters() =>
        Assert.All(Currencies.All, code => Assert.Matches("^[A-Z]{3}$", code));
}
