namespace PSPad.Module.Investments.Import;

public static class XtbTypeMap
{
    static readonly Dictionary<string, XtbRowKind> Kinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Stock purchase"] = XtbRowKind.Buy,
        ["Stock sell"] = XtbRowKind.Sell,
        ["Deposit"] = XtbRowKind.Deposit,
        ["IKE deposit"] = XtbRowKind.Deposit,
        ["IKZE deposit"] = XtbRowKind.Deposit,
        ["Withdrawal"] = XtbRowKind.Withdrawal,
        ["Dividend"] = XtbRowKind.Dividend,
        ["Withholding tax"] = XtbRowKind.Tax,
        ["Free funds interest"] = XtbRowKind.Interest,
        ["Commission"] = XtbRowKind.Fee,
        ["SEC fee"] = XtbRowKind.Fee,
    };

    public static XtbRowKind? KindOf(string type) =>
        Kinds.TryGetValue(type.Trim(), out var kind) ? kind : null;
}
