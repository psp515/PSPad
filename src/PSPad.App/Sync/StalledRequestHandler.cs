namespace PSPad.App.Sync;

public sealed class StalledRequestHandler(TimeSpan limit) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        stall.CancelAfter(limit);

        try
        {
            return await base.SendAsync(request, stall.Token);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Slow Wi-Fi answers nothing rather than failing; callers already treat a transport failure as offline.
            throw new HttpRequestException($"No answer within {limit.TotalSeconds:0} s.", exception);
        }
    }
}
