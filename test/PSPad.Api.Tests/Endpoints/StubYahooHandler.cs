using System.Net;
using System.Net.Http.Json;

namespace PSPad.Api.Tests.Endpoints;

public sealed class StubYahooHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];

    public static HttpResponseMessage Chart(string currency, int gmtoffset, params (long Ts, decimal? Close)[] closes) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                chart = new
                {
                    result = new[]
                    {
                        new
                        {
                            meta = new
                            {
                                currency,
                                gmtoffset,
                                regularMarketPrice = closes.Last(close => close.Close is not null).Close,
                                regularMarketTime = closes.Last().Ts
                            },
                            timestamp = closes.Select(close => close.Ts),
                            indicators = new { quote = new[] { new { close = closes.Select(close => close.Close) } } }
                        }
                    },
                    error = (object?)null
                }
            })
        };

    public static HttpResponseMessage NotFoundChart() =>
        new(HttpStatusCode.NotFound)
        {
            Content = JsonContent.Create(new
            {
                chart = new { result = (object?)null, error = new { code = "Not Found", description = "No data found" } }
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
