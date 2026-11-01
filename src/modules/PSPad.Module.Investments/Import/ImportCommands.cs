using System.Globalization;
using PSPad.Module.Investments.Operations;
using PSPad.Module.Money.Values;
using MoneyValue = PSPad.Module.Money.Money;

namespace PSPad.Module.Investments.Import;

internal static class ImportCommands
{
    public static long XtbId(XtbRow row) => long.Parse(row.Id!, NumberStyles.None, CultureInfo.InvariantCulture);

    public static OperationKind KindOf(XtbRow row) => Enum.Parse<OperationKind>(row.Kind!.Value.ToString());

    public static DateOnly DayOf(DateTimeOffset at, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime);

    public static bool HoldsTicker(OperationKind kind) =>
        kind is OperationKind.Buy or OperationKind.Sell or OperationKind.Dividend or OperationKind.Tax
            or OperationKind.TaxRefund or OperationKind.Fee;

    public static string? TickerOf(XtbRow row) =>
        HoldsTicker(KindOf(row)) && !string.IsNullOrWhiteSpace(row.Ticker) ? row.Ticker.Trim().ToUpperInvariant() : null;

    public static RecordInvestmentOperation Record(
        XtbRow row, Guid commandId, Guid userId, Guid accountId, string currency, DateOnly day,
        (decimal Rate, DateOnly RateDate) rate)
    {
        var kind = KindOf(row);
        var traded = kind is OperationKind.Buy or OperationKind.Sell;
        var money = currency == Currencies.Pln
            ? new MoneyValue(row.Amount!.Value, Currencies.Pln, 1m, day)
            : new MoneyValue(row.Amount!.Value, currency, rate.Rate, rate.RateDate);
        var xtbId = XtbId(row);
        return new RecordInvestmentOperation(commandId, userId, InvestmentOperation.IdFor(accountId, xtbId), accountId,
            kind, row.At!.Value, TickerOf(row), traded ? row.Quantity : null,
            traded ? row.Price : null, money, traded ? row.PositionId : null, OperationSource.Xtb,
            xtbId.ToString(CultureInfo.InvariantCulture), null, row.TransferType, row.Counterparty,
            CounterMoneyOf(row, money, day));
    }

    static MoneyValue? CounterMoneyOf(XtbRow row, MoneyValue money, DateOnly day)
    {
        if (row is not { TransferType: TransferType.CurrencyConversion, CounterAmount: > 0, CounterCurrency: { } currency })
        {
            return null;
        }

        var amount = row.CounterAmount.Value;
        var rate = currency == Currencies.Pln
            ? 1m
            : Math.Round(money.Amount * money.RateToPln / amount, 6, MidpointRounding.AwayFromZero);
        return new MoneyValue(amount, currency, rate, day);
    }
}
