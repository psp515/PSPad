using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PSPad.Abstractions;
using PSPad.Api.Quotes;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Endpoints;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class QuoteEndpointTests(MongoFixture fixture)
{
    static readonly long Monday = new DateTimeOffset(2026, 10, 26, 14, 30, 0, TimeSpan.Zero).ToUnixTimeSeconds();
    static readonly long Tuesday = Monday + 86400;

    sealed class TestClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
    }

    static string NewSymbol() => "T" + Guid.NewGuid().ToString("N")[..12];

    ApiFactory FactoryWith(StubYahooHandler yahoo, TestClock? clock = null) =>
        new(fixture, testServices: services =>
        {
            services.AddHttpClient<YahooQuoteClient>().ConfigurePrimaryHttpMessageHandler(() => yahoo);
            services.AddSingleton<IClock>(clock ?? new TestClock(new DateTimeOffset(2026, 10, 30, 10, 0, 0, TimeSpan.Zero)));
        });

    static string Url(string symbol, string from = "2026-10-01", bool refresh = false) =>
        $"/api/investments/quotes?symbol={symbol}&from={from}&refresh={refresh.ToString().ToLowerInvariant()}";

    static StubYahooHandler Healthy() =>
        new(_ => StubYahooHandler.Chart("USD", -14400, (Monday, 10.5m), (Tuesday, null), (Tuesday + 3600, 11m)));

    [Fact]
    public async Task ItReturnsClosesAndCurrencySkippingNulls()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = FactoryWith(Healthy());
        var symbol = NewSymbol();

        var series = await factory.ClientFor(Guid.NewGuid().ToString()).GetFromJsonAsync<QuoteSeriesView>(Url(symbol), ct);

        Assert.Equal("USD", series!.Currency);
        Assert.Equal([new QuoteCloseView(new DateOnly(2026, 10, 26), 10.5m), new QuoteCloseView(new DateOnly(2026, 10, 27), 11m)],
            series.Closes);
        Assert.Equal(11m, series.Latest!.Price);
        Assert.False(series.Stale);
    }

    [Fact]
    public async Task ASecondCallTheSameDayIsServedFromTheCache()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var yahoo = Healthy();
        await using var factory = FactoryWith(yahoo);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var symbol = NewSymbol();

        await client.GetFromJsonAsync<QuoteSeriesView>(Url(symbol), ct);
        var again = await client.GetFromJsonAsync<QuoteSeriesView>(Url(symbol, "2026-10-27"), ct);

        Assert.Single(yahoo.Requests);
        Assert.Equal([new QuoteCloseView(new DateOnly(2026, 10, 27), 11m)], again!.Closes);
    }

    [Fact]
    public async Task AnEarlierFromRefetchesFromThatDate()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var yahoo = Healthy();
        await using var factory = FactoryWith(yahoo);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var symbol = NewSymbol();

        await client.GetFromJsonAsync<QuoteSeriesView>(Url(symbol), ct);
        await client.GetFromJsonAsync<QuoteSeriesView>(Url(symbol, "2026-09-01"), ct);

        Assert.Equal(2, yahoo.Requests.Count);
        Assert.Contains("period1=" + new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(),
            yahoo.Requests[1].Query);
    }

    [Fact]
    public async Task RefreshWithinFifteenMinutesDoesNotRefetchButLaterDoes()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var yahoo = Healthy();
        var clock = new TestClock(new DateTimeOffset(2026, 10, 30, 10, 0, 0, TimeSpan.Zero));
        await using var factory = FactoryWith(yahoo, clock);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var symbol = NewSymbol();

        await client.GetFromJsonAsync<QuoteSeriesView>(Url(symbol), ct);
        clock.UtcNow = clock.UtcNow.AddMinutes(10);
        await client.GetFromJsonAsync<QuoteSeriesView>(Url(symbol, refresh: true), ct);
        Assert.Single(yahoo.Requests);

        clock.UtcNow = clock.UtcNow.AddMinutes(10);
        await client.GetFromJsonAsync<QuoteSeriesView>(Url(symbol, refresh: true), ct);
        Assert.Equal(2, yahoo.Requests.Count);
    }

    [Fact]
    public async Task ACachedCopyFromYesterdayIsRefetched()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var yahoo = Healthy();
        var clock = new TestClock(new DateTimeOffset(2026, 10, 29, 23, 0, 0, TimeSpan.Zero));
        await using var factory = FactoryWith(yahoo, clock);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var symbol = NewSymbol();

        await client.GetFromJsonAsync<QuoteSeriesView>(Url(symbol), ct);
        clock.UtcNow = clock.UtcNow.AddHours(2);
        await client.GetFromJsonAsync<QuoteSeriesView>(Url(symbol), ct);

        Assert.Equal(2, yahoo.Requests.Count);
    }

    [Fact]
    public async Task AFailureWithACachedCopyReturnsItStale()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var fail = false;
        var yahoo = new StubYahooHandler(_ => fail
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : StubYahooHandler.Chart("USD", 0, (Monday, 10.5m)));
        var clock = new TestClock(new DateTimeOffset(2026, 10, 30, 10, 0, 0, TimeSpan.Zero));
        await using var factory = FactoryWith(yahoo, clock);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var symbol = NewSymbol();
        await client.GetFromJsonAsync<QuoteSeriesView>(Url(symbol), ct);

        fail = true;
        clock.UtcNow = clock.UtcNow.AddDays(1);
        var stale = await client.GetFromJsonAsync<QuoteSeriesView>(Url(symbol), ct);

        Assert.True(stale!.Stale);
        Assert.Equal(10.5m, Assert.Single(stale.Closes).Close);
    }

    [Fact]
    public async Task AFailureWithoutACachedCopyGives502()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = FactoryWith(new StubYahooHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)));

        var response = await factory.ClientFor(Guid.NewGuid().ToString()).GetAsync(Url(NewSymbol()), ct);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task ATimeoutGives502()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = FactoryWith(new StubYahooHandler(_ =>
            throw new TaskCanceledException("Yahoo timed out.", new TimeoutException())));

        var response = await factory.ClientFor(Guid.NewGuid().ToString()).GetAsync(Url(NewSymbol()), ct);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task AnUnknownSymbolGives404()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = FactoryWith(new StubYahooHandler(_ => StubYahooHandler.NotFoundChart()));

        var response = await factory.ClientFor(Guid.NewGuid().ToString()).GetAsync(Url(NewSymbol()), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AnOkAnswerWithAnErrorBodyGives404()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = FactoryWith(new StubYahooHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { chart = new { result = (object?)null, error = new { code = "Not Found" } } })
        }));

        var response = await factory.ClientFor(Guid.NewGuid().ToString()).GetAsync(Url(NewSymbol()), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("%20")]
    [InlineData("A%2FB")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTUVWXYZ")]
    [InlineData("A%20B")]
    public async Task AnInvalidSymbolGives404WithoutCallingYahoo(string symbol)
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var yahoo = Healthy();
        await using var factory = FactoryWith(yahoo);

        var response = await factory.ClientFor(Guid.NewGuid().ToString()).GetAsync(Url(symbol), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(yahoo.Requests);
    }

    [Fact]
    public async Task TheCurrencyPassesThroughUnchanged()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = FactoryWith(new StubYahooHandler(_ => StubYahooHandler.Chart("GBp", 3600, (Monday, 1250m))));

        var series = await factory.ClientFor(Guid.NewGuid().ToString())
            .GetFromJsonAsync<QuoteSeriesView>(Url(NewSymbol()), ct);

        Assert.Equal("GBp", series!.Currency);
        Assert.Equal(1250m, Assert.Single(series.Closes).Close);
    }

    [Theory]
    [InlineData("""{"chart":{"result":null,"error":null}}""")]
    [InlineData("""{"chart":{"result":[],"error":null}}""")]
    public async Task AnOkAnswerWithNoResultGives404(string body)
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = FactoryWith(new StubYahooHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        }));

        var response = await factory.ClientFor(Guid.NewGuid().ToString()).GetAsync(Url(NewSymbol()), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AMalformedBodyGives502()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = FactoryWith(new StubYahooHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html/>", System.Text.Encoding.UTF8, "text/html")
        }));

        var response = await factory.ClientFor(Guid.NewGuid().ToString()).GetAsync(Url(NewSymbol()), ct);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task APositiveOffsetMovesACloseToTheNextDay()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var late = new DateTimeOffset(2026, 10, 26, 23, 30, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        await using var factory = FactoryWith(new StubYahooHandler(_ => StubYahooHandler.Chart("EUR", 3600, (late, 5m))));

        var series = await factory.ClientFor(Guid.NewGuid().ToString())
            .GetFromJsonAsync<QuoteSeriesView>(Url(NewSymbol()), ct);

        Assert.Equal(new DateOnly(2026, 10, 27), Assert.Single(series!.Closes).Date);
    }

    [Fact]
    public async Task ANegativeOffsetMovesACloseToThePreviousDay()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var early = new DateTimeOffset(2026, 10, 27, 0, 30, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        await using var factory = FactoryWith(new StubYahooHandler(_ => StubYahooHandler.Chart("USD", -3600, (early, 5m))));

        var series = await factory.ClientFor(Guid.NewGuid().ToString())
            .GetFromJsonAsync<QuoteSeriesView>(Url(NewSymbol()), ct);

        Assert.Equal(new DateOnly(2026, 10, 26), Assert.Single(series!.Closes).Date);
    }

    [Fact]
    public async Task AnEmptyQuoteArrayGives502()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = FactoryWith(new StubYahooHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                chart = new
                {
                    result = new[]
                    {
                        new
                        {
                            meta = new { currency = "USD", gmtoffset = 0 },
                            timestamp = new[] { Monday },
                            indicators = new { quote = Array.Empty<object>() }
                        }
                    }
                }
            })
        }));

        var response = await factory.ClientFor(Guid.NewGuid().ToString()).GetAsync(Url(NewSymbol()), ct);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task FewerClosesThanTimestampsKeepsTheClosesThereAre()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = FactoryWith(new StubYahooHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                chart = new
                {
                    result = new[]
                    {
                        new
                        {
                            meta = new { currency = "USD", gmtoffset = 0 },
                            timestamp = new[] { Monday, Tuesday },
                            indicators = new { quote = new[] { new { close = new[] { 10m } } } }
                        }
                    }
                }
            })
        }));

        var series = await factory.ClientFor(Guid.NewGuid().ToString())
            .GetFromJsonAsync<QuoteSeriesView>(Url(NewSymbol()), ct);

        Assert.Equal(10m, Assert.Single(series!.Closes).Close);
    }

    [Theory]
    [InlineData("..", "2026-10-01")]
    [InlineData("^^", "2026-10-01")]
    [InlineData("ABC", "2026-10-31")]
    [InlineData("ABC", "1969-12-31")]
    public async Task AnUnusableRequestGives404WithoutCallingYahoo(string symbol, string from)
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var yahoo = Healthy();
        await using var factory = FactoryWith(yahoo);

        var response = await factory.ClientFor(Guid.NewGuid().ToString()).GetAsync(Url(symbol, from), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(yahoo.Requests);
    }

    [Fact]
    public async Task ItRequiresSignIn()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var yahoo = Healthy();
        await using var factory = FactoryWith(yahoo);

        var response = await factory.CreateClient().GetAsync(Url(NewSymbol()), ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(yahoo.Requests);
    }

    [Fact]
    public async Task TheRealClientTargetsYahooAndGivesUpAfterTenSeconds()
    {
        await using var factory = new ApiFactory(fixture);

        var http = factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(YahooQuoteClient));

        Assert.Equal(new Uri("https://query1.finance.yahoo.com/"), http.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(10), http.Timeout);
    }
}
