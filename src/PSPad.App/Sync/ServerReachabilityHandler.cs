namespace PSPad.App.Sync;

public sealed class ServerReachabilityHandler(ServerReachability reachability, IConnectivity connectivity)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await base.SendAsync(request, cancellationToken);

            // Any answer at all proves the server is there; a 401 is the server talking.
            reachability.Succeeded();

            return response;
        }
        catch (HttpRequestException)
        {
            reachability.Failed(connectivity.IsOnline);

            throw;
        }
    }
}
