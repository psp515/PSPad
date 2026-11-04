using PSPad.Contracts;

namespace PSPad.App.State.Investments;

public sealed record StoredQuote(QuoteSeriesView Series, DateOnly From);
