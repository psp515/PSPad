using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Instruments;
using PSPad.Module.Investments.Operations;
using MoneyValue = PSPad.Module.Money.Money;

namespace PSPad.App.Tests.State.Investments;

internal static class Investing
{
    public static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly DateTimeOffset Now = new(2026, 10, 30, 8, 0, 0, TimeSpan.Zero);

    public static InvestmentAccount AnAccount(string name, string currency)
    {
        var account = new InvestmentAccount();
        account.ApplyAll(InvestmentAccount.Decide(null,
            new CreateInvestmentAccount(Guid.NewGuid(), Owner, Guid.NewGuid(), name, currency, Broker.Xtb, null), Now));
        return account;
    }

    public static Instrument AnInstrument(string ticker, string name)
    {
        var instrument = new Instrument();
        instrument.ApplyAll(Instrument.Decide(null, new EnsureInstrument(Guid.NewGuid(), Owner, ticker, name), Now));
        return instrument;
    }

    public static InvestmentOperation Deposit(
        InvestmentAccount account, decimal amount, DateOnly day, decimal rate = 4m, TimeOnly? at = null) =>
        Record(account, OperationKind.Deposit, day, null, null, null, amount, rate, at);

    public static InvestmentOperation Buy(
        InvestmentAccount account, string ticker, decimal quantity, decimal price, decimal amount, DateOnly day, decimal rate = 4m) =>
        Record(account, OperationKind.Buy, day, ticker, quantity, price, amount, rate);

    static InvestmentOperation Record(
        InvestmentAccount account, OperationKind kind, DateOnly day, string? ticker, decimal? quantity, decimal? price,
        decimal amount, decimal rate, TimeOnly? at = null)
    {
        var money = new MoneyValue(amount, account.Currency, account.Currency == "PLN" ? 1m : rate, day);
        var command = new RecordInvestmentOperation(Guid.NewGuid(), Owner, Guid.NewGuid(), account.Id, kind,
            new DateTimeOffset(day.ToDateTime(at ?? new TimeOnly(12, 0)), TimeSpan.Zero), ticker, quantity, price, money, null,
            OperationSource.Manual, null, null);
        var operation = new InvestmentOperation();
        operation.ApplyAll(InvestmentOperation.Decide(null, account, command, Now));
        return operation;
    }
}
