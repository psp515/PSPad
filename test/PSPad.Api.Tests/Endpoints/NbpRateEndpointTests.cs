using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PSPad.Api.Rates;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Endpoints;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class NbpRateEndpointTests(MongoFixture fixture)
{
    const string Friday = "/api/money/nbp-rate?currency=eur&date=2026-10-09";

    ApiFactory FactoryWith(StubNbpHandler nbp) =>
        new(fixture, testServices: services =>
            services.AddHttpClient<NbpRateClient>().ConfigurePrimaryHttpMessageHandler(() => nbp));

    [Fact]
    public async Task ItReturnsTheRateForTheDate()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var nbp = new StubNbpHandler(_ => StubNbpHandler.Rates(("2026-10-08", 4.2490m), ("2026-10-09", 4.2512m)));
        await using var factory = FactoryWith(nbp);

        var rate = await factory.ClientFor(Guid.NewGuid().ToString()).GetFromJsonAsync<NbpRateView>(Friday, ct);

        Assert.Equal(new NbpRateView("EUR", 4.2512m, new DateOnly(2026, 10, 9)), rate);
        Assert.Equal("https://api.nbp.pl/api/exchangerates/rates/A/EUR/2026-10-02/2026-10-09/?format=json",
            Assert.Single(nbp.Requests).AbsoluteUri);
    }

    [Fact]
    public async Task AWeekendDateGetsTheLastRateBeforeIt()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var nbp = new StubNbpHandler(_ => StubNbpHandler.Rates(("2026-10-08", 4.2490m), ("2026-10-09", 4.2512m)));
        await using var factory = FactoryWith(nbp);

        var rate = await factory.ClientFor(Guid.NewGuid().ToString())
            .GetFromJsonAsync<NbpRateView>("/api/money/nbp-rate?currency=EUR&date=2026-10-11", ct);

        Assert.Equal(new DateOnly(2026, 10, 9), rate!.EffectiveDate);
        Assert.EndsWith("/A/EUR/2026-10-04/2026-10-11/?format=json", Assert.Single(nbp.Requests).AbsoluteUri);
    }

    [Fact]
    public async Task NoRateFromNbpGives404()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = FactoryWith(new StubNbpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));

        var response = await factory.ClientFor(Guid.NewGuid().ToString()).GetAsync(Friday, ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AnUnknownCurrencyGives404WithoutCallingNbp()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var nbp = new StubNbpHandler(_ => StubNbpHandler.Rates(("2026-10-09", 1m)));
        await using var factory = FactoryWith(nbp);

        var response = await factory.ClientFor(Guid.NewGuid().ToString())
            .GetAsync("/api/money/nbp-rate?currency=XXX&date=2026-10-09", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(nbp.Requests);
    }

    [Fact]
    public async Task ATimeoutGives502()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = FactoryWith(new StubNbpHandler(_ =>
            throw new TaskCanceledException("NBP timed out.", new TimeoutException())));

        var response = await factory.ClientFor(Guid.NewGuid().ToString()).GetAsync(Friday, ct);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task AnNbpServerErrorGives502()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = FactoryWith(new StubNbpHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        var response = await factory.ClientFor(Guid.NewGuid().ToString()).GetAsync(Friday, ct);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task ASecondLookupIsServedFromTheCache()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var nbp = new StubNbpHandler(_ => StubNbpHandler.Rates(("2026-10-09", 4.2512m)));
        await using var factory = FactoryWith(nbp);
        var client = factory.ClientFor(Guid.NewGuid().ToString());

        await client.GetFromJsonAsync<NbpRateView>(Friday, ct);
        var again = await client.GetFromJsonAsync<NbpRateView>("/api/money/nbp-rate?currency=EUR&date=2026-10-09", ct);

        Assert.Equal(4.2512m, again!.Rate);
        Assert.Single(nbp.Requests);
    }

    [Fact]
    public async Task PlnIsOneAndNeverCallsNbp()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var nbp = new StubNbpHandler(_ => StubNbpHandler.Rates(("2026-10-09", 9m)));
        await using var factory = FactoryWith(nbp);

        var rate = await factory.ClientFor(Guid.NewGuid().ToString())
            .GetFromJsonAsync<NbpRateView>("/api/money/nbp-rate?currency=PLN&date=2026-10-09", ct);

        Assert.Equal(new NbpRateView("PLN", 1m, new DateOnly(2026, 10, 9)), rate);
        Assert.Empty(nbp.Requests);
    }

    [Fact]
    public async Task ItRequiresSignIn()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var nbp = new StubNbpHandler(_ => StubNbpHandler.Rates(("2026-10-09", 4.2512m)));
        await using var factory = FactoryWith(nbp);

        var response = await factory.CreateClient().GetAsync(Friday, ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(nbp.Requests);
    }

    [Fact]
    public async Task TheRealClientTargetsNbpAndGivesUpAfterFiveSeconds()
    {
        await using var factory = new ApiFactory(fixture);

        var http = factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(NbpRateClient));

        Assert.Equal(new Uri("https://api.nbp.pl/"), http.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(5), http.Timeout);
    }
}
