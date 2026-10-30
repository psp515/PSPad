using System.Globalization;

namespace PSPad.App.State.Investments;

public static class InvestmentText
{
    const string Minus = "−";

    public static string Money(decimal amount, string currency) => $"{MoneyText.Amount(amount)} {currency}";

    public static string Signed(decimal amount) => MoneyText.SignedAmount(amount);

    public static string Signed(decimal amount, string currency) => $"{MoneyText.SignedAmount(amount)} {currency}";

    public static string Percent(decimal? share)
    {
        if (share is not { } value)
        {
            return "—";
        }

        var rounded = Math.Round(value * 100, 1, MidpointRounding.AwayFromZero);
        var text = Math.Abs(rounded).ToString("0.0", CultureInfo.CurrentCulture) + "%";
        return rounded switch
        {
            < 0 => Minus + text,
            > 0 => "+" + text,
            _ => text
        };
    }

    public static string Quantity(decimal quantity) => quantity.ToString("#,0.########", CultureInfo.CurrentCulture);

    public static string Price(decimal price) => price.ToString("#,0.00######", CultureInfo.CurrentCulture);

    public static string Day(DateOnly day) => day.ToString("d MMM yyyy", CultureInfo.CurrentCulture);

    public static string Tone(decimal? value) => value switch
    {
        < 0 => "pspad-loss",
        > 0 => "pspad-gain",
        _ => ""
    };
}
