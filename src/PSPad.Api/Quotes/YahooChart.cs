using PSPad.Contracts;

namespace PSPad.Api.Quotes;

public sealed record YahooChart(string Currency, IReadOnlyList<QuoteCloseView> Closes, QuoteLatestView? Latest);
