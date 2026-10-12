using System.Globalization;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Values;

namespace PSPad.App.State;

public static class MoneyText
{
    const string Minus = "−";

    public static string Amount(decimal value) => value.ToString("N2", CultureInfo.InvariantCulture);

    public static string Pln(decimal value) => $"{Amount(value)} {Currencies.Pln}";

    public static string SignedPln(decimal value) => value switch
    {
        < 0 => $"{Minus}{Pln(-value)}",
        > 0 => $"+{Pln(value)}",
        _ => Pln(0)
    };

    public static string Signed(CategoryKind kind, decimal value, string currency) =>
        $"{(kind == CategoryKind.Expense ? Minus : "+")}{Amount(value)} {currency}";

    public static string Rate(decimal rate) => rate.ToString("0.0000##", CultureInfo.InvariantCulture);

    public static string MonthTitle(YearMonth month) =>
        month.FirstDay.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

    public static string Day(DateOnly date) => date.ToString("dddd, d MMMM", CultureInfo.InvariantCulture);

    public static string Range(DateOnly from, DateOnly to) =>
        $"{from.ToString("d MMM yyyy", CultureInfo.InvariantCulture)} – {to.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}";

    public static string Percent(decimal? share)
    {
        if (share is not { } value)
        {
            return "—";
        }

        var whole = WholePercent(value);
        return whole < 0
            ? $"{Minus}{(-whole).ToString(CultureInfo.InvariantCulture)}%"
            : $"{whole.ToString(CultureInfo.InvariantCulture)}%";
    }

    public static bool PercentIsNegative(decimal? share) => share is { } value && WholePercent(value) < 0;

    static long WholePercent(decimal share) => (long)Math.Round(share * 100, 0, MidpointRounding.AwayFromZero);

    public static string[] MonthAxis(IReadOnlyList<YearMonth> months)
    {
        if (months.Count == 0)
        {
            return [];
        }

        var oneYear = months[0].Year == months[^1].Year;
        var format = oneYear ? "MMM" : "MMM yyyy";
        var step = oneYear ? 1 : (int)Math.Ceiling(months.Count / 6d);
        var labels = new string[months.Count];

        for (var index = 0; index < months.Count; index++)
        {
            labels[index] = index % step == 0 ? months[index].FirstDay.ToString(format, CultureInfo.InvariantCulture) : "";
        }

        return labels;
    }
}
