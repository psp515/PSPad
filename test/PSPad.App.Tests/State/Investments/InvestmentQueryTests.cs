using PSPad.App.State.Investments;
using PSPad.Module.Investments.Reading;
using PSPad.TestInfrastructure;
using static PSPad.App.Tests.State.Investments.Investing;

namespace PSPad.App.Tests.State.Investments;

[UnitTest]
public class InvestmentQueryTests
{
    static readonly DateOnly Today = new(2026, 10, 30);
    static readonly DateOnly First = new(2026, 9, 1);
    static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    static readonly TimeZoneInfo Warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    readonly InMemoryReplica _replica = new();
    readonly StubInvestmentQuotes _quotes = new();

    InvestmentQuery Query() => new(_replica, _quotes);

    async Task<(Guid Pln, Guid Usd)> TwoAccountsAsync()
    {
        var ike = AnAccount("IKE", "PLN");
        var usd = AnAccount("USD", "USD");
        await _replica.SaveAsync(ike);
        await _replica.SaveAsync(usd);
        await _replica.SaveAsync(AnInstrument("GHI.PL", "Ghi SA"));
        await _replica.SaveAsync(AnInstrument("XYZ.US", "Xyz Inc"));
        await _replica.SaveAsync(Deposit(ike, 1_000m, First));
        await _replica.SaveAsync(Buy(ike, "GHI.PL", 4m, 150m, 600m, First.AddDays(1)));
        await _replica.SaveAsync(Deposit(usd, 500m, First.AddDays(3)));
        await _replica.SaveAsync(Buy(usd, "XYZ.US", 2m, 100m, 200m, First.AddDays(4)));
        _quotes.PricesByTicker["GHI.PL"] = new PriceHistory("GHI.WA", "PLN", [new DailyClose(Today.AddDays(-1), 200m)]);
        _quotes.PricesByTicker["XYZ.US"] = new PriceHistory("XYZ", "USD", [new DailyClose(Today.AddDays(-1), 150m)]);
        _quotes.Rates["USD"] = [new DailyRate(First, 4m)];
        return (ike.Id, usd.Id);
    }

    [Fact]
    public async Task EachAccountIsValuedInItsCurrencyAndThePortfolioInPln()
    {
        var (pln, usd) = await TwoAccountsAsync();

        var overview = await Query().LoadAsync(Owner, Today, Utc, true, false, TestContext.Current.CancellationToken);

        Assert.Equal(1_200m, overview.Figures[pln].Value);
        Assert.Equal(200m, overview.Figures[pln].TotalProfit);
        Assert.Equal(600m, overview.Figures[usd].Value);
        Assert.Equal(1_200m + 600m * 4m, overview.Portfolio.ValuePln);
        Assert.False(overview.Portfolio.MissingRates);
    }

    [Fact]
    public async Task PricesAreAskedFromTheFirstOperationDayUnderEachInstrumentsSymbol()
    {
        await TwoAccountsAsync();

        await Query().LoadAsync(Owner, Today, Utc, true, false, TestContext.Current.CancellationToken);

        Assert.Equal(First, _quotes.PricesFrom);
        Assert.Equal([("GHI.PL", "GHI.WA"), ("XYZ.US", "XYZ")], _quotes.InstrumentsAsked.Order());
    }

    [Fact]
    public async Task RatesCoverTheAccountCurrenciesAndThePriceCurrencies()
    {
        await TwoAccountsAsync();
        _quotes.PricesByTicker["GHI.PL"] = new PriceHistory("GHI.WA", "EUR", [new DailyClose(Today.AddDays(-1), 50m)]);

        await Query().LoadAsync(Owner, Today, Utc, true, false, TestContext.Current.CancellationToken);

        Assert.Contains("USD", _quotes.CurrenciesAsked);
        Assert.Contains("EUR", _quotes.CurrenciesAsked);
    }

    [Fact]
    public async Task ValueOverTimeRunsFromTheFirstOperationToTodayInPln()
    {
        await TwoAccountsAsync();

        var overview = await Query().LoadAsync(Owner, Today, Utc, true, false, TestContext.Current.CancellationToken);

        Assert.Equal(First, overview.ValueInPln[0].Day);
        Assert.Equal(Today, overview.ValueInPln[^1].Day);
        Assert.Equal(overview.Portfolio.ValuePln, overview.ValueInPln[^1].Value);
    }

