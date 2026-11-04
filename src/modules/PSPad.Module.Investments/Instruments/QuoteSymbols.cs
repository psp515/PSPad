namespace PSPad.Module.Investments.Instruments;

public static class QuoteSymbols
{
    static readonly (string Broker, string Yahoo)[] Suffixes =
    [
        (".PL", ".WA"), (".UK", ".L"), (".US", ""), (".DE", ".DE"), (".FR", ".PA"), (".NL", ".AS")
    ];

    public static string DefaultFor(string brokerTicker)
    {
        var ticker = brokerTicker.Trim().ToUpperInvariant();
        foreach (var (broker, yahoo) in Suffixes)
        {
            if (ticker.EndsWith(broker, StringComparison.Ordinal))
            {
                return ticker[..^broker.Length] + yahoo;
            }
        }

        return ticker;
    }
}
