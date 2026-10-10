namespace PSPad.Api.Rates;

public sealed record NbpSeries(string Code, IReadOnlyList<NbpRate> Rates);
