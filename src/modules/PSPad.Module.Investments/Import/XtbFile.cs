using PSPad.Module.Money.Values;

namespace PSPad.Module.Investments.Import;

public sealed record XtbFile(
    string AccountNumber, string? Product, IReadOnlyList<XtbRow> Rows, IReadOnlyList<XtbOpenPosition>? OpenPositions)
{
    public IReadOnlyDictionary<string, string> ProductNumbers { get; init; } = new Dictionary<string, string>();

    public IReadOnlyDictionary<string, string> ProductCurrencies { get; init; } = new Dictionary<string, string>();

    public IReadOnlyDictionary<string, string> ConversionCurrencies { get; init; } = new Dictionary<string, string>();

    public string? FileNameCurrency { get; init; }

    public XtbFile NamedAfter(string? fileName) => this with { FileNameCurrency = CurrencyInName(fileName) };

    public string? CurrencyOf(string accountNumber) =>
        ProductOf(accountNumber) is { } product && ProductCurrencies.TryGetValue(product, out var summarised) ? summarised
        : ConversionCurrencies.TryGetValue(accountNumber, out var converted) ? converted
        : accountNumber == AccountNumber ? FileNameCurrency
        : null;

    static string? CurrencyInName(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? "");
        var end = name.IndexOf('_', StringComparison.Ordinal);
        var prefix = end > 0 ? name[..end].ToUpperInvariant() : null;
        return prefix is not null && Currencies.All.Contains(prefix) ? prefix : null;
    }

    public string AccountNumberOf(XtbRow row) =>
        row.Product is { } product && ProductNumbers.TryGetValue(product, out var number) ? number : AccountNumber;

    public IReadOnlyList<string> AccountNumbers =>
        [AccountNumber, .. Rows.Select(AccountNumberOf).Where(number => number != AccountNumber).Distinct(StringComparer.Ordinal)];

    public string? ProductOf(string accountNumber) =>
        accountNumber == AccountNumber
            ? Product
            : Rows.Where(row => AccountNumberOf(row) == accountNumber).Select(row => row.Product).FirstOrDefault(product => product is not null);
}
