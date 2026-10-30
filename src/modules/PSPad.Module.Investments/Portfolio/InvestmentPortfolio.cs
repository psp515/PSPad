using System.Text.Json.Serialization;
using PSPad.Abstractions;
using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Instruments;

namespace PSPad.Module.Investments.Portfolio;

public sealed class InvestmentPortfolio : Aggregate
{
    public const int MaxAccounts = 32;

    public const int MaxInstruments = 500;

    static readonly byte[] Salt = Guid.Parse("706f7274-666f-6c69-6f00-000000000001").ToByteArray();

    [JsonInclude]
    List<Guid> _accountIds = [];

    [JsonInclude]
    List<string> _tickers = [];

    public IReadOnlyList<Guid> AccountIds => _accountIds;

    public IReadOnlyList<string> Tickers => _tickers;

    public static Guid IdFor(Guid userId) => DeterministicId.From(userId, Salt, 0);

    public static IReadOnlyList<DomainEvent> Decide(InvestmentPortfolio? portfolio, ICommand command, DateTimeOffset at)
    {
        switch (command)
        {
            case CreateInvestmentAccount create:
                var accounts = portfolio?._accountIds ?? [];
                if (accounts.Contains(create.AccountId))
                {
                    return [];
                }

                return accounts.Count >= MaxAccounts
                    ? throw new DomainRejectedException($"You can have at most {MaxAccounts} investment accounts.")
                    : [new PortfolioAccountAdded(IdFor(create.UserId), create.UserId, at, create.AccountId)];

            case EnsureInstrument ensure:
                var ticker = Instrument.RequireTicker(ensure.Ticker);
                var tickers = portfolio?._tickers ?? [];
                if (tickers.Contains(ticker))
                {
                    return [];
                }

                return tickers.Count >= MaxInstruments
                    ? throw new DomainRejectedException($"You can have at most {MaxInstruments} instruments.")
                    : [new PortfolioTickerAdded(IdFor(ensure.UserId), ensure.UserId, at, ticker)];

            default:
                throw new DomainRejectedException($"A portfolio cannot handle {command.GetType().Name}.");
        }
    }

    protected override void When(DomainEvent @event)
    {
        Id = @event.AggregateId;
        UserId = @event.UserId;

        switch (@event)
        {
            case PortfolioAccountAdded added:
                _accountIds.Add(added.AccountId);
                break;
            case PortfolioTickerAdded added:
                _tickers.Add(added.Ticker);
                break;
        }
    }
}
