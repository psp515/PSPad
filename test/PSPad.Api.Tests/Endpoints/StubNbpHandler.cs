using System.Net;
using System.Net.Http.Json;

namespace PSPad.Api.Tests.Endpoints;

public sealed class StubNbpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];

    public static HttpResponseMessage Rates(params (string Date, decimal Mid)[] rates) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                table = "A",
                currency = "euro",
                code = "EUR",
                rates = rates.Select(rate => new { no = "195/A/NBP/2026", effectiveDate = rate.Date, mid = rate.Mid })
            })
        };

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        await Task.Yield();
        return respond(request);
    }
}
