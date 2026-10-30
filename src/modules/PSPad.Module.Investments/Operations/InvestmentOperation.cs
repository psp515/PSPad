using System.Globalization;
using System.Text.Json.Serialization;
using PSPad.Abstractions;
using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Instruments;
using MoneyValue = PSPad.Module.Money.Money;

namespace PSPad.Module.Investments.Operations;

public sealed class InvestmentOperation : Aggregate
{
    public const int MaxNoteLength = ContentLimits.FieldValue;

    public const decimal MaxQuantity = 1_000_000_000m;

    public const decimal MaxPrice = 1_000_000_000m;

    public const int MaxDecimals = 8;

    public const int MaxPositionIdLength = 32;

    static readonly byte[] Salt = Guid.Parse("6f706572-6174-696f-6e00-000000000001").ToByteArray();

    [JsonInclude]
    public Guid AccountId { get; private set; }

    [JsonInclude]
    public OperationKind Kind { get; private set; }

    [JsonInclude]
    public DateTimeOffset At { get; private set; }

    [JsonInclude]
    public string? Ticker { get; private set; }

    [JsonInclude]
    public decimal? Quantity { get; private set; }

    [JsonInclude]
    public decimal? Price { get; private set; }

    [JsonInclude]
    public MoneyValue Money { get; private set; } = default!;

    [JsonInclude]
    public string? PositionId { get; private set; }

    [JsonInclude]
    public OperationSource Source { get; private set; }

    [JsonInclude]
    public string? ExternalId { get; private set; }

    [JsonInclude]
    public string? Note { get; private set; }

    [JsonInclude]
    public DateTimeOffset RecordedAt { get; private set; }

    [JsonIgnore]
    public decimal SignedAmount => Kind switch
    {
        OperationKind.Deposit or OperationKind.Sell or OperationKind.Dividend or OperationKind.Interest => Money.Amount,
        _ => -Money.Amount
    };

    public static Guid IdFor(Guid accountId, long externalId) => DeterministicId.From(accountId, Salt, (ulong)externalId);

    public static IReadOnlyList<DomainEvent> Decide(
        InvestmentOperation? operation, InvestmentAccount? account, ICommand command, DateTimeOffset at)
    {
        switch (command)
        {
            case RecordInvestmentOperation record:
                var owner = InvestmentAccountAccess.Owned(account, record.UserId);
                if (operation is not null)
                {
                    return operation.UserId == record.UserId ? [] : throw Missing();
                }

                var recording = InvestmentAccountAccess.Writable(owner);
                var recorded = Valid(recording, record.Kind, record.At, record.Ticker, record.Quantity, record.Price,
                    record.Money, record.PositionId, record.Note);
                return [new InvestmentOperationRecorded(record.OperationId, recording.UserId, at, recording.Id,
                    recorded.Kind, recorded.At, recorded.Ticker, recorded.Quantity, recorded.Price, recorded.Money,
                    recorded.PositionId, RequireSource(record.Source), ExternalIdOf(record), recorded.Note)];

            case EditInvestmentOperation edit:
                var editing = RequireLive(operation);
                var edited = Valid(Writable(editing, account, edit.UserId), edit.Kind, edit.At, edit.Ticker,
                    edit.Quantity, edit.Price, edit.Money, edit.PositionId, edit.Note);
                return edited == ValuesOf(editing)
                    ? []
                    : [new InvestmentOperationEdited(editing.Id, editing.UserId, at, edited.Kind, edited.At,
                        edited.Ticker, edited.Quantity, edited.Price, edited.Money, edited.PositionId, edited.Note)];

            case DeleteInvestmentOperation delete:
                var deleting = operation ?? throw Missing();
                Writable(deleting, account, delete.UserId);
                return deleting.Deleted ? [] : [new InvestmentOperationDeleted(deleting.Id, deleting.UserId, at)];

            case RestoreInvestmentOperation restore:
                var restoring = operation ?? throw Missing();
                Writable(restoring, account, restore.UserId);
                return restoring.Deleted ? [new InvestmentOperationRestored(restoring.Id, restoring.UserId, at)] : [];

            default:
                throw new DomainRejectedException($"An investment operation cannot handle {command.GetType().Name}.");
        }
    }

    protected override void When(DomainEvent @event)
    {
        switch (@event)
        {
            case InvestmentOperationRecorded recorded:
                Id = recorded.AggregateId;
                UserId = recorded.UserId;
                RecordedAt = recorded.At;
                AccountId = recorded.AccountId;
                Source = recorded.Source;
                ExternalId = recorded.ExternalId;
                Take(new OperationValues(recorded.Kind, recorded.OperationAt, recorded.Ticker, recorded.Quantity,
                    recorded.Price, recorded.Money, recorded.PositionId, recorded.Note));
                break;
            case InvestmentOperationEdited edited:
                Take(new OperationValues(edited.Kind, edited.OperationAt, edited.Ticker, edited.Quantity, edited.Price,
                    edited.Money, edited.PositionId, edited.Note));
                break;
            case InvestmentOperationDeleted:
                Deleted = true;
                break;
            case InvestmentOperationRestored:
                Deleted = false;
                break;
        }
    }

