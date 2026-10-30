using PSPad.Abstractions;
using PSPad.App.State.Replica;
using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Instruments;
using PSPad.Module.Investments.Operations;
using PSPad.Module.Investments.Reading;

namespace PSPad.App.State.Investments;

public sealed class InvestmentQuery(IReplica replica, IInvestmentQuotes quotes)
{
    readonly SemaphoreSlim _oneAtATime = new(1, 1);
    (int Documents, DateOnly Today, TimeZoneInfo Zone, bool Online, int Quotes, DateOnly QuoteDay)? _latestKey;
    bool _latestStale;

    public InvestmentOverview? Latest { get; private set; }

    public event Action? Changed;

    public async Task<InvestmentOverview> LoadAsync(
        Guid userId, DateOnly today, TimeZoneInfo zone, bool online, bool refresh, CancellationToken ct)
    {
        await _oneAtATime.WaitAsync(ct);
        try
        {
            var documents = await DocumentsAsync(userId);
            var fingerprint = Fingerprint(documents);
            if (!refresh && !_latestStale && Latest is not null
                && _latestKey == (fingerprint, today, zone, online, quotes.Generation, quotes.QuoteDay))
            {
                return Latest;
            }

            Latest = await BuildAsync(documents, today, zone, online, refresh, true, ct);
            _latestKey = (fingerprint, today, zone, online, quotes.Generation, quotes.QuoteDay);
            _latestStale = Latest.Operations.Select(operation => operation.Ticker).OfType<string>().Distinct()
                .Any(ticker => quotes.IsStale(Latest.SymbolOf(ticker)));
        }
        finally
        {
            _oneAtATime.Release();
        }

        Changed?.Invoke();
        return Latest;
    }

    public async Task<InvestmentOverview> FiguresAsync(Guid userId, DateOnly today, TimeZoneInfo zone, CancellationToken ct)
    {
        var documents = await DocumentsAsync(userId);
        if (Latest is { } latest && _latestKey is { } key && key.Documents == Fingerprint(documents) && key.Today == today && key.Zone == zone
            && key.Quotes == quotes.Generation)
        {
            return latest;
        }

        return await BuildAsync(documents, today, zone, false, false, false, ct);
    }

    async Task<Documents> DocumentsAsync(Guid userId)
    {
        var accounts = (await replica.LoadAllAsync<InvestmentAccount>(userId))
            .Where(account => account.UserId == userId)
            .OrderBy(account => account.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var known = accounts.Select(account => account.Id).ToHashSet();
        var operations = (await replica.LoadAllAsync<InvestmentOperation>(userId))
            .Where(operation => known.Contains(operation.AccountId))
            .ToList();
        var instruments = (await replica.LoadAllAsync<Instrument>(userId))
            .Where(instrument => instrument.UserId == userId)
            .ToList();
        return new Documents(accounts, operations, instruments);
    }

    static int Fingerprint(Documents documents)
    {
        var hash = new HashCode();
        foreach (var document in documents.Accounts.Cast<Aggregate>().Concat(documents.Operations).Concat(documents.Instruments)
                     .OrderBy(document => document.Id))
        {
            hash.Add(document.Id);
            hash.Add(document.Version);
            hash.Add(document.Deleted);
            hash.Add(document.Seq);
        }

        return hash.ToHashCode();
    }

    async Task<InvestmentOverview> BuildAsync(
        Documents documents, DateOnly today, TimeZoneInfo zone, bool online, bool refresh, bool withHistory, CancellationToken ct)
    {
        var (accounts, operations, all) = documents;
        var instruments = all.GroupBy(instrument => instrument.Ticker, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var live = operations.Where(operation => !operation.Deleted).ToList();
        var from = live.Count == 0 ? today : DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(live.Min(operation => operation.At), zone).DateTime);
        if (from > today)
        {
            from = today;
        }

        var asked = live.Select(operation => operation.Ticker).OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select(ticker => (Ticker: ticker,
                Symbol: instruments.TryGetValue(ticker, out var instrument) ? instrument.QuoteSymbol : QuoteSymbols.DefaultFor(ticker)))
            .ToList();
        var prices = asked.Count == 0
            ? new Dictionary<string, PriceHistory>(StringComparer.Ordinal)
            : await quotes.PricesAsync(asked, from, refresh, online, ct);

        var currencies = accounts.Select(account => account.Currency)
            .Concat(prices.Values.Select(history => history.MajorCurrency))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var fx = await quotes.FxAsync(currencies, from.AddDays(-InvestmentImportRates.LeadDays), today, online, ct);

        var byAccount = operations.ToLookup(operation => operation.AccountId);
        var figures = accounts.ToDictionary(account => account.Id,
            account => AccountFigures.For(account, [.. byAccount[account.Id]], prices, fx, today));
        var open = accounts.Where(account => !account.IsArchived).ToList();
        var values = withHistory
            ? open.ToDictionary(account => account.Id,
                account => ValueOverTime.For(account, [.. byAccount[account.Id]], prices, fx, from, today, zone))
            : new Dictionary<Guid, IReadOnlyList<ValuePoint>>();
        var portfolio = PortfolioFigures.For(
            [.. open.Select(account => (figures[account.Id], (IReadOnlyList<InvestmentOperation>)[.. byAccount[account.Id]]))],
            fx, today);
        IReadOnlyList<ValuePoint> inPln = withHistory ? ValueOverTime.InPln([.. open.Select(account => (account, values[account.Id]))], fx) : [];

        return new InvestmentOverview(today, from, zone, accounts, operations, instruments, prices, fx, figures, values, portfolio, inPln);
    }

    sealed record Documents(List<InvestmentAccount> Accounts, List<InvestmentOperation> Operations, List<Instrument> Instruments);
}
