using PSPad.Contracts;

namespace PSPad.App.Api;

public interface INbpRates
{
    Task<NbpRateView?> RateAsync(string currency, DateOnly date);
}
