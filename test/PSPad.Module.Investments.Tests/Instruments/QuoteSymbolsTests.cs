using PSPad.Module.Investments.Instruments;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Investments.Tests.Instruments;

[UnitTest]
public class QuoteSymbolsTests
{
    [Theory]
    [InlineData("DEF.PL", "DEF.WA")]
    [InlineData("ABC.US", "ABC")]
    [InlineData("FUND.UK", "FUND.L")]
    [InlineData("sft.de", "SFT.DE")]
    [InlineData("AVI.FR", "AVI.PA")]
    [InlineData("CHIP.NL", "CHIP.AS")]
    [InlineData("ABC.XX", "ABC.XX")]
    [InlineData("PLAIN", "PLAIN")]
    public void DefaultSymbolsFollowYahoo(string ticker, string symbol) => Assert.Equal(symbol, QuoteSymbols.DefaultFor(ticker));
}
