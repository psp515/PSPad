using System.Text.Json.Serialization;
using PSPad.Abstractions;
using PSPad.Module.Money.Values;

namespace PSPad.Module.Money.Preferences;

public sealed class MoneyPreferences : Aggregate
{
    static readonly byte[] Salt = Guid.Parse("6d6f6e65-7970-7265-6673-000000000001").ToByteArray();

    [JsonInclude]
    public string DefaultCurrency { get; private set; } = Currencies.Pln;

    public static Guid IdFor(Guid userId)
    {
        var bytes = userId.ToByteArray();
        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index] ^= Salt[index];
        }

        return new Guid(bytes);
    }

    public static IReadOnlyList<DomainEvent> Decide(MoneyPreferences? preferences, ICommand command, DateTimeOffset at)
    {
        switch (command)
        {
            case SetDefaultCurrency set:
                if (preferences is not null && preferences.UserId != set.UserId)
                {
                    throw new DomainRejectedException("Those preferences belong to somebody else.");
                }

                var currency = Currencies.Require(set.Currency);
                return preferences?.DefaultCurrency == currency
                    ? []
                    : [new DefaultCurrencySet(IdFor(set.UserId), set.UserId, at, currency)];

            default:
                throw new DomainRejectedException($"Money preferences cannot handle {command.GetType().Name}.");
        }
    }

    protected override void When(DomainEvent @event)
    {
        if (@event is DefaultCurrencySet set)
        {
            Id = set.AggregateId;
            UserId = set.UserId;
            DefaultCurrency = set.Currency;
        }
    }
}
