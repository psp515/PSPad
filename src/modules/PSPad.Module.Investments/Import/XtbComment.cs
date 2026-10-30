using System.Globalization;
using System.Text.RegularExpressions;

namespace PSPad.Module.Investments.Import;

public sealed partial record XtbComment(decimal Quantity, decimal Price)
{
    [GeneratedRegex(@"^(OPEN|CLOSE)\s+BUY\s+\S+\s+(?<q>\d+(\.\d+)?)\s+@\s+(?<p>\d+(\.\d+)?)$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    public static XtbComment? Parse(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
            return null;
        var match = Pattern().Match(comment.Trim());
        if (!match.Success)
            return null;
        var ok = decimal.TryParse(match.Groups["q"].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var quantity)
            & decimal.TryParse(match.Groups["p"].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var price);
        return ok ? new XtbComment(quantity, price) : null;
    }
}
