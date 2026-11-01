namespace PSPad.Module.Investments.Import;

public sealed record XtbFile(
    string AccountNumber, string? Product, IReadOnlyList<XtbRow> Rows, IReadOnlyList<XtbOpenPosition>? OpenPositions)
{
    public IReadOnlyDictionary<string, string> ProductNumbers { get; init; } = new Dictionary<string, string>();

    public string AccountNumberOf(XtbRow row) =>
        row.Product is { } product && ProductNumbers.TryGetValue(product, out var number) ? number : AccountNumber;

    public IReadOnlyList<string> AccountNumbers =>
        [AccountNumber, .. Rows.Select(AccountNumberOf).Where(number => number != AccountNumber).Distinct(StringComparer.Ordinal)];

    public string? ProductOf(string accountNumber) =>
        accountNumber == AccountNumber
            ? Product
            : Rows.Where(row => AccountNumberOf(row) == accountNumber).Select(row => row.Product).FirstOrDefault(product => product is not null);
}
