namespace PSPad.Contracts;

public sealed record QuoteSeriesView(
    string Symbol,
    string Currency,
    IReadOnlyList<QuoteCloseView> Closes,
    QuoteLatestView? Latest,
    DateTimeOffset FetchedAt,
    bool Stale);
