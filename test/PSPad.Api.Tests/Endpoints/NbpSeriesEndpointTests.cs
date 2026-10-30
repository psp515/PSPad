using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PSPad.Abstractions;
using PSPad.Api.Rates;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Endpoints;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class NbpSeriesEndpointTests(MongoFixture fixture)
{
    sealed class TestClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    ApiFactory FactoryWith(StubNbpHandler nbp) =>
        new(fixture, testServices: services =>
        {
            services.AddHttpClient<NbpSeriesClient>().ConfigurePrimaryHttpMessageHandler(() => nbp);
            services.AddSingleton<IClock>(new TestClock(new DateTimeOffset(2026, 10, 30, 10, 0, 0, TimeSpan.Zero)));
        });

    const string Range = "/api/money/nbp-rates?currency=USD&from=2025-01-01&to=2025-07-19";

    [Fact]
    public async Task ALongRangeIsFetchedInChunksAndMergedInOrder()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var nbp = new StubNbpHandler(request =>
        {
            var from = request.RequestUri!.AbsolutePath.Split('/')[^3];
            return StubNbpHandler.Rates((from, 4m));
        });
        await using var factory = FactoryWith(nbp);

        var rates = await factory.ClientFor(Guid.NewGuid().ToString()).GetFromJsonAsync<NbpRateView[]>(Range, ct);

        Assert.Equal(3, nbp.Requests.Count);
        Assert.Equal(3, rates!.Length);
        Assert.Equal(rates.OrderBy(rate => rate.EffectiveDate), rates);
        Assert.All(rates, rate => Assert.Equal("USD", rate.Currency));
        Assert.Equal(new DateOnly(2025, 1, 1), rates[0].EffectiveDate);
    }

    [Fact]
    public async Task AFutureEndIsCutToTomorrow()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var nbp = new StubNbpHandler(_ => StubNbpHandler.Rates(("2026-10-02", 4m)));
        await using var factory = FactoryWith(nbp);

        await factory.ClientFor(Guid.NewGuid().ToString())
            .GetAsync("/api/money/nbp-rates?currency=USD&from=2026-10-01&to=2050-01-01", ct);

        var request = Assert.Single(nbp.Requests);
        Assert.Equal("2026-10-31", request.AbsolutePath.Split('/')[^2]);
    }

    [Fact]
    public async Task AChunkNbpHasNothingFor404sIsSkipped()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var calls = 0;
        var nbp = new StubNbpHandler(_ => ++calls switch
        {
            2 => new HttpResponseMessage(HttpStatusCode.NotFound),
            1 => StubNbpHandler.Rates(("2025-01-02", 4m)),
            _ => StubNbpHandler.Rates(("2025-07-08", 4m))
        });
        await using var factory = FactoryWith(nbp);

        var rates = await factory.ClientFor(Guid.NewGuid().ToString()).GetFromJsonAsync<NbpRateView[]>(Range, ct);

        Assert.Equal(3, nbp.Requests.Count);
        Assert.Equal([new DateOnly(2025, 1, 2), new DateOnly(2025, 7, 8)], rates!.Select(rate => rate.EffectiveDate));
    }

    [Fact]
    public async Task ATooManyRequestsAnswerGives502AndIsNotCached()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var limited = true;
        var nbp = new StubNbpHandler(_ => limited
            ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            : StubNbpHandler.Rates(("2025-01-02", 4m)));
        await using var factory = FactoryWith(nbp);
        var client = factory.ClientFor(Guid.NewGuid().ToString());

        var first = await client.GetAsync(Range, ct);
        limited = false;
        var second = await client.GetFromJsonAsync<NbpRateView[]>(Range, ct);

        Assert.Equal(HttpStatusCode.BadGateway, first.StatusCode);
        Assert.NotEmpty(second!);
    }

    [Fact]
    public async Task PlnIsEmptyAndNeverCallsNbp()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var nbp = new StubNbpHandler(_ => StubNbpHandler.Rates(("2025-01-02", 4m)));
        await using var factory = FactoryWith(nbp);

        var rates = await factory.ClientFor(Guid.NewGuid().ToString())
            .GetFromJsonAsync<NbpRateView[]>("/api/money/nbp-rates?currency=PLN&from=2025-01-01&to=2025-02-01", ct);

        Assert.Empty(rates!);
        Assert.Empty(nbp.Requests);
    }

    [Fact]
    public async Task AnUnknownCurrencyGives404WithoutCallingNbp()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var nbp = new StubNbpHandler(_ => StubNbpHandler.Rates(("2025-01-02", 4m)));
        await using var factory = FactoryWith(nbp);

        var response = await factory.ClientFor(Guid.NewGuid().ToString())
            .GetAsync("/api/money/nbp-rates?currency=XXX&from=2025-01-01&to=2025-02-01", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(nbp.Requests);
    }

    [Fact]
    public async Task AServerErrorGives502()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = FactoryWith(new StubNbpHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        var response = await factory.ClientFor(Guid.NewGuid().ToString()).GetAsync(Range, ct);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task ARepeatedRangeIsServedFromTheCache()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var nbp = new StubNbpHandler(_ => StubNbpHandler.Rates(("2025-01-02", 4m)));
        await using var factory = FactoryWith(nbp);
        var client = factory.ClientFor(Guid.NewGuid().ToString());

        await client.GetAsync(Range, ct);
        await client.GetAsync(Range, ct);

        Assert.Equal(3, nbp.Requests.Count);
    }

    [Fact]
    public async Task TheRealClientTargetsNbp()
    {
        await using var factory = new ApiFactory(fixture);

        var http = factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(NbpSeriesClient));

        Assert.Equal(new Uri("https://api.nbp.pl/"), http.BaseAddress);
    }
}
