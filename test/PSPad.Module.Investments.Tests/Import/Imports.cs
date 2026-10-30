using System.Globalization;
using PSPad.Abstractions;
using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Import;
using PSPad.Module.Investments.Operations;
using PSPad.Module.Investments.Portfolio;

namespace PSPad.Module.Investments.Tests.Import;

internal static class Imports
{
    public const string Number = "12345678";

    public static readonly TimeZoneInfo Warsaw = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");

    public static readonly DateTimeOffset Start = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    public static readonly Guid NewAccountId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public static XtbFile FileWith(params XtbRow[] rows) => new(Number, "IKE", rows, null);

    public static XtbRow Deposit(int sheetRow, decimal amount, long id, DateTimeOffset? at = null) =>
        new(sheetRow, "IKE deposit", XtbRowKind.Deposit, null, null, at ?? Start.AddMinutes(sheetRow), amount, Id(id),
            "IKE deposit", "IKE", null, null, null, []);

    public static XtbRow Purchase(
        int sheetRow, long id, string ticker, string position, decimal quantity, decimal price, decimal amount,
        string instrument = "Acme Works SA", DateTimeOffset? at = null) =>
        new(sheetRow, "Stock purchase", XtbRowKind.Buy, instrument, ticker, at ?? Start.AddMinutes(sheetRow), amount,
            Id(id), $"OPEN BUY {ticker} {quantity} @ {price}", "IKE", position, quantity, price, []);

    public static XtbRow Sale(
        int sheetRow, long id, string ticker, string position, decimal quantity, decimal price, decimal amount,
        DateTimeOffset? at = null) =>
        new(sheetRow, "Stock sell", XtbRowKind.Sell, "Acme Works SA", ticker, at ?? Start.AddMinutes(sheetRow), amount,
            Id(id), $"CLOSE BUY {ticker} {quantity} @ {price}", "IKE", position, quantity, price, []);

    public static XtbRow Commission(int sheetRow, long id, string ticker, decimal amount) =>
        new(sheetRow, "Commission", XtbRowKind.Fee, "Acme Works SA", ticker, Start.AddMinutes(sheetRow), amount, Id(id),
            "Commission", "IKE", null, null, null, []);

    public static XtbRow Broken(int sheetRow, string type, string error) =>
        new(sheetRow, type, null, null, null, Start.AddMinutes(sheetRow), null, null, null, "IKE", null, null, null, [error]);

    public static InvestmentImportPreview Preview(
        XtbFile file, InvestmentAccount? target = null, IReadOnlyList<InvestmentAccount>? accounts = null,
        IReadOnlyList<InvestmentOperation>? operations = null, InvestmentPortfolio? portfolio = null,
        string currency = "PLN", IReadOnlyDictionary<ImportRateKey, (decimal Rate, DateOnly RateDate)>? rates = null,
        IReadOnlySet<int>? unticked = null, IReadOnlySet<int>? tickedDeleted = null) =>
        InvestmentImportPreviewer.Preview(file, target, accounts ?? (target is null ? [] : [target]), operations ?? [],
            portfolio, currency, Warsaw, rates ?? NoRates, unticked ?? new HashSet<int>(),
            tickedDeleted ?? new HashSet<int>());

    public static readonly IReadOnlyDictionary<ImportRateKey, (decimal Rate, DateOnly RateDate)> NoRates =
        new Dictionary<ImportRateKey, (decimal Rate, DateOnly RateDate)>();

    public static IReadOnlyList<ICommand> Plan(
        InvestmentImportPreview preview, string name = "IKE 12345678", string currency = "PLN",
        IReadOnlyDictionary<ImportRateKey, (decimal Rate, DateOnly RateDate)>? rates = null) =>
        InvestmentImportPlan.Commands(preview, Given.Owner, NewAccountId, name, currency, rates ?? NoRates, Guid.NewGuid);

    public static InvestmentAccount Counted(InvestmentAccount account, int operations)
    {
        account.ApplyAll(Enumerable.Range(0, operations)
            .Select(_ => (DomainEvent)new AccountOperationCounted(account.Id, Given.Owner, Given.Now)).ToList());
        return account;
    }

    static string Id(long id) => id.ToString(CultureInfo.InvariantCulture);
}
