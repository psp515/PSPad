using PSPad.Contracts;

namespace PSPad.App.State.Investments;

public sealed record StoredRates(IReadOnlyList<NbpRateView> Rates, DateOnly? From);
