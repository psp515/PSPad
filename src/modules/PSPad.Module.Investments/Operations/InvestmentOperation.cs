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

    public const int MaxCounterpartyAccountNumberLength = 32;

    static readonly byte[] Salt = Guid.Parse("6f706572-6174-696f-6e00-000000000001").ToByteArray();

    static readonly byte[] ReceivedSalt = Guid.Parse("72656365-6976-6564-0000-000000000001").ToByteArray();

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
    public TransferType? TransferType { get; private set; }

    [JsonInclude]
    public string? CounterpartyAccountNumber { get; private set; }

    [JsonInclude]
    public MoneyValue? CounterMoney { get; private set; }

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
        OperationKind.Deposit or OperationKind.TransferIn or OperationKind.Sell or OperationKind.Dividend
            or OperationKind.TaxRefund or OperationKind.Interest => Money.Amount,
        OperationKind.SharesReceived => 0m,
        _ => -Money.Amount
    };

    public static Guid IdFor(Guid accountId, long externalId) => DeterministicId.From(accountId, Salt, (ulong)externalId);

    public static Guid ReceivedIdFor(Guid accountId, long positionId) =>
        DeterministicId.From(accountId, ReceivedSalt, (ulong)positionId);

    public static Guid ImportedIdFor(Guid accountId, OperationKind kind, long externalId) =>
        kind == OperationKind.SharesReceived ? ReceivedIdFor(accountId, externalId) : IdFor(accountId, externalId);

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
                var recorded = Valid(recording, new OperationValues(record.Kind, record.At, record.Ticker,
                    record.Quantity, record.Price, record.Money, record.PositionId, record.Note, record.TransferType,
                    record.CounterpartyAccountNumber, record.CounterMoney));
                return [new InvestmentOperationRecorded(record.OperationId, recording.UserId, at, recording.Id,
                    recorded.Kind, recorded.At, recorded.Ticker, recorded.Quantity, recorded.Price, recorded.Money,
                    recorded.PositionId, RequireSource(record.Source), ExternalIdOf(record), recorded.Note,
                    recorded.TransferType, recorded.CounterpartyAccountNumber, recorded.CounterMoney)];

            case EditInvestmentOperation edit:
                var editing = RequireLive(operation);
                var edited = Valid(Writable(editing, account, edit.UserId), new OperationValues(edit.Kind, edit.At,
                    edit.Ticker, edit.Quantity, edit.Price, edit.Money, edit.PositionId, edit.Note, edit.TransferType,
                    edit.CounterpartyAccountNumber, edit.CounterMoney));
                return edited == ValuesOf(editing)
                    ? []
                    : [new InvestmentOperationEdited(editing.Id, editing.UserId, at, edited.Kind, edited.At,
                        edited.Ticker, edited.Quantity, edited.Price, edited.Money, edited.PositionId, edited.Note,
                        edited.TransferType, edited.CounterpartyAccountNumber, edited.CounterMoney)];

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
                    recorded.Price, recorded.Money, recorded.PositionId, recorded.Note, recorded.TransferType,
                    recorded.CounterpartyAccountNumber, recorded.CounterMoney));
                break;
            case InvestmentOperationEdited edited:
                Take(new OperationValues(edited.Kind, edited.OperationAt, edited.Ticker, edited.Quantity, edited.Price,
                    edited.Money, edited.PositionId, edited.Note, edited.TransferType, edited.CounterpartyAccountNumber,
                    edited.CounterMoney));
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
        TransferType = values.TransferType;
        CounterpartyAccountNumber = values.CounterpartyAccountNumber;
        CounterMoney = values.CounterMoney;
    }

    static OperationValues ValuesOf(InvestmentOperation operation) =>
        new(operation.Kind, operation.At, operation.Ticker, operation.Quantity, operation.Price, operation.Money,
            operation.PositionId, operation.Note, operation.TransferType, operation.CounterpartyAccountNumber,
            operation.CounterMoney);

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

    static OperationValues Valid(InvestmentAccount account, OperationValues requested)
    {
        var kind = requested.Kind;
        var valid = MoneyOf(kind, requested.Money, DateOnly.FromDateTime(requested.At.UtcDateTime));
        if (valid.Currency != account.Currency)
        {
            throw new DomainRejectedException("An operation is in the account's currency.");
        }

        var name = Enum.IsDefined(kind)
            ? kind == OperationKind.SharesReceived ? "received share" : kind.ToString().ToLowerInvariant()
            : throw new DomainRejectedException("That operation kind does not exist.");
        var traded = kind is OperationKind.Buy or OperationKind.Sell or OperationKind.SharesReceived;
        var held = traded || kind is OperationKind.Dividend;
        var mayHold = kind is OperationKind.Tax or OperationKind.TaxRefund or OperationKind.Fee;
        var ticker = requested.Ticker;
        var transferType = TransferTypeOf(kind, requested.TransferType);

        return new OperationValues(kind, requested.At.ToUniversalTime(),
            held ? TickerOf(ticker, name)
                : mayHold ? OptionalTickerOf(ticker)
                : Absent(ticker, $"A {name} has no ticker."),
            traded ? Positive(requested.Quantity, MaxQuantity, $"A {name} needs a quantity.", "A quantity")
                : Absent(requested.Quantity, $"A {name} has no quantity."),
            traded ? Positive(requested.Price, MaxPrice, $"A {name} needs a price.", "A price")
                : Absent(requested.Price, $"A {name} has no price."),
            valid, PositionIdOf(requested.PositionId), NoteOf(requested.Note), transferType,
            CounterpartyOf(transferType, requested.CounterpartyAccountNumber),
            CounterMoneyOf(account, transferType, requested.CounterMoney, valid.RateDate));
    }

    static MoneyValue MoneyOf(OperationKind kind, MoneyValue money, DateOnly day) =>
        kind == OperationKind.SharesReceived && money is { Amount: 0m }
            ? MoneyValue.Require(money with { Amount = 1m }, day) with { Amount = 0m }
            : MoneyValue.Require(money, day);

    static bool IsTransfer(OperationKind kind) => kind is OperationKind.TransferIn or OperationKind.TransferOut;

    static TransferType? TransferTypeOf(OperationKind kind, TransferType? type)
    {
        if (!IsTransfer(kind))
        {
            return type is null ? null : throw new DomainRejectedException("Only a transfer has a transfer type.");
        }

        return type switch
        {
            null => throw new DomainRejectedException("A transfer needs a type."),
            { } given when Enum.IsDefined(given) => given,
            _ => throw new DomainRejectedException("That transfer type does not exist.")
        };
    }

    static string? CounterpartyOf(TransferType? type, string? number)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return null;
        }

        if (type is null)
        {
            throw new DomainRejectedException("Only a transfer has a counterparty account.");
        }

        var trimmed = number.Trim();
        return trimmed.Length > MaxCounterpartyAccountNumberLength
            ? throw new DomainRejectedException(
                $"A counterparty account number is at most {MaxCounterpartyAccountNumberLength} characters.")
            : trimmed;
    }

    static MoneyValue? CounterMoneyOf(InvestmentAccount account, TransferType? type, MoneyValue? counter, DateOnly day)
    {
        if (counter is null)
        {
            return null;
        }

        if (type != Operations.TransferType.CurrencyConversion)
        {
            throw new DomainRejectedException("Only a currency conversion has a counter amount.");
        }

        var valid = MoneyValue.Require(counter, day);
        return valid.Currency == account.Currency
            ? throw new DomainRejectedException("A currency conversion's other side is in another currency.")
            : valid;
    }

    static string? OptionalTickerOf(string? ticker) =>
        string.IsNullOrWhiteSpace(ticker) ? null : Instrument.RequireTicker(ticker);

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
               && record.OperationId == ImportedIdFor(record.AccountId, record.Kind, xtbId)
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
