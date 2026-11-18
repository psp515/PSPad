using PSPad.App.Api;
using PSPad.Contracts;

namespace PSPad.App.Tests.Push;

public sealed class FakePushApi : IPushApi
{
    public List<PushSubscriptionRequest> Subscribed { get; } = [];

    public Task<string?> PushKeyAsync() => Task.FromResult<string?>("key");

    public Task<bool> SubscribeAsync(PushSubscriptionRequest request)
    {
        Subscribed.Add(request);
        return Task.FromResult(true);
    }

    public Task<bool> UnsubscribeAsync(string endpoint) => Task.FromResult(true);

    public Task<bool> SendTestAsync(string endpoint) => Task.FromResult(true);
}
