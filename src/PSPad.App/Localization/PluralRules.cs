using System.Globalization;

namespace PSPad.App.Localization;

public static class PluralRules
{
    public static PluralForm For(int count, CultureInfo culture) =>
        culture.TwoLetterISOLanguageName == "pl" ? Polish(count) : English(count);

    static PluralForm English(int count) => count == 1 ? PluralForm.One : PluralForm.Many;

    static PluralForm Polish(int count)
    {
        var tens = count % 100;
        var ones = count % 10;
        if (count == 1)
        {
            return PluralForm.One;
        }

        return ones is >= 2 and <= 4 && tens is < 12 or > 14 ? PluralForm.Few : PluralForm.Many;
    }
}
