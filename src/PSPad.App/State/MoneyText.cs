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
}
