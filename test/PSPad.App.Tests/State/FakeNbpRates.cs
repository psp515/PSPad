using PSPad.App.Api;
using PSPad.Contracts;

namespace PSPad.App.Tests.State;

public sealed class FakeNbpRates : INbpRates
{
    public NbpRateView? Next { get; set; }

    public Dictionary<string, NbpRateView?> ByCurrency { get; } = [];

    public TaskCompletionSource<NbpRateView?>? Gate { get; set; }

    public Dictionary<string, TaskCompletionSource<NbpRateView?>> GatesByCurrency { get; } = [];

    public List<(string Currency, DateOnly Date)> Asked { get; } = [];

    public Task<NbpRateView?> RateAsync(string currency, DateOnly date)
    {
        Asked.Add((currency, date));
        return GatesByCurrency.TryGetValue(currency, out var gate) ? gate.Task : Gate?.Task ?? Task.FromResult(ByCurrency.TryGetValue(currency, out var rate) ? rate : Next);
    }
}
