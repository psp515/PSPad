using System.Globalization;
using PSPad.Module.Money.Values;

namespace PSPad.Module.Money.Import;

internal static class CsvValues
{
    static readonly string[] DateFormats = ["yyyy-MM-dd", "dd.MM.yyyy"];
    static readonly string[] MonthFormats = ["yyyy-MM", "MM.yyyy"];

    public static decimal Amount(string text) =>
        Number(text, 2) ?? throw new FormatException("Amount must be a number with at most 2 decimals.");

    public static decimal? Rate(string text) =>
        text.Trim().Length == 0
            ? null
            : Number(text, 6) ?? throw new FormatException("Rate must be a number with at most 6 decimals.");

    public static DateOnly Date(string text) =>
        DateOnly.TryParseExact(text.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw new FormatException("Date must be yyyy-MM-dd or dd.MM.yyyy.");

    public static YearMonth Month(string text) =>
        DateOnly.TryParseExact(text.Trim(), MonthFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var first)
            ? YearMonth.Of(first)
            : throw new FormatException("Month must be yyyy-MM or MM.yyyy.");

    public static string? Currency(string text) =>
        text.Trim().Length == 0 ? null : Currencies.Require(text);

    static decimal? Number(string text, int maxDecimals)
    {
        var compact = string.Concat(text.Where(character => character is not (' ' or ' ' or ' ')));

        if (compact.Contains('.') && compact.Contains(','))
        {
            return null;
        }

        compact = compact.Replace(',', '.');
        var point = compact.IndexOf('.');

        if (point >= 0 && compact.Length - point - 1 > maxDecimals)
        {
            return null;
        }

        return decimal.TryParse(compact, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}
