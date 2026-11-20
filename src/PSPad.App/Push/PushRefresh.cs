using Microsoft.JSInterop;
using PSPad.App.Api;
using PSPad.App.State;
using PSPad.App.Sync;
using PSPad.Contracts;

namespace PSPad.App.Push;

public sealed class PushRefresh(IPushDevice device, IPushApi api, IConnectivity connectivity)
{
    public async Task RefreshAsync()
    {
        if (!connectivity.IsOnline)
        {
            return;
        }

        try
        {
            var support = await device.SupportAsync();
            if (PushState.Of(support) is not PushCardState.On || await device.CurrentAsync() is not { } current)
            {
                return;
            }

            await api.SubscribeAsync(new PushSubscriptionRequest(current.Endpoint, current.P256dh, current.Auth, support.Label));
        }
        catch (JSException)
        {
        }
    }
}
