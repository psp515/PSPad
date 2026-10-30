using PSPad.Abstractions;
using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Instruments;
using PSPad.Module.Investments.Operations;
using PSPad.Module.Investments.Portfolio;
using MoneyValue = PSPad.Module.Money.Money;

namespace PSPad.Module.Investments.Tests;

internal static class Given
{
    public static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid Stranger = Guid.Parse("99999999-9999-9999-9999-999999999999");
    public static readonly Guid AccountId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly DateTimeOffset Now = new(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset TradedAt = new(2026, 10, 1, 14, 30, 0, TimeSpan.Zero);

    public static InvestmentAccount AnAccount(string currency = "PLN", Guid? id = null, string? number = null)
    {
        var account = new InvestmentAccount();
        account.ApplyAll(InvestmentAccount.Decide(null,
            new CreateInvestmentAccount(Guid.NewGuid(), Owner, id ?? Guid.NewGuid(), "IKE", currency, Broker.Xtb, number), Now));
        return account;
    }

    public static InvestmentAccount Archive(InvestmentAccount account)
    {
        account.ApplyAll(InvestmentAccount.Decide(account, new ArchiveInvestmentAccount(Guid.NewGuid(), Owner, account.Id), Now));
        return account;
    }

    public static InvestmentPortfolio APortfolioWith(int accounts = 0, int tickers = 0)
    {
        var portfolio = new InvestmentPortfolio();
        for (var index = 0; index < accounts; index++)
        {
            portfolio.ApplyAll(InvestmentPortfolio.Decide(portfolio,
                new CreateInvestmentAccount(Guid.NewGuid(), Owner, Guid.NewGuid(), "IKE", "PLN", Broker.Xtb, null), Now));
        }

        for (var index = 0; index < tickers; index++)
        {
            portfolio.ApplyAll(InvestmentPortfolio.Decide(portfolio,
                new EnsureInstrument(Guid.NewGuid(), Owner, $"T{index}.US", "Ticker"), Now));
        }

        return portfolio;
    }

    public static MoneyValue Cash(decimal amount, string currency = "PLN") =>
        new(amount, currency, currency == "PLN" ? 1m : 4m, DateOnly.FromDateTime(TradedAt.UtcDateTime));

    public static RecordInvestmentOperation ABuyCommand(
        InvestmentAccount account, Guid? id = null, string? ticker = "ABC.US", decimal? quantity = 2m, decimal? price = 50m,
        MoneyValue? money = null, OperationKind kind = OperationKind.Buy, Guid? userId = null) =>
        new(Guid.NewGuid(), userId ?? Owner, id ?? Guid.NewGuid(), account.Id, kind, TradedAt, ticker, quantity, price,
            money ?? Cash(100m, account.Currency), null, OperationSource.Manual, null, null);

    public static RecordInvestmentOperation ACashCommand(InvestmentAccount account, OperationKind kind, decimal amount = 100m) =>
        new(Guid.NewGuid(), Owner, Guid.NewGuid(), account.Id, kind, TradedAt, null, null, null,
            Cash(amount, account.Currency), null, OperationSource.Manual, null, null);

    public static RecordInvestmentOperation ImportedBuyCommand(InvestmentAccount account, long xtbId) =>
        ABuyCommand(account, InvestmentOperation.IdFor(account.Id, xtbId)) with
        {
            Source = OperationSource.Xtb,
            ExternalId = xtbId.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

    public static InvestmentOperation Recorded(InvestmentAccount account, RecordInvestmentOperation command)
    {
        var operation = new InvestmentOperation();
        operation.ApplyAll(InvestmentOperation.Decide(null, account, command, Now));
        account.ApplyAll(InvestmentAccount.Count(account, null, command, Now));
        return operation;
    }

    public static InvestmentOperation AnImportedBuy(InvestmentAccount account, long xtbId) =>
        Recorded(account, ImportedBuyCommand(account, xtbId));

    public static InvestmentOperation Delete(InvestmentOperation operation, InvestmentAccount account)
    {
        operation.ApplyAll(InvestmentOperation.Decide(operation, account,
            new DeleteInvestmentOperation(Guid.NewGuid(), Owner, operation.Id), Now));
        return operation;
    }

    public static EditInvestmentOperation EditOf(InvestmentOperation operation) =>
        new(Guid.NewGuid(), Owner, operation.Id, operation.Kind, operation.At, operation.Ticker, operation.Quantity,
            operation.Price, operation.Money, operation.PositionId, operation.Note);

    static readonly DateOnly DayZero = new(2026, 8, 31);
    static int recordedSeconds;

    public static DateOnly Day(int number) => DayZero.AddDays(number);

    public static InvestmentOperation Deposit(InvestmentAccount account, decimal amount, DateOnly day, decimal rate = 4m) =>
        Operation(account, OperationKind.Deposit, day, null, null, null, amount, null, rate);

    public static InvestmentOperation Withdrawal(InvestmentAccount account, decimal amount, DateOnly day, decimal rate = 4m) =>
        Operation(account, OperationKind.Withdrawal, day, null, null, null, amount, null, rate);

    public static InvestmentOperation Buy(
        InvestmentAccount account, string ticker, string? position, decimal quantity, decimal price, decimal amount,
        DateOnly day, decimal rate = 4m) =>
        Operation(account, OperationKind.Buy, day, ticker, quantity, price, amount, position, rate);

    public static InvestmentOperation Sell(
        InvestmentAccount account, string ticker, string? position, decimal quantity, decimal price, decimal amount,
        DateOnly day, decimal rate = 4m) =>
        Operation(account, OperationKind.Sell, day, ticker, quantity, price, amount, position, rate);

    public static InvestmentOperation Dividend(
        InvestmentAccount account, string ticker, decimal amount, DateOnly day, decimal rate = 4m) =>
        Operation(account, OperationKind.Dividend, day, ticker, null, null, amount, null, rate);

    public static InvestmentOperation Tax(InvestmentAccount account, string ticker, decimal amount, DateOnly day, decimal rate = 4m) =>
        Operation(account, OperationKind.Tax, day, ticker, null, null, amount, null, rate);

    public static InvestmentOperation Interest(InvestmentAccount account, decimal amount, DateOnly day, decimal rate = 4m) =>
        Operation(account, OperationKind.Interest, day, null, null, null, amount, null, rate);

    public static InvestmentOperation Fee(InvestmentAccount account, decimal amount, DateOnly day, decimal rate = 4m) =>
        Operation(account, OperationKind.Fee, day, null, null, null, amount, null, rate);

    public static InvestmentOperation DepositAt(InvestmentAccount account, decimal amount, DateTimeOffset at) =>
        Operation(account, OperationKind.Deposit, at, null, null, null, amount, null, 4m);

    public static InvestmentOperation BuyAt(
        InvestmentAccount account, string ticker, decimal quantity, decimal price, DateTimeOffset at) =>
        Operation(account, OperationKind.Buy, at, ticker, quantity, price, quantity * price, "A", 4m);

    static InvestmentOperation Operation(
        InvestmentAccount account, OperationKind kind, DateOnly day, string? ticker, decimal? quantity, decimal? price,
        decimal amount, string? position, decimal rate) =>
        Operation(account, kind,
            new DateTimeOffset(day.ToDateTime(new TimeOnly(10, 0)), TimeSpan.Zero)
                .AddSeconds(Interlocked.Increment(ref recordedSeconds) % 3600),
            ticker, quantity, price, amount, position, rate);

    static InvestmentOperation Operation(
        InvestmentAccount account, OperationKind kind, DateTimeOffset at, string? ticker, decimal? quantity, decimal? price,
        decimal amount, string? position, decimal rate)
    {
        var day = DateOnly.FromDateTime(at.UtcDateTime);
        var money = new MoneyValue(amount, account.Currency, account.Currency == "PLN" ? 1m : rate, day);
        var operation = new InvestmentOperation();
        operation.ApplyAll(InvestmentOperation.Decide(null, account,
            new RecordInvestmentOperation(Guid.NewGuid(), Owner, Guid.NewGuid(), account.Id, kind, at, ticker, quantity,
                price, money, position, OperationSource.Manual, null, null), Now));
        return operation;
    }

    public static Instrument AnInstrument(string ticker = "ABC.US")
    {
        var instrument = new Instrument();
        instrument.ApplyAll(Instrument.Decide(null, new EnsureInstrument(Guid.NewGuid(), Owner, ticker, "Abc Corp"), Now));
        return instrument;
    }
}
