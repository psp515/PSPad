using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor;
using PSPad.App.Components;
using PSPad.App.Pages;
using PSPad.App.Statistics;
using PSPad.App.Tests;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Pages;

[UnitTest]
public class StatisticsPageTests : Bunit.TestContext
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 3, 10);

    [Fact]
    public async Task TheCachedOverviewRendersBeforeTheFetchResolves()
    {
        var cache = NewCache();
        await cache.WriteAsync(30, Overview(doneToday: 7));
        var source = new FakeStatisticsSource { Blocked = true };
        Arrange(source, cache);

        var page = Render<StatisticsPage>();

        var tile = page.FindComponents<StatTile>().Single(t => t.Instance.Label == "Done today");
        Assert.Equal(7, tile.Instance.Value);
    }

    [Fact]
    public async Task AFailedFetchShowsTheOfflineBannerAndKeepsTheCachedNumbers()
    {
        var cache = NewCache();
        await cache.WriteAsync(30, Overview(doneToday: 7));
        var source = new FakeStatisticsSource { Fails = true };
        Arrange(source, cache);

        var page = Render<StatisticsPage>();

        Assert.NotEmpty(page.FindComponents<MudAlert>());
        var tile = page.FindComponents<StatTile>().Single(t => t.Instance.Label == "Done today");
        Assert.Equal(7, tile.Instance.Value);
    }

    [Fact]
    public void ARecordFinishedMoreThanOnceShowsItsCount()
    {
        var source = new FakeStatisticsSource
        {
            Records = [Record(taskName: "Buy milk", completionNumber: 3, status: "Open")]
        };
        Arrange(source, NewCache());

        var page = Render<StatisticsPage>();

        var chip = page.FindComponents<MudChip<string>>().Single();
        Assert.Contains("#3", chip.Markup);
    }

    [Fact]
    public void ARecordWhoseTaskIsGoneIsNotClickable()
    {
        var goneTaskId = Guid.NewGuid();
        var source = new FakeStatisticsSource
        {
            Records = [Record(taskName: "Old task", completionNumber: null, status: "Gone", taskId: goneTaskId)]
        };
        Arrange(source, NewCache());

        var page = Render<StatisticsPage>();

        var row = page.Find(".pspad-record-gone");

        Assert.Throws<MissingEventHandlerException>(() => row.Click());
    }

    void Arrange(FakeStatisticsSource source, StatisticsCache cache)
    {
        AppTestHost.Arrange(this, User, Today);
        Services.AddSingleton<IStatisticsSource>(source);
        Services.AddSingleton(cache);
    }

    static StatisticsCache NewCache() => new(new FakeJsRuntime());

    static StatisticsOverview Overview(int doneToday) =>
        new(new StatisticsTilesView(doneToday, 0, 0, 0), [], [], [], [], []);

    static StatisticsRecordView Record(
        string taskName, int? completionNumber, string status, Guid? taskId = null) =>
        new(
            Seq: 1,
            At: new DateTimeOffset(2026, 3, 9, 9, 0, 0, TimeSpan.Zero),
            Kind: "Completed",
            TaskId: taskId ?? Guid.NewGuid(),
            TaskName: taskName,
            ListName: null,
            GoalName: null,
            CompletionNumber: completionNumber,
            CurrentStatus: status);

    sealed class FakeStatisticsSource : IStatisticsSource
    {
        public StatisticsOverview? Overview { get; set; }

        public IReadOnlyList<StatisticsRecordView> Records { get; set; } = [];

        public bool Fails { get; set; }

        public bool Blocked { get; set; }

        readonly TaskCompletionSource<bool> _gate = new();

        public async Task<StatisticsOverview?> OverviewAsync(int days)
        {
            await GateAsync();
            return Overview;
        }

        public async Task<IReadOnlyList<StatisticsRecordView>> RecordsAsync(long? before, int limit)
        {
            await GateAsync();
            return Records;
        }

        async Task GateAsync()
        {
            if (Blocked)
            {
                await _gate.Task;
            }

            if (Fails)
            {
                throw new HttpRequestException("offline");
            }
        }
    }

    sealed class FakeJsRuntime : IJSRuntime
    {
        readonly Dictionary<string, string> _values = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            var key = (string)args![0]!;

            switch (identifier)
            {
                case "localStorage.getItem":
                    return ValueTask.FromResult((TValue)(object?)(_values.TryGetValue(key, out var value) ? value : null)!);
                case "localStorage.setItem":
                    _values[key] = (string)args[1]!;
                    return ValueTask.FromResult(default(TValue)!);
                case "localStorage.removeItem":
                    _values.Remove(key);
                    return ValueTask.FromResult(default(TValue)!);
                default:
                    return ValueTask.FromResult(default(TValue)!);
            }
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }
}