    void Take(OperationValues values)
    {
        Kind = values.Kind;
        At = values.At;
        Ticker = values.Ticker;
        Quantity = values.Quantity;
        Price = values.Price;
        Money = values.Money;
        PositionId = values.PositionId;
        Note = values.Note;
    }

    static OperationValues ValuesOf(InvestmentOperation operation) =>
        new(operation.Kind, operation.At, operation.Ticker, operation.Quantity, operation.Price, operation.Money,
            operation.PositionId, operation.Note);

    static DomainRejectedException Missing() => new("That operation does not exist.", Unrecoverable: true);

    static InvestmentOperation RequireLive(InvestmentOperation? operation) =>
        operation is null || operation.Deleted ? throw Missing() : operation;

    static InvestmentAccount Writable(InvestmentOperation operation, InvestmentAccount? account, Guid actorId)
    {
        var owner = InvestmentAccountAccess.Owned(account, actorId);
        return operation.UserId != actorId || operation.AccountId != owner.Id
            ? throw Missing()
            : InvestmentAccountAccess.Writable(owner);
    }

    static OperationValues Valid(
        InvestmentAccount account, OperationKind kind, DateTimeOffset at, string? ticker, decimal? quantity,
        decimal? price, MoneyValue money, string? positionId, string? note)
    {
        var valid = MoneyValue.Require(money, DateOnly.FromDateTime(at.UtcDateTime));
        if (valid.Currency != account.Currency)
        {
            throw new DomainRejectedException("An operation is in the account's currency.");
        }

        var name = Enum.IsDefined(kind)
            ? kind.ToString().ToLowerInvariant()
            : throw new DomainRejectedException("That operation kind does not exist.");
        var traded = kind is OperationKind.Buy or OperationKind.Sell;
        var held = traded || kind is OperationKind.Dividend or OperationKind.Tax;

        return new OperationValues(kind, at.ToUniversalTime(),
            held ? TickerOf(ticker, name) : Absent(ticker, $"A {name} has no ticker."),
            traded ? Positive(quantity, MaxQuantity, $"A {name} needs a quantity.", "A quantity")
                : Absent(quantity, $"A {name} has no quantity."),
            traded ? Positive(price, MaxPrice, $"A {name} needs a price.", "A price")
                : Absent(price, $"A {name} has no price."),
            valid, PositionIdOf(positionId), NoteOf(note));
    }

    static string TickerOf(string? ticker, string kind) =>
        string.IsNullOrWhiteSpace(ticker)
            ? throw new DomainRejectedException($"A {kind} needs a ticker.")
            : Instrument.RequireTicker(ticker);

    static string? Absent(string? value, string message) =>
        string.IsNullOrWhiteSpace(value) ? null : throw new DomainRejectedException(message);

    static decimal? Absent(decimal? value, string message) =>
        value is null ? null : throw new DomainRejectedException(message);

    static decimal Positive(decimal? value, decimal max, string missing, string what)
    {
        if (value is not { } given || given <= 0)
        {
            throw new DomainRejectedException(missing);
        }

        if (given > max)
        {
            throw new DomainRejectedException($"{what} can be at most 1 000 000 000.");
        }

        return given != Math.Round(given, MaxDecimals)
            ? throw new DomainRejectedException($"{what} has at most {MaxDecimals} decimals.")
            : given;
    }

    static OperationSource RequireSource(OperationSource source) =>
        Enum.IsDefined(source) ? source : throw new DomainRejectedException("That operation source does not exist.");

    static string? ExternalIdOf(RecordInvestmentOperation record)
    {
        if (record.Source != OperationSource.Xtb)
        {
            return string.IsNullOrWhiteSpace(record.ExternalId)
                ? null
                : throw new DomainRejectedException("A manual operation has no external id.");
        }

        var external = record.ExternalId?.Trim();
        return long.TryParse(external, NumberStyles.None, CultureInfo.InvariantCulture, out var xtbId)
               && record.OperationId == IdFor(record.AccountId, xtbId)
            ? external
            : throw new DomainRejectedException("An imported operation has the wrong id.");
    }

    static string? PositionIdOf(string? positionId)
    {
        if (string.IsNullOrWhiteSpace(positionId))
        {
            return null;
        }

        var trimmed = positionId.Trim();
        return trimmed.Length > MaxPositionIdLength
            ? throw new DomainRejectedException($"A position id is at most {MaxPositionIdLength} characters.")
            : trimmed;
    }

    static string? NoteOf(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return null;
        }

        var trimmed = note.Trim();
        return trimmed.Length > MaxNoteLength
            ? throw new DomainRejectedException($"A note is at most {MaxNoteLength} characters.")
            : trimmed;
    }
}
