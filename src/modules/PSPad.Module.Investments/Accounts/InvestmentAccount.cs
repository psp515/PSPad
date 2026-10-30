using System.Text.Json.Serialization;
using PSPad.Abstractions;
using PSPad.Module.Investments.Operations;
using PSPad.Module.Money.Values;

namespace PSPad.Module.Investments.Accounts;

public sealed class InvestmentAccount : Aggregate
{
    public const int MaxNameLength = ContentLimits.Name;

    public const int MaxBrokerAccountNumberLength = 32;

    public const int MaxOperations = 20_000;

    [JsonInclude]
    public string Name { get; private set; } = "";

    [JsonInclude]
    public string Currency { get; private set; } = "";

    [JsonInclude]
    public Broker Broker { get; private set; }

    [JsonInclude]
    public string? BrokerAccountNumber { get; private set; }

    [JsonInclude]
    public int OperationCount { get; private set; }

    [JsonInclude]
    public DateTimeOffset CreatedAt { get; private set; }

    [JsonInclude]
    public DateTimeOffset? ArchivedAt { get; private set; }

    public bool IsArchived => ArchivedAt is not null;

    public static IReadOnlyList<DomainEvent> Decide(InvestmentAccount? account, ICommand command, DateTimeOffset at)
    {
        switch (command)
        {
            case CreateInvestmentAccount create:
                if (account is not null)
                {
                    throw new DomainRejectedException("That account already exists.");
                }

                return [new InvestmentAccountCreated(create.AccountId, create.UserId, at, RequireName(create.Name),
                    Currencies.Require(create.Currency), RequireBroker(create.Broker), NumberOf(create.BrokerAccountNumber))];

            case RenameInvestmentAccount rename:
                var renaming = Writable(account, rename.UserId);
                var name = RequireName(rename.Name);
                return renaming.Name == name ? [] : [new InvestmentAccountRenamed(renaming.Id, renaming.UserId, at, name)];

            case ArchiveInvestmentAccount archive:
                var archiving = InvestmentAccountAccess.Owned(account, archive.UserId);
                return archiving.IsArchived ? [] : [new InvestmentAccountArchived(archiving.Id, archiving.UserId, at)];

            case RestoreInvestmentAccount restore:
                var restoring = InvestmentAccountAccess.Owned(account, restore.UserId);
                return restoring.IsArchived ? [new InvestmentAccountRestored(restoring.Id, restoring.UserId, at)] : [];

            case SetBrokerAccountNumber set:
                var numbering = Writable(account, set.UserId);
                var number = NumberOf(set.Number);
                if (number is null || numbering.BrokerAccountNumber == number)
                {
                    return [];
                }

                return numbering.BrokerAccountNumber is { } current
                    ? throw new DomainRejectedException($"This account already has broker account number {current}.")
                    : [new BrokerAccountNumberSet(numbering.Id, numbering.UserId, at, number)];

            default:
                throw new DomainRejectedException($"An investment account cannot handle {command.GetType().Name}.");
        }
    }

    public static IReadOnlyList<DomainEvent> Count(
        InvestmentAccount? account, InvestmentOperation? operation, RecordInvestmentOperation command, DateTimeOffset at)
    {
        var counting = InvestmentAccountAccess.Owned(account, command.UserId);
        if (operation is not null)
        {
            return [];
        }

        return InvestmentAccountAccess.Writable(counting).OperationCount >= MaxOperations
            ? throw new DomainRejectedException("An account can hold at most 20,000 operations.")
            : [new AccountOperationCounted(counting.Id, counting.UserId, at)];
    }

    protected override void When(DomainEvent @event)
    {
        switch (@event)
        {
            case InvestmentAccountCreated created:
                Id = created.AggregateId;
                UserId = created.UserId;
                Name = created.Name;
                Currency = created.Currency;
                Broker = created.Broker;
                BrokerAccountNumber = created.BrokerAccountNumber;
                CreatedAt = created.At;
                break;
            case InvestmentAccountRenamed renamed:
                Name = renamed.Name;
                break;
            case InvestmentAccountArchived archived:
                ArchivedAt = archived.At;
                break;
            case InvestmentAccountRestored:
                ArchivedAt = null;
                break;
            case BrokerAccountNumberSet set:
                BrokerAccountNumber = set.Number;
                break;
            case AccountOperationCounted:
                OperationCount++;
                break;
        }
    }

    static InvestmentAccount Writable(InvestmentAccount? account, Guid actorId) =>
        InvestmentAccountAccess.Writable(InvestmentAccountAccess.Owned(account, actorId));

    static Broker RequireBroker(Broker broker) =>
        Enum.IsDefined(broker) ? broker : throw new DomainRejectedException("That broker does not exist.");

    static string RequireName(string name)
    {
        var trimmed = name.Trim();

        if (trimmed.Length == 0)
        {
            throw new DomainRejectedException("An account needs a name.");
        }

        return trimmed.Length > MaxNameLength
            ? throw new DomainRejectedException($"An account name is at most {MaxNameLength} characters.")
            : trimmed;
    }

    static string? NumberOf(string? number)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return null;
        }

        var trimmed = number.Trim();
        return trimmed.Length > MaxBrokerAccountNumberLength
            ? throw new DomainRejectedException(
                $"A broker account number is at most {MaxBrokerAccountNumberLength} characters.")
            : trimmed;
    }
}
