using System.Text.Json.Serialization;
using PSPad.Abstractions;

namespace PSPad.Module.Investments.Instruments;

public sealed class Instrument : Aggregate
{
    public const int MaxTickerLength = 24;

    public const int MaxQuoteSymbolLength = 24;

    public const int MaxNameLength = 64;

    static readonly byte[] Salt = Guid.Parse("696e7374-7275-6d65-6e74-000000000001").ToByteArray();

    [JsonInclude]
    public string Ticker { get; private set; } = "";

    [JsonInclude]
    public string Name { get; private set; } = "";

    [JsonInclude]
    public string QuoteSymbol { get; private set; } = "";

    [JsonInclude]
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Guid IdFor(Guid userId, string ticker) =>
        DeterministicId.From(userId, Salt, DeterministicId.Fnv1a64(ticker.Trim().ToUpperInvariant()));

    public static IReadOnlyList<DomainEvent> Decide(Instrument? instrument, ICommand command, DateTimeOffset at)
    {
        switch (command)
        {
            case EnsureInstrument ensure:
                var ticker = RequireTicker(ensure.Ticker);
                if (instrument is not null)
                {
                    return [];
                }

                return [new InstrumentEnsured(IdFor(ensure.UserId, ticker), ensure.UserId, at, ticker,
                    NameOf(ensure.Name, ticker), QuoteSymbols.DefaultFor(ticker))];

            case SetQuoteSymbol set:
                var setting = instrument is null || instrument.UserId != set.UserId
                    ? throw new DomainRejectedException("That instrument does not exist.", Unrecoverable: true)
                    : instrument;
                var symbol = RequireSymbol(set.Symbol);
                return setting.QuoteSymbol == symbol ? [] : [new QuoteSymbolSet(setting.Id, setting.UserId, at, symbol)];

            default:
                throw new DomainRejectedException($"An instrument cannot handle {command.GetType().Name}.");
        }
    }

    protected override void When(DomainEvent @event)
    {
        UpdatedAt = @event.At;

        switch (@event)
        {
            case InstrumentEnsured ensured:
                Id = ensured.AggregateId;
                UserId = ensured.UserId;
                Ticker = ensured.Ticker;
                Name = ensured.Name;
                QuoteSymbol = ensured.QuoteSymbol;
                break;
            case QuoteSymbolSet set:
                QuoteSymbol = set.Symbol;
                break;
        }
    }

    internal static string RequireTicker(string ticker)
    {
        var normalised = ticker.Trim().ToUpperInvariant();

        if (normalised.Length == 0)
        {
            throw new DomainRejectedException("An instrument needs a ticker.");
        }

        return normalised.Length > MaxTickerLength
            ? throw new DomainRejectedException($"A ticker is at most {MaxTickerLength} characters.")
            : normalised;
    }

    static string RequireSymbol(string symbol)
    {
        var normalised = symbol.Trim().ToUpperInvariant();

        if (normalised.Length == 0)
        {
            throw new DomainRejectedException("An instrument needs a quote symbol.");
        }

        return normalised.Length > MaxQuoteSymbolLength
            ? throw new DomainRejectedException($"A quote symbol is at most {MaxQuoteSymbolLength} characters.")
            : normalised;
    }

    static string NameOf(string name, string ticker)
    {
        var trimmed = name.Trim();

        if (trimmed.Length == 0)
        {
            return ticker;
        }

        return trimmed.Length > MaxNameLength
            ? throw new DomainRejectedException($"An instrument name is at most {MaxNameLength} characters.")
            : trimmed;
    }
}
