using PSPad.App.State;
using PSPad.Contracts;
using PSPad.Module.Money.Import;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class ImportRatesTests
{
    static readonly RateRequest Eur = new("EUR", new DateOnly(2026, 9, 5));
    static readonly RateRequest Usd = new("USD", new DateOnly(2026, 9, 5));
    static readonly DateOnly Today = new(2026, 10, 14);

    [Fact]
    public async Task AnsweredRatesBecomeKnownAndMissingOnesFail()
    {
        var nbp = new AppTestHost.FakeNbpRates();
        nbp.ByCurrency["EUR"] = new NbpRateView("EUR", 4.25m, new DateOnly(2026, 9, 4));
        nbp.ByCurrency["USD"] = null;

        var book = await ImportRates.FetchAsync(nbp, true, Today, [Eur, Usd], RateBook.Empty);

        Assert.Equal(new ImportRate(4.25m, new DateOnly(2026, 9, 4)), book.Known[Eur]);
        Assert.Equal("No NBP rate for USD on 2026-09-05.", book.Failed[Usd]);
    }

    [Fact]
    public async Task OfflineEveryRequestFailsWithoutAsking()
    {
        var nbp = new AppTestHost.FakeNbpRates();

        var book = await ImportRates.FetchAsync(nbp, false, Today, [Eur], RateBook.Empty);

        Assert.Equal(ImportRates.OfflineMessage, book.Failed[Eur]);
        Assert.Empty(nbp.Asked);
    }

    [Fact]
    public async Task RetryingReplacesAnEarlierFailure()
    {
        var nbp = new AppTestHost.FakeNbpRates { Next = new NbpRateView("EUR", 4.3m, Eur.Date) };
        var failed = new RateBook(new Dictionary<RateRequest, ImportRate>(),
            new Dictionary<RateRequest, string> { [Eur] = ImportRates.OfflineMessage });

        var book = await ImportRates.FetchAsync(nbp, true, Today, [Eur], failed);

        Assert.Empty(book.Failed);
        Assert.Equal(4.3m, book.Known[Eur].Rate);
    }

    [Fact]
    public async Task AFutureDateFailsWithoutAskingAndSaysTheRateIsNotPublishedYet()
    {
        var nbp = new AppTestHost.FakeNbpRates();
        var future = new RateRequest("USD", new DateOnly(2026, 10, 31));

        var book = await ImportRates.FetchAsync(nbp, true, Today, [future], RateBook.Empty);

        Assert.Equal("2026-10-31 is in the future, so NBP has no USD rate for it yet. Fill the rate column or change the date.",
            book.Failed[future]);
        Assert.Empty(nbp.Asked);
    }
}
