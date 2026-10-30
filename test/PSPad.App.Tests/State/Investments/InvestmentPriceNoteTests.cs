using PSPad.App.State.Investments;
using PSPad.Module.Investments.Reading;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State.Investments;

[UnitTest]
public class InvestmentPriceNoteTests
{
    static readonly DateTimeOffset Fetched = new(2026, 10, 30, 16, 5, 0, TimeSpan.Zero);

    static AccountValueOnDay Value(Dictionary<string, DateOnly?> days, bool noPrice = false) =>
        new(Guid.NewGuid(), 10m, "PLN", days, false, noPrice);

    static PriceNote Note(AccountValueOnDay value, Func<string, bool>? stale = null, Func<string, DateTimeOffset?>? fetched = null) =>
        InvestmentPriceNote.For(value, ticker => ticker + ".S", stale ?? (_ => false), fetched ?? (_ => Fetched));

    [Fact]
    public void AnAccountHoldingNothingHasNoNote() =>
        Assert.Equal(PriceNoteKind.None, Note(Value([])).Kind);

    [Fact]
    public void AnInstrumentNeverPricedSaysSo() =>
        Assert.Equal(PriceNoteKind.NoPrice, Note(Value(new() { ["A"] = null }, noPrice: true)).Kind);

    [Fact]
    public void AFailedRefreshShowsTheOldestStoredPriceDay()
    {
        var value = Value(new() { ["A"] = new DateOnly(2026, 10, 27), ["B"] = new DateOnly(2026, 10, 24) });

        var note = Note(value, stale: symbol => symbol == "A.S");

        Assert.Equal(PriceNoteKind.Stale, note.Kind);
        Assert.Equal(new DateOnly(2026, 10, 24), note.Day);
    }

    [Fact]
    public void FreshPricesShowTheEarliestFetchTime()
    {
        var value = Value(new() { ["A"] = new DateOnly(2026, 10, 30), ["B"] = new DateOnly(2026, 10, 30) });

        var note = Note(value, fetched: symbol => symbol == "A.S" ? Fetched : Fetched.AddHours(-3));

        Assert.Equal(PriceNoteKind.Fetched, note.Kind);
        Assert.Equal(Fetched.AddHours(-3), note.At);
    }

    [Fact]
    public void WithoutAFetchTimeTheLatestPriceDayIsShown()
    {
        var value = Value(new() { ["A"] = new DateOnly(2026, 10, 27), ["B"] = new DateOnly(2026, 10, 28) });

        var note = Note(value, fetched: _ => null);

        Assert.Equal(PriceNoteKind.Day, note.Kind);
        Assert.Equal(new DateOnly(2026, 10, 28), note.Day);
    }
}
