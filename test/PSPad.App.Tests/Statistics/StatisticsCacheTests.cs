using Microsoft.JSInterop;
using PSPad.App.Statistics;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Statistics;

[UnitTest]
public class StatisticsCacheTests
{
    [Fact]
    public async Task ReadingBeforeAnyWriteReturnsNull()
    {
        var cache = new StatisticsCache(new FakeJsRuntime());

        Assert.Null(await cache.ReadAsync(30));
    }

    [Fact]
    public async Task AWrittenOverviewRoundTripsForItsOwnWindow()
    {
        var cache = new StatisticsCache(new FakeJsRuntime());
        var overview = Sample(doneToday: 3);

        await cache.WriteAsync(30, overview);

        Assert.Equal(3, (await cache.ReadAsync(30))!.Tiles.DoneToday);
    }

    [Fact]
    public async Task DifferentWindowsAreCachedSeparately()
    {
        var cache = new StatisticsCache(new FakeJsRuntime());

        await cache.WriteAsync(30, Sample(doneToday: 1));
        await cache.WriteAsync(90, Sample(doneToday: 2));

        Assert.Equal(1, (await cache.ReadAsync(30))!.Tiles.DoneToday);
        Assert.Equal(2, (await cache.ReadAsync(90))!.Tiles.DoneToday);
    }

    [Fact]
    public async Task ClearRemovesEveryKnownWindow()
    {
        var cache = new StatisticsCache(new FakeJsRuntime());
        await cache.WriteAsync(30, Sample(doneToday: 1));
        await cache.WriteAsync(90, Sample(doneToday: 2));
        await cache.WriteAsync(365, Sample(doneToday: 3));

        await cache.ClearAsync();

        Assert.Null(await cache.ReadAsync(30));
        Assert.Null(await cache.ReadAsync(90));
        Assert.Null(await cache.ReadAsync(365));
    }

    [Fact]
    public async Task AReadThatThrowsIsTreatedAsNoCache()
    {
        var cache = new StatisticsCache(new ThrowingJsRuntime());

        Assert.Null(await cache.ReadAsync(30));
    }

    [Fact]
    public async Task AWriteThatThrowsDoesNotEscape()
    {
        var cache = new StatisticsCache(new ThrowingJsRuntime());

        await cache.WriteAsync(30, Sample(doneToday: 1));
    }

    [Fact]
    public async Task CorruptedStoredJsonIsTreatedAsNoCache()
    {
        var js = new FakeJsRuntime();
        js.Values["pspad.statistics.3.30"] = "not valid json";
        var cache = new StatisticsCache(js);

        Assert.Null(await cache.ReadAsync(30));
    }

    [Fact]
    public async Task APayloadCachedUnderThePreviousShapesKeyIsIgnored()
    {
        var js = new FakeJsRuntime();
        js.Values["pspad.statistics.2.30"] =
            """{"Tiles":{"DoneToday":3,"OpenedToday":0,"DoneThisWeek":0,"NetChange":0}}""";
        var cache = new StatisticsCache(js);

        Assert.Null(await cache.ReadAsync(30));
    }

    [Fact]
    public async Task ClearAlsoRemovesWhatThePreviousShapeLeftBehind()
    {
        var js = new FakeJsRuntime();
        js.Values["pspad.statistics.30"] = "{}";
        js.Values["pspad.statistics.2.30"] = "{}";
        var cache = new StatisticsCache(js);

        await cache.ClearAsync();

        Assert.Empty(js.Values);
    }

    static StatisticsOverview Sample(int doneToday) =>
        new(new StatisticsTilesView(doneToday, 0, 0, 0), [], [], [], [], [], []);

    sealed class FakeJsRuntime : IJSRuntime
    {
        public Dictionary<string, string> Values { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            var key = (string)args![0]!;

            switch (identifier)
            {
                case "localStorage.getItem":
                    return ValueTask.FromResult((TValue)(object?)(Values.TryGetValue(key, out var value) ? value : null)!);
                case "localStorage.setItem":
                    Values[key] = (string)args[1]!;
                    return ValueTask.FromResult(default(TValue)!);
                case "localStorage.removeItem":
                    Values.Remove(key);
                    return ValueTask.FromResult(default(TValue)!);
                default:
                    return ValueTask.FromResult(default(TValue)!);
            }
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }

    sealed class ThrowingJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new JSException("blocked");

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }
}
