using System.Globalization;
using System.Text.RegularExpressions;

namespace PSPad.App.State;

public static partial class TimeText
{
    [GeneratedRegex(@"^(\d{1,2})(?:[:.,]?(\d{2}))?$", RegexOptions.CultureInvariant)]
    private static partial Regex Typed();

    public static bool TryParse(string? text, out TimeOnly? time)
    {
        time = null;
        var typed = text?.Trim() ?? "";

        if (typed.Length == 0)
        {
            return true;
        }

        var match = Typed().Match(typed);

        if (!match.Success)
        {
            return false;
        }

        var hour = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var minute = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 0;

        if (hour > 23 || minute > 59)
        {
            return false;
        }

        time = new TimeOnly(hour, minute);
        return true;
    }
}
