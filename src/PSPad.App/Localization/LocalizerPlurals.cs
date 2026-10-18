using System.Globalization;
using Microsoft.Extensions.Localization;

namespace PSPad.App.Localization;

public static class LocalizerPlurals
{
    public static string Plural(this IStringLocalizer localizer, int count, string key)
    {
        if (count == 0)
        {
            var zero = localizer[key + ".Zero"];
            if (!zero.ResourceNotFound)
            {
                return zero.Value;
            }
        }

        var form = PluralRules.For(count, CultureInfo.CurrentUICulture);
        var chosen = localizer[$"{key}.{form}"];
        if (chosen.ResourceNotFound && form == PluralForm.Few)
        {
            chosen = localizer[key + ".Many"];
        }

        return string.Format(CultureInfo.CurrentCulture, chosen.Value, count);
    }
}
