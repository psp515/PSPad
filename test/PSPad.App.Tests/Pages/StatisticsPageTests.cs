using Bunit;
using Bunit.TestDoubles;
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
    public void TheSevenDayTileIsLabelledLastSevenDaysNotThisWeek()
    {
        var source = new FakeStatisticsSource { Overview = Overview(doneToday: 0) };
        Arrange(source, NewCache());

        var page = Render<StatisticsPage>();

        Assert.Contains("Last 7 days", page.Markup);
    }

    [Fact]
    public void TheHistoryRouteRedirectsToStatistics()
    {
        var source = new FakeStatisticsSource { Overview = Overview(doneToday: 0) };
        Arrange(source, NewCache());
        var navigation = Services.GetRequiredService<BunitNavigationManager>();
        navigation.NavigateTo("/history");

        Render<StatisticsPage>();

        Assert.Equal("http://localhost/statistics", navigation.Uri);
    }

    [Fact]
    public async Task SelectingADifferentRangeRefetchesAndRerendersTheTiles()
    {
        var source = new FakeStatisticsSource
        {
            OverviewsByDays =
            {
                [30] = Overview(doneToday: 1),
                [90] = Overview(doneToday: 9)
            }
        };
        Arrange(source, NewCache());

        var page = Render<StatisticsPage>();
        page.FindAll("button").First(button => button.TextContent.Trim() == "90 days").Click();

        var tile = page.FindComponents<StatTile>().Single(t => t.Instance.Label == "Done today");
        Assert.Equal(9, tile.Instance.Value);
        Assert.Contains(90, source.RequestedDays);
    }

    [Fact]
    public void CompletionsChartRendersThePlannedAndUnplannedSeriesPerDay()
    {
        var source = new FakeStatisticsSource { Overview = FullOverview() };
        Arrange(source, NewCache());

        var page = Render<StatisticsPage>();

        var chart = page.FindComponents<MudChart<double>>()
            .Single(c => c.Instance.ChartSeries.Any(series => series.Name == "Planned"));
        var planned = chart.Instance.ChartSeries.Single(series => series.Name == "Planned");
        var unplanned = chart.Instance.ChartSeries.Single(series => series.Name == "Unplanned");

        Assert.Equal([1d, 3d, 5d], planned.Data.Values);
        Assert.Equal([2d, 4d, 6d], unplanned.Data.Values);
        Assert.Equal(["03-08", "03-09", "03-10"], chart.Instance.ChartLabels);
    }

    [Fact]
    public void OpenedChartRendersTheOpenedCountsPerDay()
    {
        var source = new FakeStatisticsSource { Overview = FullOverview() };
        Arrange(source, NewCache());

        var page = Render<StatisticsPage>();

        var chart = page.FindComponents<MudChart<double>>()
            .Single(c => c.Instance.ChartSeries.Any(series => series.Name == "Opened"));
        var opened = chart.Instance.ChartSeries.Single();

        Assert.Equal([2d, 4d, 6d], opened.Data.Values);
        Assert.Equal(["03-08", "03-09", "03-10"], chart.Instance.ChartLabels);
    }

    [Fact]
    public void OutstandingChartRendersTheOutstandingCountsPerDay()
    {
        var source = new FakeStatisticsSource { Overview = FullOverview() };
        Arrange(source, NewCache());

        var page = Render<StatisticsPage>();

        var chart = page.FindComponents<MudChart<double>>()
            .Single(c => c.Instance.ChartSeries.Any(series => series.Name == "Outstanding"));
        var outstanding = chart.Instance.ChartSeries.Single();

        Assert.Equal([10d, 8d, 6d], outstanding.Data.Values);
        Assert.Equal(["03-08", "03-09", "03-10"], chart.Instance.ChartLabels);
    }

    [Fact]
    public void ByGoalRendersARowPerGoalIncludingTheExplicitNoGoalBar()
    {
        var source = new FakeStatisticsSource { Overview = FullOverview() };
        Arrange(source, NewCache());

        var page = Render<StatisticsPage>();

        var group = page.FindAll("[role='group']")
            .Single(element => element.QuerySelector("[role='progressbar']") is not null);
        var names = group.QuerySelectorAll(".mud-typography-body2")
            .Select(element => element.TextContent.Trim())
            .ToList();
        var bars = group.QuerySelectorAll("[role='progressbar']");
        var values = bars.Select(bar => int.Parse(bar.GetAttribute("aria-valuenow")!)).ToList();
        var maxes = bars.Select(bar => int.Parse(bar.GetAttribute("aria-valuemax")!)).ToList();

        Assert.Equal(["Health", "Home", "No goal"], names);
        Assert.Equal([7, 3, 5], values);
        Assert.All(maxes, max => Assert.Equal(7, max));
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

    static StatisticsOverview FullOverview() =>
        new(
            new StatisticsTilesView(0, 0, 0, 0),
            [
                new DailyCompletionsView(new DateOnly(2026, 3, 8), Planned: 1, Unplanned: 2),
                new DailyCompletionsView(new DateOnly(2026, 3, 9), Planned: 3, Unplanned: 4),
                new DailyCompletionsView(new DateOnly(2026, 3, 10), Planned: 5, Unplanned: 6)
            ],
            [
                new DailyCountView(new DateOnly(2026, 3, 8), 2),
                new DailyCountView(new DateOnly(2026, 3, 9), 4),
                new DailyCountView(new DateOnly(2026, 3, 10), 6)
            ],
            [
                new DailyCountView(new DateOnly(2026, 3, 8), 10),
                new DailyCountView(new DateOnly(2026, 3, 9), 8),
                new DailyCountView(new DateOnly(2026, 3, 10), 6)
            ],
            [
                new GoalTotalView(Guid.NewGuid(), "Health", 7),
                new GoalTotalView(Guid.NewGuid(), "Home", 3),
                new GoalTotalView(null, "No goal", 5)
            ],
            [
                new DailyCountView(new DateOnly(2026, 3, 8), 3),
                new DailyCountView(new DateOnly(2026, 3, 9), 7),
                new DailyCountView(new DateOnly(2026, 3, 10), 11)
            ]);

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

        public Dictionary<int, StatisticsOverview> OverviewsByDays { get; } = [];

        public List<int> RequestedDays { get; } = [];

        public IReadOnlyList<StatisticsRecordView> Records { get; set; } = [];

        public bool Fails { get; set; }

        public bool Blocked { get; set; }

        readonly TaskCompletionSource<bool> _gate = new();

        public async Task<StatisticsOverview?> OverviewAsync(int days)
        {
            RequestedDays.Add(days);
            await GateAsync();
            return OverviewsByDays.TryGetValue(days, out var overview) ? overview : Overview;
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
