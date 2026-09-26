using AngleSharp.Dom;
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
        page.FindAll(".mud-toggle-item").First(item => item.TextContent.Trim() == "90 days").Click();

        var tile = page.FindComponents<StatTile>().Single(t => t.Instance.Label == "Done today");
        Assert.Equal(9, tile.Instance.Value);
        Assert.Contains(90, source.RequestedDays);
        Assert.Equal("true", RangeButton(page, "90 days").GetAttribute("aria-checked"));
        Assert.Equal("false", RangeButton(page, "30 days").GetAttribute("aria-checked"));
        Assert.Equal("false", RangeButton(page, "365 days").GetAttribute("aria-checked"));
    }

    [Fact]
    public void TheThirtyDayRangeIsSelectedByDefault()
    {
        var source = new FakeStatisticsSource { Overview = Overview(doneToday: 0) };
        Arrange(source, NewCache());

        var page = Render<StatisticsPage>();

        Assert.Equal("true", RangeButton(page, "30 days").GetAttribute("aria-checked"));
        Assert.Equal("false", RangeButton(page, "90 days").GetAttribute("aria-checked"));
        Assert.Equal("false", RangeButton(page, "365 days").GetAttribute("aria-checked"));
    }

    static IElement RangeButton(IRenderedComponent<StatisticsPage> page, string text) =>
        page.FindAll(".mud-toggle-item").Single(item => item.TextContent.Trim() == text);

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

    [Theory]
    [InlineData(30, 7)]
    [InlineData(90, 8)]
    [InlineData(365, 8)]
    public void ChartLabelsAreThinnedToAReadableCountAcrossRanges(int days, int expectedVisibleLabels)
    {
        var source = new FakeStatisticsSource { Overview = RangeOverview(days, maxValue: 5) };
        Arrange(source, NewCache());

        var page = Render<StatisticsPage>();

        var chart = OutstandingChart(page);

        Assert.Equal(days, chart.Instance.ChartLabels.Length);
        Assert.Equal(expectedVisibleLabels, chart.Instance.ChartLabels.Count(label => label.Length > 0));
        Assert.NotEqual("", chart.Instance.ChartLabels[0]);
        Assert.NotEqual("", chart.Instance.ChartLabels[^1]);
    }

    [Fact]
    public void ChartLabelsStayNumericAtTheShorterRanges()
    {
        var source = new FakeStatisticsSource { Overview = RangeOverview(30, maxValue: 5) };
        Arrange(source, NewCache());

        var page = Render<StatisticsPage>();

        var populated = OutstandingChart(page).Instance.ChartLabels.Where(label => label.Length > 0);

        Assert.All(populated, label => Assert.Matches(@"^\d{2}-\d{2}$", label));
    }

    [Fact]
    public void ChartLabelsSwitchToMonthAndYearAtTheYearLongRange()
    {
        var source = new FakeStatisticsSource { Overview = RangeOverview(365, maxValue: 5) };
        Arrange(source, NewCache());

        var page = Render<StatisticsPage>();

        var populated = OutstandingChart(page).Instance.ChartLabels.Where(label => label.Length > 0);

        Assert.All(populated, label => Assert.Matches(@"^[A-Za-z]{3} \d{4}$", label));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(40, 10)]
    [InlineData(0, 1)]
    public void OutstandingChartYAxisScalesToTheData(int max, int expectedTicks)
    {
        var source = new FakeStatisticsSource { Overview = RangeOverview(30, max) };
        Arrange(source, NewCache());

        var page = Render<StatisticsPage>();

        var options = Assert.IsType<LineChartOptions>(OutstandingChart(page).Instance.ChartOptions);
        Assert.Equal(expectedTicks, options.YAxisTicks);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(40, 10)]
    [InlineData(0, 1)]
    public void CompletionsChartYAxisScalesToTheStackedTotal(int max, int expectedTicks)
    {
        var source = new FakeStatisticsSource { Overview = RangeOverview(30, max) };
        Arrange(source, NewCache());

        var page = Render<StatisticsPage>();

        var chart = page.FindComponents<MudChart<double>>()
            .Single(c => c.Instance.ChartSeries.Any(series => series.Name == "Planned"));
        var options = Assert.IsType<StackedBarChartOptions>(chart.Instance.ChartOptions);

        Assert.Equal(expectedTicks, options.YAxisTicks);
    }

    [Fact]
    public void AnAllZeroRangeRendersWithoutThrowing()
    {
        var source = new FakeStatisticsSource { Overview = RangeOverview(30, maxValue: 0) };
        Arrange(source, NewCache());

        var exception = Xunit.Record.Exception(() => Render<StatisticsPage>());

        Assert.Null(exception);
    }

    static IRenderedComponent<MudChart<double>> OutstandingChart(IRenderedComponent<StatisticsPage> page) =>
        page.FindComponents<MudChart<double>>()
            .Single(c => c.Instance.ChartSeries.Any(series => series.Name == "Outstanding"));

    static StatisticsOverview RangeOverview(int days, int maxValue)
    {
        var start = Today.AddDays(-(days - 1));
        var completions = Enumerable.Range(0, days)
            .Select(i => new DailyCompletionsView(start.AddDays(i), i == days - 1 ? maxValue : 0, 0))
            .ToList();
        var counts = Enumerable.Range(0, days)
            .Select(i => new DailyCountView(start.AddDays(i), i == days - 1 ? maxValue : 0))
            .ToList();

        return new StatisticsOverview(
            new StatisticsTilesView(0, 0, 0, 0),
            completions,
            counts,
            counts,
            [],
            counts,
            Captures(days, maxValue));
    }

    static IReadOnlyList<WeeklyCountView> Captures(int days, int maxValue)
    {
        var first = Today.AddDays(-(days - 1));
        var weekStart = first.AddDays(-(int)first.DayOfWeek);
        var weeks = new List<WeeklyCountView>();

        for (var week = weekStart; week <= Today; week = week.AddDays(7))
        {
            weeks.Add(new WeeklyCountView(week, maxValue));
        }

        return weeks;
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
    public void TheConsistencyHeatmapCarriesItsOwnShadeKey()
    {
        var source = new FakeStatisticsSource { Overview = FullOverview() };
        Arrange(source, NewCache());

        var page = Render<StatisticsPage>();

        Assert.Single(page.FindComponents<HeatmapKey>());
        Assert.Equal(5, page.FindAll(".pspad-heatmap-key-cell").Count);
    }

    [Fact]
    public void InboxCapturesChartRendersOneBarPerWeekWithItsCount()
    {
        var source = new FakeStatisticsSource { Overview = FullOverview() };
        Arrange(source, NewCache());

        var page = Render<StatisticsPage>();

        var chart = page.FindComponents<MudChart<double>>()
            .Single(c => c.Instance.ChartSeries.Any(series => series.Name == "Captured"));
        var captures = chart.Instance.ChartSeries.Single();

        Assert.Equal([1d, 4d, 2d], captures.Data.Values);
        Assert.Equal(["02-22", "03-01", "03-08"], chart.Instance.ChartLabels);
        Assert.Contains("Captured into the Inbox each week", page.Markup);
    }

    [Fact]
    public void InboxCapturesChartLabelsAreThinnedAcrossAYearOfWeeks()
    {
        var source = new FakeStatisticsSource { Overview = RangeOverview(365, maxValue: 3) };
        Arrange(source, NewCache());

        var page = Render<StatisticsPage>();

        var chart = CapturesChart(page);
        var visible = chart.Instance.ChartLabels.Count(label => label.Length > 0);

        Assert.True(chart.Instance.ChartLabels.Length > 8);
        Assert.InRange(visible, 4, 8);
        Assert.All(
            chart.Instance.ChartLabels.Where(label => label.Length > 0),
            label => Assert.Matches(@"^[A-Za-z]{3} \d{4}$", label));
    }

    [Fact]
    public void InboxCapturesChartLabelsStayNumericAtTheThirtyDayRange()
    {
        var source = new FakeStatisticsSource { Overview = RangeOverview(30, maxValue: 3) };
        Arrange(source, NewCache());

        var page = Render<StatisticsPage>();

        var populated = CapturesChart(page).Instance.ChartLabels.Where(label => label.Length > 0);

        Assert.All(populated, label => Assert.Matches(@"^\d{2}-\d{2}$", label));
    }

    [Fact]
    public void AnAllZeroInboxCapturesChartRendersWithoutDividingByZero()
    {
        var source = new FakeStatisticsSource { Overview = RangeOverview(30, maxValue: 0) };
        Arrange(source, NewCache());

        var page = Render<StatisticsPage>();

        var options = Assert.IsType<BarChartOptions>(CapturesChart(page).Instance.ChartOptions);
        Assert.Equal(1, options.YAxisTicks);
    }

    [Fact]
    public void TheInboxCapturesTableCarriesEveryWeeksNumberForScreenReaders()
    {
        var source = new FakeStatisticsSource { Overview = FullOverview() };
        Arrange(source, NewCache());

        var page = Render<StatisticsPage>();

        var table = page.FindAll("table.mud-sr-only")
            .Single(candidate => candidate.QuerySelectorAll("tr").Length > 1);
        var rows = table.QuerySelectorAll("tbody tr")
            .Select(row => row.QuerySelectorAll("td").Select(cell => cell.TextContent.Trim()).ToArray())
            .ToList();

        Assert.Equal(
            new[]
            {
                new[] { "2026-02-22", "1" },
                new[] { "2026-03-01", "4" },
                new[] { "2026-03-08", "2" }
            },
            rows);
    }

    static IRenderedComponent<MudChart<double>> CapturesChart(IRenderedComponent<StatisticsPage> page) =>
        page.FindComponents<MudChart<double>>()
            .Single(c => c.Instance.ChartSeries.Any(series => series.Name == "Captured"));

    [Fact]
    public void TheRecordFeedStartsCollapsed()
    {
        var source = new FakeStatisticsSource
        {
            Overview = FullOverview(),
            Records = [Record(taskName: "Buy milk", completionNumber: 1, status: "Open")]
        };
        Arrange(source, NewCache());

        var page = Render<StatisticsPage>();

        Assert.DoesNotContain("mud-panel-expanded", page.Find(".mud-expand-panel").ClassName);
        Assert.Contains("invisible", page.Find(".mud-collapse-container").ClassName);
    }

    [Fact]
    public void ClickingTheFeedHeaderOpensIt()
    {
        var source = new FakeStatisticsSource
        {
            Overview = FullOverview(),
            Records = [Record(taskName: "Buy milk", completionNumber: 1, status: "Open")]
        };
        Arrange(source, NewCache());

        var page = Render<StatisticsPage>();
        page.Find(".mud-expand-panel-header").Click();

        Assert.Contains("mud-panel-expanded", page.Find(".mud-expand-panel").ClassName);
        Assert.DoesNotContain("invisible", page.Find(".mud-collapse-container").ClassName);
        Assert.Contains("Buy milk", page.Markup);
    }

    [Fact]
    public void LoadOlderStillPagesOnceTheFeedIsOpen()
    {
        var source = new FakeStatisticsSource
        {
            Overview = FullOverview(),
            Records = [Record(taskName: "Buy milk", completionNumber: 1, status: "Open", seq: 9)],
            OlderRecords = [Record(taskName: "Older errand", completionNumber: 1, status: "Open", seq: 4)]
        };
        Arrange(source, NewCache());

        var page = Render<StatisticsPage>();
        page.Find(".mud-expand-panel-header").Click();
        page.FindAll("button").Single(button => button.TextContent.Trim() == "load older").Click();

        Assert.Equal([null, 9L], source.RequestedBefore);
        Assert.Contains("Buy milk", page.Markup);
        Assert.Contains("Older errand", page.Markup);
    }

    [Fact]
    public void CollapsingTheFeedAgainKeepsTheRecordsItAlreadyLoaded()
    {
        var source = new FakeStatisticsSource
        {
            Overview = FullOverview(),
            Records = [Record(taskName: "Buy milk", completionNumber: 1, status: "Open", seq: 9)],
            OlderRecords = [Record(taskName: "Older errand", completionNumber: 1, status: "Open", seq: 4)]
        };
        Arrange(source, NewCache());

        var page = Render<StatisticsPage>();
        page.Find(".mud-expand-panel-header").Click();
        page.FindAll("button").Single(button => button.TextContent.Trim() == "load older").Click();
        page.Find(".mud-expand-panel-header").Click();
        page.Find(".mud-expand-panel-header").Click();

        Assert.Contains("mud-panel-expanded", page.Find(".mud-expand-panel").ClassName);
        Assert.Contains("Buy milk", page.Markup);
        Assert.Contains("Older errand", page.Markup);
        Assert.Equal([null, 9L], source.RequestedBefore);
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
        new(new StatisticsTilesView(doneToday, 0, 0, 0), [], [], [], [], [], []);

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
            ],
            [
                new WeeklyCountView(new DateOnly(2026, 2, 22), 1),
                new WeeklyCountView(new DateOnly(2026, 3, 1), 4),
                new WeeklyCountView(new DateOnly(2026, 3, 8), 2)
            ]);

    static StatisticsRecordView Record(
        string taskName, int? completionNumber, string status, Guid? taskId = null, long seq = 1) =>
        new(
            Seq: seq,
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

        public IReadOnlyList<StatisticsRecordView> OlderRecords { get; set; } = [];

        public List<long?> RequestedBefore { get; } = [];

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
            RequestedBefore.Add(before);
            await GateAsync();
            return before is null ? Records : OlderRecords;
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