    [Fact]
    public async Task AnArchivedAccountIsLeftOutOfThePortfolio()
    {
        var (_, usd) = await TwoAccountsAsync();
        var archived = (await _replica.LoadAsync<PSPad.Module.Investments.Accounts.InvestmentAccount>(usd))!;
        archived.ApplyAll(PSPad.Module.Investments.Accounts.InvestmentAccount.Decide(archived,
            new PSPad.Module.Investments.Accounts.ArchiveInvestmentAccount(Guid.NewGuid(), Owner, usd), Now));
        await _replica.SaveAsync(archived);

        var overview = await Query().LoadAsync(Owner, Today, Utc, true, false, TestContext.Current.CancellationToken);

        Assert.Equal(1_200m, overview.Portfolio.ValuePln);
        Assert.Equal(600m, overview.Figures[usd].Value);
    }

    [Fact]
    public async Task WithNoAccountsThePortfolioIsEmpty()
    {
        var overview = await Query().LoadAsync(Owner, Today, Utc, true, false, TestContext.Current.CancellationToken);

        Assert.Equal(0m, overview.Portfolio.ValuePln);
        Assert.Empty(overview.Accounts);
    }

    [Fact]
    public async Task AnArchivedAccountGetsNoValueSeries()
    {
        var (pln, usd) = await TwoAccountsAsync();
        await ArchiveAsync(usd);

        var overview = await Query().LoadAsync(Owner, Today, Utc, true, false, TestContext.Current.CancellationToken);

        Assert.True(overview.ValueByAccount.ContainsKey(pln));
        Assert.False(overview.ValueByAccount.ContainsKey(usd));
    }

    [Fact]
    public async Task AFiguresOnlyLoadGivesProfitSharesWithoutValueSeries()
    {
        var (pln, usd) = await TwoAccountsAsync();

        var overview = await Query().FiguresAsync(Owner, Today, Utc, TestContext.Current.CancellationToken);

        Assert.Equal(0.2m, overview.ProfitShare(pln));
        Assert.Equal(0.2m, overview.ProfitShare(usd));
        Assert.Empty(overview.ValueByAccount);
        Assert.Empty(overview.ValueInPln);
    }

    [Fact]
    public async Task ALoadWithNothingChangedAsksForNoQuotesAgain()
    {
        await TwoAccountsAsync();
        var query = Query();
        await query.LoadAsync(Owner, Today, Utc, true, false, TestContext.Current.CancellationToken);
        _quotes.InstrumentsAsked.Clear();

        var again = await query.LoadAsync(Owner, Today, Utc, true, false, TestContext.Current.CancellationToken);

        Assert.Empty(_quotes.InstrumentsAsked);
        Assert.Equal(3_600m, again.Portfolio.ValuePln);
    }

    [Fact]
    public async Task ALoadAfterAChangedDocumentComputesAgain()
    {
        var (pln, _) = await TwoAccountsAsync();
        var query = Query();
        await query.LoadAsync(Owner, Today, Utc, true, false, TestContext.Current.CancellationToken);
        var account = (await _replica.LoadAsync<PSPad.Module.Investments.Accounts.InvestmentAccount>(pln))!;
        await _replica.SaveAsync(Deposit(account, 100m, Today));

        var again = await query.LoadAsync(Owner, Today, Utc, true, false, TestContext.Current.CancellationToken);

        Assert.Equal(1_300m, again.Figures[pln].Value);
    }

    [Fact]
    public async Task TheSidebarReusesTheLastFullLoad()
    {
        var (pln, _) = await TwoAccountsAsync();
        var query = Query();
        await query.LoadAsync(Owner, Today, Utc, true, false, TestContext.Current.CancellationToken);
        _quotes.InstrumentsAsked.Clear();

        var figures = await query.FiguresAsync(Owner, Today, Utc, TestContext.Current.CancellationToken);

        Assert.Empty(_quotes.InstrumentsAsked);
        Assert.Equal(0.2m, figures.ProfitShare(pln));
    }

