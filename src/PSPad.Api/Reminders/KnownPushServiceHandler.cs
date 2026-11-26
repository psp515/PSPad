namespace PSPad.Api.Reminders;

public sealed class KnownPushServiceHandler(PushServiceHosts hosts) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        request.RequestUri is { } uri && hosts.IsKnown(uri)
            ? base.SendAsync(request, cancellationToken)
            : throw new HttpRequestException($"Refusing to send to {request.RequestUri?.Host}: not a known push service.");
}
