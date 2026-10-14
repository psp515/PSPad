using System.Net;

namespace PSPad.App.Tests.Live;

public sealed class StubHttpHandler(HttpStatusCode status, string body = "") : HttpMessageHandler
{
    public HttpRequestMessage? Last { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Last = request;
        return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }
}
