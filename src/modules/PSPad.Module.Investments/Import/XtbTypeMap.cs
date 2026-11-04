using PSPad.Module.Investments.Operations;

namespace PSPad.Module.Investments.Import;

public static class XtbTypeMap
{
    static readonly Dictionary<string, (XtbRowKind Out, XtbRowKind In)> Kinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Stock purchase"] = (XtbRowKind.Buy, XtbRowKind.Buy),
        ["Stock sell"] = (XtbRowKind.Sell, XtbRowKind.Sell),
        ["Deposit"] = (XtbRowKind.Deposit, XtbRowKind.Deposit),
        ["IKZE deposit"] = (XtbRowKind.Deposit, XtbRowKind.Deposit),
        ["Withdrawal"] = (XtbRowKind.Withdrawal, XtbRowKind.Withdrawal),
        ["IKE deposit"] = (XtbRowKind.TransferOut, XtbRowKind.TransferIn),
        ["Subaccount transfer"] = (XtbRowKind.TransferOut, XtbRowKind.TransferIn),
        ["Transfer"] = (XtbRowKind.TransferOut, XtbRowKind.TransferIn),
        ["Dividend"] = (XtbRowKind.Dividend, XtbRowKind.Dividend),
        ["Withholding tax"] = (XtbRowKind.Tax, XtbRowKind.TaxRefund),
        ["Free funds interest"] = (XtbRowKind.Interest, XtbRowKind.Interest),
        ["Free funds interest tax"] = (XtbRowKind.Tax, XtbRowKind.Tax),
        ["Commission"] = (XtbRowKind.Fee, XtbRowKind.Fee),
        ["SEC fee"] = (XtbRowKind.Fee, XtbRowKind.Fee),
        ["Stamp duty"] = (XtbRowKind.Fee, XtbRowKind.Fee),
        ["Tax IFTT"] = (XtbRowKind.Fee, XtbRowKind.Fee),
    };

    static readonly Dictionary<string, TransferType> Transfers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["IKE deposit"] = TransferType.Ike,
        ["Subaccount transfer"] = TransferType.Subaccount,
        ["Transfer"] = TransferType.CurrencyConversion,
    };

    public static XtbRowKind? KindOf(string type, decimal? amount) =>
        Kinds.TryGetValue(type.Trim(), out var kinds) ? amount < 0 ? kinds.Out : kinds.In : null;

    public static TransferType? TransferTypeOf(string type) =>
        Transfers.TryGetValue(type.Trim(), out var transfer) ? transfer : null;

    public static bool SignDecidesKind(string type) =>
        Kinds.TryGetValue(type.Trim(), out var kinds) && kinds.Out != kinds.In;
}
