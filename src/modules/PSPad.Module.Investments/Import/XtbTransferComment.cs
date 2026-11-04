using System.Globalization;
using System.Text.RegularExpressions;

namespace PSPad.Module.Investments.Import;

public sealed partial record XtbTransferComment(
    string? From, string? To, string? SourceCurrency, string? TargetCurrency, decimal? Rate)
{
    [GeneratedRegex(@"^Transfer\s+(in|out)\s+operation\s+on\s+account\s+with\s+id\s+(?<n>\d+)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex Ike();

    [GeneratedRegex(@"^Transfer\s+from\s+(?<from>\d+)\s+to\s+(?<to>\d+)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex Subaccount();

    [GeneratedRegex(@"^Currency\s+conversion,\s*(?<src>[A-Z]{3})\s+to\s+(?<dst>[A-Z]{3})\s+from\s+TA:\s*(?<from>\d+)\s+to:\s*(?<to>\d+),\s*Exchange\s+rate:\s*(?<rate>\d+(\.\d+)?)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex Conversion();

    public static XtbTransferComment? Parse(string? comment)
    {
        var text = comment?.Trim() ?? "";
        if (Ike().Match(text) is { Success: true } ike)
            return new XtbTransferComment(ike.Groups["n"].Value, ike.Groups["n"].Value, null, null, null);
        if (Subaccount().Match(text) is { Success: true } subaccount)
            return new XtbTransferComment(subaccount.Groups["from"].Value, subaccount.Groups["to"].Value, null, null, null);
        if (Conversion().Match(text) is not { Success: true } conversion)
            return null;
        return new XtbTransferComment(conversion.Groups["from"].Value, conversion.Groups["to"].Value,
            conversion.Groups["src"].Value.ToUpperInvariant(), conversion.Groups["dst"].Value.ToUpperInvariant(),
            decimal.TryParse(conversion.Groups["rate"].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture,
                out var rate) && rate > 0 ? rate : null);
    }

    public string? CounterpartyOf(bool outgoing) => outgoing ? To : From;

    public string? OwnNumberOf(bool outgoing) => outgoing ? From : To;

    public (decimal Amount, string Currency)? CounterOf(decimal amount, bool outgoing)
    {
        if (Rate is not { } rate || SourceCurrency is null || TargetCurrency is null)
            return null;
        var counter = outgoing ? amount * rate : amount / rate;
        return (Math.Round(counter, 2, MidpointRounding.AwayFromZero), outgoing ? TargetCurrency : SourceCurrency);
    }
}
