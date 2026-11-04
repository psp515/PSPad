using PSPad.Abstractions;

namespace PSPad.Module.Money.Values;

public static class Currencies
{
    public const string Pln = "PLN";

    public static IReadOnlyList<string> All { get; } =
    [
        Pln, "AUD", "BGN", "BRL", "CAD", "CHF", "CNY", "CZK", "DKK", "EUR", "GBP", "HKD", "HUF", "ILS",
        "INR", "ISK", "JPY", "KRW", "MXN", "NOK", "NZD", "RON", "SEK", "SGD", "THB", "TRY", "UAH", "USD", "ZAR"
    ];

    static readonly HashSet<string> Known = [.. All];

    public static string Require(string code)
    {
        var normalised = code.Trim().ToUpperInvariant();
        return Known.Contains(normalised)
            ? normalised
            : throw new DomainRejectedException($"{normalised} is not a currency PSPad knows.");
    }
}
