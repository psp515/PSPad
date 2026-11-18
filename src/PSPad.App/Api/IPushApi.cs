using PSPad.Contracts;

namespace PSPad.App.Api;

public interface IPushApi
{
    Task<string?> PushKeyAsync();

    Task<bool> SubscribeAsync(PushSubscriptionRequest request);

    Task<bool> UnsubscribeAsync(string endpoint);

    Task<bool> SendTestAsync(string endpoint);
}
