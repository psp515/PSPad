using PSPad.App.State;
using PSPad.Contracts;

namespace PSPad.App.Api;

public sealed class TimeZoneHintHandler(BrowserTimeZone zone) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.TryAddWithoutValidation(TimeZoneHint.Header, zone.Id);
        return base.SendAsync(request, cancellationToken);
    }
}
