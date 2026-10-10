using System.Net;
using System.Net.Http.Json;
using PSPad.App.Api;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Api;

[UnitTest]
public class NbpRatesClientTests
{
    static readonly DateOnly Day = new(2026, 10, 9);

    sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? Last { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Last = request;
            return Task.FromResult(respond(request));
        }
    }

    static INbpRates ClientWith(Stub stub) =>
        new PSPadApiClient(new HttpClient(stub) { BaseAddress = new Uri("http://localhost/") });

    [Fact]
    public async Task ItAsksForTheCurrencyOnTheDate()
    {
        var stub = new Stub(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new NbpRateView("EUR", 4.2512m, Day))
        });

        var rate = await ClientWith(stub).RateAsync("EUR", Day);

        Assert.Equal(new NbpRateView("EUR", 4.2512m, Day), rate);
        Assert.Equal("/api/money/nbp-rate?currency=EUR&date=2026-10-09", stub.Last!.RequestUri!.PathAndQuery);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task AFailedLookupIsNull(HttpStatusCode status) =>
        Assert.Null(await ClientWith(new Stub(_ => new HttpResponseMessage(status))).RateAsync("EUR", Day));

    [Fact]
    public async Task AnUnreachableServerIsNull() =>
        Assert.Null(await ClientWith(new Stub(_ => throw new HttpRequestException("down"))).RateAsync("EUR", Day));
}
