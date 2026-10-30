using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Instruments;
using PSPad.Module.Investments.Operations;
using PSPad.Module.Investments.Reading;

namespace PSPad.App.State.Investments;

public sealed record InvestmentOverview(
    DateOnly Today,
    DateOnly From,
    TimeZoneInfo Zone,
    IReadOnlyList<InvestmentAccount> Accounts,
    IReadOnlyList<InvestmentOperation> Operations,
    IReadOnlyDictionary<string, Instrument> Instruments,
    IReadOnlyDictionary<string, PriceHistory> Prices,
    FxBook Fx,
    IReadOnlyDictionary<Guid, AccountFigures> Figures,
    IReadOnlyDictionary<Guid, IReadOnlyList<ValuePoint>> ValueByAccount,
    PortfolioFigures Portfolio,
    IReadOnlyList<ValuePoint> ValueInPln)
{
    public IReadOnlyList<InvestmentAccount> Open => [.. Accounts.Where(account => !account.IsArchived)];

    public IReadOnlyList<ValuePoint> ValueOf(Guid accountId) =>
        ValueByAccount.TryGetValue(accountId, out var points) ? points
        : Account(accountId) is { } account ? ValueOverTime.For(account, OperationsOf(accountId), Prices, Fx, From, Today, Zone)
        : [];

    public InvestmentAccount? Account(Guid accountId) => Accounts.FirstOrDefault(account => account.Id == accountId);

    public IReadOnlyList<InvestmentOperation> OperationsOf(Guid accountId) =>
        [.. Operations.Where(operation => operation.AccountId == accountId)];

    public string SymbolOf(string ticker) =>
        Instruments.TryGetValue(ticker, out var instrument) ? instrument.QuoteSymbol : QuoteSymbols.DefaultFor(ticker);

    public string NameOf(string ticker) => Instruments.TryGetValue(ticker, out var instrument) ? instrument.Name : ticker;

    public static decimal? ShareOf(decimal profit, decimal netDeposits) => netDeposits > 0 ? profit / netDeposits : null;

    public decimal? ProfitShare(Guid accountId) =>
        Figures.TryGetValue(accountId, out var figures) ? ShareOf(figures.TotalProfit, figures.NetDeposits) : null;

    public decimal? PortfolioProfitShare => ShareOf(Portfolio.TotalProfitPln, Portfolio.NetDepositsPln);
}
