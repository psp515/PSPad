using PSPad.Abstractions;
using PSPad.Module.Investments.Instruments;
using PSPad.TestInfrastructure;
using static PSPad.Module.Investments.Tests.Given;

namespace PSPad.Module.Investments.Tests.Instruments;

[UnitTest]
public class InstrumentTests
{
    [Fact]
    public void EnsuringANewTickerCreatesItWithTheDefaultSymbol()
    {
        var instrument = AnInstrument(" def.pl ");

        Assert.Equal(Instrument.IdFor(Owner, "DEF.PL"), instrument.Id);
        Assert.Equal(Owner, instrument.UserId);
        Assert.Equal("DEF.PL", instrument.Ticker);
        Assert.Equal("Abc Corp", instrument.Name);
        Assert.Equal("DEF.WA", instrument.QuoteSymbol);
        Assert.Equal(Now, instrument.UpdatedAt);
    }

    [Fact]
    public void EnsuringAKnownTickerChangesNothing() =>
        Assert.Empty(Instrument.Decide(AnInstrument(), new EnsureInstrument(Guid.NewGuid(), Owner, "ABC.US", "Other"), Now));

    [Fact]
    public void TheIdIgnoresTickerCase() =>
        Assert.Equal(Instrument.IdFor(Owner, "abc.us"), Instrument.IdFor(Owner, "ABC.US"));

    [Fact]
    public void EveryUserHasTheirOwnInstrumentId() =>
        Assert.NotEqual(Instrument.IdFor(Owner, "ABC.US"), Instrument.IdFor(Stranger, "ABC.US"));

    [Fact]
    public void DifferentTickersHaveDifferentIds() =>
        Assert.NotEqual(Instrument.IdFor(Owner, "ABC.US"), Instrument.IdFor(Owner, "DEF.PL"));

    [Fact]
    public void ABlankNameFallsBackToTheTicker() =>
        Assert.Equal("ABC.US", Assert.IsType<InstrumentEnsured>(Assert.Single(
            Instrument.Decide(null, new EnsureInstrument(Guid.NewGuid(), Owner, "ABC.US", " "), Now))).Name);

    [Theory]
    [InlineData("")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXY")]
    public void ATickerIsOneTo24Characters(string ticker) =>
        Assert.Throws<DomainRejectedException>(() =>
            Instrument.Decide(null, new EnsureInstrument(Guid.NewGuid(), Owner, ticker, "Name"), Now));

    [Fact]
    public void ANameOver64CharactersIsRejected() =>
        Assert.Throws<DomainRejectedException>(() =>
            Instrument.Decide(null, new EnsureInstrument(Guid.NewGuid(), Owner, "ABC.US", new string('n', 65)), Now));

    [Fact]
    public void SettingASymbolChangesIt()
    {
        var instrument = AnInstrument();

        instrument.ApplyAll(Instrument.Decide(instrument, new SetQuoteSymbol(Guid.NewGuid(), Owner, "ABC.US", " abc-b.co "), Now));

        Assert.Equal("ABC-B.CO", instrument.QuoteSymbol);
    }

    [Fact]
    public void SettingTheSameSymbolChangesNothing() =>
        Assert.Empty(Instrument.Decide(AnInstrument(), new SetQuoteSymbol(Guid.NewGuid(), Owner, "ABC.US", "ABC"), Now));

    [Theory]
    [InlineData("")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXY")]
    public void ASymbolIsOneTo24Characters(string symbol) =>
        Assert.Throws<DomainRejectedException>(() =>
            Instrument.Decide(AnInstrument(), new SetQuoteSymbol(Guid.NewGuid(), Owner, "ABC.US", symbol), Now));

    [Fact]
    public void AMissingInstrumentHasNoSymbolToSet()
    {
        var rejection = Assert.Throws<DomainRejectedException>(() =>
            Instrument.Decide(null, new SetQuoteSymbol(Guid.NewGuid(), Owner, "ABC.US", "ABC"), Now));

        Assert.Equal("That instrument does not exist.", rejection.Message);
        Assert.True(rejection.Unrecoverable);
    }
}
