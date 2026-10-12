using System.Text.Json.Serialization;
using PSPad.Abstractions;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Values;

namespace PSPad.Module.Money.Balance;

public sealed class BalanceSnapshot : Aggregate
{
    public const int MaxHoldingNameLength = ContentLimits.Name;

    public const int MaxHoldings = 64;

    static readonly byte[] Salt = Guid.Parse("62616c61-6e63-6573-6e61-700000000001").ToByteArray();

    [JsonInclude]
    public Guid BudgetId { get; private set; }

    [JsonInclude]
    public DateTimeOffset UpdatedAt { get; private set; }

    // The BSON driver cannot deserialize a struct, so the month is stored as its yyyy-MM text.
    [JsonInclude]
    string _month = "";

    [JsonInclude]
    List<Holding> _holdings = [];

    [JsonIgnore]
    public YearMonth Month => YearMonth.Parse(_month);

    public IReadOnlyList<Holding> Holdings => _holdings;

    public static Guid IdFor(Guid budgetId, YearMonth month)
    {
        var bytes = budgetId.ToByteArray();
        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index] ^= Salt[index];
        }

        bytes[0] ^= (byte)(month.Year >> 8);
        bytes[1] ^= (byte)month.Year;
        bytes[2] ^= (byte)month.Month;
        return new Guid(bytes);
    }

    public static IReadOnlyList<DomainEvent> Decide(
        BalanceSnapshot? snapshot, Budget? budget, ICommand command, DateTimeOffset at) =>
        Decide(snapshot, budget, [], command, at);

    public static IReadOnlyList<DomainEvent> Decide(
        BalanceSnapshot? snapshot, Budget? budget, IReadOnlyList<BalanceSnapshot> history, ICommand command,
        DateTimeOffset at)
    {
        switch (command)
        {
            case StartBalanceMonth start:
                var starting = Writable(budget, start.UserId);
                if (snapshot is not null && snapshot.BudgetId != starting.Id)
                {
                    throw new DomainRejectedException("That budget does not exist.");
                }

                if (snapshot is { Deleted: false })
                {
                    return [];
                }

                return [new BalanceMonthStarted(IdFor(starting.Id, start.Month), starting.UserId, at, starting.Id,
                    start.Month, CarriedInto(start.Month, starting.Id, history))];

            case AddHolding add:
                var adding = Live(snapshot, Writable(budget, add.UserId));
                if (adding.IndexOf(add.HoldingId) >= 0)
                {
                    throw new DomainRejectedException("That holding already exists.");
                }

                if (adding._holdings.Count >= MaxHoldings)
                {
                    throw new DomainRejectedException($"A month can have at most {MaxHoldings} holdings.");
                }

                return [new HoldingAdded(adding.Id, adding.UserId, at,
                    Valid(add.HoldingId, add.Name, add.Type, add.Money, adding.Month))];

            case EditHolding edit:
                var editing = Live(snapshot, Writable(budget, edit.UserId));
                var current = editing._holdings[RequireHolding(editing, edit.HoldingId)];
                var edited = Valid(edit.HoldingId, edit.Name, edit.Type, edit.Money, editing.Month);
                return current == edited ? [] : [new HoldingEdited(editing.Id, editing.UserId, at, edited)];

            case RemoveHolding remove:
                var removing = Live(snapshot, Writable(budget, remove.UserId));
                RequireHolding(removing, remove.HoldingId);
                return [new HoldingRemoved(removing.Id, removing.UserId, at, remove.HoldingId)];

            case DeleteBalanceMonth delete:
                var owner = Writable(budget, delete.UserId);
                var deleting = snapshot is null || snapshot.BudgetId != owner.Id
                    ? throw new DomainRejectedException("That month has no balance yet.")
                    : snapshot;
                return deleting.Deleted ? [] : [new BalanceMonthDeleted(deleting.Id, deleting.UserId, at)];

            default:
                throw new DomainRejectedException($"A balance snapshot cannot handle {command.GetType().Name}.");
        }
    }

    protected override void When(DomainEvent @event)
    {
        UpdatedAt = @event.At;

        switch (@event)
        {
            case BalanceMonthStarted started:
                Id = started.AggregateId;
                UserId = started.UserId;
                BudgetId = started.BudgetId;
                _month = started.Month.ToString();
                _holdings = [.. started.Holdings];
                Deleted = false;
                break;
            case HoldingAdded added:
                _holdings.Add(added.Holding);
                break;
            case HoldingEdited edited:
                _holdings[IndexOf(edited.Holding.HoldingId)] = edited.Holding;
                break;
            case HoldingRemoved removed:
                _holdings.RemoveAt(IndexOf(removed.HoldingId));
                break;
            case BalanceMonthDeleted:
                Deleted = true;
                break;
        }
    }

    int IndexOf(Guid holdingId) => _holdings.FindIndex(holding => holding.HoldingId == holdingId);

    static Budget Writable(Budget? budget, Guid actorId) => BudgetAccess.Writable(BudgetAccess.To(budget, actorId));

    static BalanceSnapshot Live(BalanceSnapshot? snapshot, Budget budget) =>
        snapshot is null || snapshot.Deleted || snapshot.BudgetId != budget.Id
            ? throw new DomainRejectedException("That month has no balance yet.")
            : snapshot;

    static int RequireHolding(BalanceSnapshot snapshot, Guid holdingId)
    {
        var index = snapshot.IndexOf(holdingId);
        return index < 0 ? throw new DomainRejectedException("That holding does not exist.") : index;
    }

    static IReadOnlyList<Holding> CarriedInto(YearMonth month, Guid budgetId, IReadOnlyList<BalanceSnapshot> history)
    {
        var previous = history
            .Where(earlier => !earlier.Deleted && earlier.BudgetId == budgetId && earlier.Month < month)
            .OrderByDescending(earlier => earlier.Month)
            .FirstOrDefault();

        return previous is null ? [] : [.. previous._holdings];
    }

    static Holding Valid(Guid holdingId, string name, HoldingType type, Money money, YearMonth month) =>
        new(holdingId, RequireName(name), RequireType(type), Money.Require(money, month.LastDay));

    static HoldingType RequireType(HoldingType type) =>
        Enum.IsDefined(type) ? type : throw new DomainRejectedException("That holding type does not exist.");

    static string RequireName(string name)
    {
        var trimmed = name.Trim();

        if (trimmed.Length == 0)
        {
            throw new DomainRejectedException("A holding needs a name.");
        }

        return trimmed.Length > MaxHoldingNameLength
            ? throw new DomainRejectedException($"A holding name is at most {MaxHoldingNameLength} characters.")
            : trimmed;
    }
}
