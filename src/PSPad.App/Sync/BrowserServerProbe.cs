using Microsoft.JSInterop;

namespace PSPad.App.Sync;

public sealed class BrowserServerProbe(IJSRuntime js, IConfiguration configuration) : IServerProbe
{
    public async Task<bool> ReachableAsync()
    {
        try
        {
            var health = new Uri(new Uri(configuration["Api:BaseAddress"]!), "health").ToString();
            var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/connectivity.js");
            return await module.InvokeAsync<bool>("probe", health);
        }
        catch (JSException)
        {
            return false;
        }
    }
}