    async Task ArchiveAsync(Guid accountId)
    {
        var account = (await _replica.LoadAsync<PSPad.Module.Investments.Accounts.InvestmentAccount>(accountId))!;
        account.ApplyAll(PSPad.Module.Investments.Accounts.InvestmentAccount.Decide(account,
            new PSPad.Module.Investments.Accounts.ArchiveInvestmentAccount(Guid.NewGuid(), Owner, accountId), Now));
        await _replica.SaveAsync(account);
    }

    [Fact]
    public async Task AReloadAfterAStaleFetchAsksForQuotesAgain()
    {
        await TwoAccountsAsync();
        _quotes.Stale.Add("XYZ");
        var query = Query();
        await query.LoadAsync(Owner, Today, Utc, true, false, TestContext.Current.CancellationToken);
        _quotes.InstrumentsAsked.Clear();

        await query.LoadAsync(Owner, Today, Utc, true, false, TestContext.Current.CancellationToken);

        Assert.NotEmpty(_quotes.InstrumentsAsked);
    }

    [Fact]
    public async Task ANewlySavedSeriesInvalidatesTheLastResult()
    {
        await TwoAccountsAsync();
        var query = Query();
        await query.LoadAsync(Owner, Today, Utc, true, false, TestContext.Current.CancellationToken);
        _quotes.Generation++;
        _quotes.PricesByTicker["GHI.PL"] = new PriceHistory("GHI.WA", "PLN", [new DailyClose(Today, 250m)]);

        var again = await query.LoadAsync(Owner, Today, Utc, true, false, TestContext.Current.CancellationToken);

        Assert.Equal(1_400m, again.Figures.Values.Single(figures => figures.Currency == "PLN").Value);
    }

    [Fact]
    public async Task ANewQuoteDayInvalidatesTheLastResult()
    {
        await TwoAccountsAsync();
        var query = Query();
        await query.LoadAsync(Owner, Today, Utc, true, false, TestContext.Current.CancellationToken);
        _quotes.InstrumentsAsked.Clear();
        _quotes.QuoteDay = _quotes.QuoteDay.AddDays(1);

        await query.LoadAsync(Owner, Today, Utc, true, false, TestContext.Current.CancellationToken);

        Assert.NotEmpty(_quotes.InstrumentsAsked);
    }

    [Fact]
    public async Task AnArchivedAccountShownAloneGetsItsValueSeries()
    {
        var (_, usd) = await TwoAccountsAsync();
        await ArchiveAsync(usd);
        var overview = await Query().LoadAsync(Owner, Today, Utc, true, false, TestContext.Current.CancellationToken);

        var series = overview.ValueOf(usd);

        Assert.Equal(600m, series[^1].Value);
    }

    [Fact]
    public async Task ADepositAfterMidnightInTheUsersZoneStartsTheSeriesOnThatDay()
    {
        var account = AnAccount("IKE", "PLN");
        await _replica.SaveAsync(account);
        await _replica.SaveAsync(Deposit(account, 500m, Today.AddDays(-1), at: new TimeOnly(23, 30)));

        var overview = await Query().LoadAsync(Owner, Today, Warsaw, true, false, TestContext.Current.CancellationToken);

        Assert.Equal([new ValuePoint(Today, 500m, 500m)], overview.ValueInPln);
        Assert.Equal([new ValuePoint(Today, 500m, 500m)], overview.ValueOf(account.Id));
    }

    [Fact]
    public async Task ARateFromTheWeekBeforeTheFirstOperationValuesAForeignAccountFromItsFirstDay()
    {
        var account = AnAccount("USD", "USD");
        await _replica.SaveAsync(account);
        await _replica.SaveAsync(Deposit(account, 100m, First));
        _quotes.Rates["USD"] = [new DailyRate(First.AddDays(-4), 4m)];

        var overview = await Query().LoadAsync(Owner, Today, Utc, true, false, TestContext.Current.CancellationToken);

        Assert.Equal(new ValuePoint(First, 400m, 400m), overview.ValueInPln[0]);
    }
}
