using PSPad.TestInfrastructure;

namespace PSPad.Module.Statistics.Tests;

[UnitTest]
public class StatisticsOverviewReaderTests
{
    static readonly TimeZoneInfo Zone =
        TimeZoneInfo.CreateCustomTimeZone("test", TimeSpan.FromHours(2), "test", "test");

    static readonly DateOnly Today = new(2026, 9, 24);
    static readonly Guid User = Guid.NewGuid();

    sealed class FakeStore(params StatisticsRecord[] records) : IStatisticsStore
    {
        public List<DateTimeOffset> Since { get; } = [];

        public List<DateTimeOffset> OpenBefore { get; } = [];

        public Task SaveAsync(StatisticsRecord record, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<int> CountCompletionsBeforeAsync(Guid userId, Guid taskId, long seq, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<StatisticsRecord>> PageAsync(
            Guid userId, long? before, int limit, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<StatisticsRecord>> SinceAsync(
            Guid userId, DateTimeOffset from, CancellationToken ct)
        {
            Since.Add(from);

            return Task.FromResult<IReadOnlyList<StatisticsRecord>>(records
                .Where(record => record.UserId == userId && record.At >= from)
                .OrderBy(record => record.Id)
                .ToArray());
        }

        public Task<IReadOnlySet<Guid>> OpenTaskIdsBeforeAsync(
            Guid userId, DateTimeOffset from, CancellationToken ct)
        {
            OpenBefore.Add(from);
            var open = new HashSet<Guid>();

            foreach (var record in records
                         .Where(record => record.UserId == userId && record.At < from)
                         .OrderBy(record => record.Id))
            {
                if (record.Kind is RecordKind.Created or RecordKind.Reopened)
                {
                    open.Add(record.TaskId);
                }
                else if (record.Kind is RecordKind.Completed or RecordKind.Deleted)
                {
                    open.Remove(record.TaskId);
                }
            }

            return Task.FromResult<IReadOnlySet<Guid>>(open);
        }
    }

    sealed class FakeInboxStore(params InboxRecord[] records) : IInboxRecordStore
    {
        public List<DateTimeOffset> Since { get; } = [];

        public Task SaveAsync(InboxRecord record, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<InboxRecord>> SinceAsync(
            Guid userId, DateTimeOffset from, CancellationToken ct)
        {
            Since.Add(from);

            return Task.FromResult<IReadOnlyList<InboxRecord>>(records
                .Where(record => record.UserId == userId && record.At >= from)
                .OrderBy(record => record.Id)
                .ToArray());
        }
    }

    sealed class FakeLabelStore(params StatisticsLabel[] labels) : ILabelStore
    {
        public Task SaveAsync(StatisticsLabel label, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<StatisticsLabel?> FindAsync(Guid id, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<StatisticsLabel>> AllAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<StatisticsLabel>>(labels);
    }

    static InboxRecord Capture(long id, DateOnly day) =>
        new() { Id = id, UserId = User, At = Midday(day), ItemId = Guid.NewGuid() };

    static StatisticsOverviewReader Reader(
        IStatisticsStore store, ILabelStore? labels = null, IInboxRecordStore? inbox = null) =>
        new(store, labels ?? new FakeLabelStore(), inbox ?? new FakeInboxStore());

    static DateTimeOffset Midday(DateOnly day) =>
        new(day.ToDateTime(new TimeOnly(12, 0)), TimeSpan.FromHours(2));

    static StatisticsRecord Record(
        long id, RecordKind kind, DateOnly day, Guid taskId, Guid? goalId = null) =>
        new()
        {
            Id = id,
            UserId = User,
            At = Midday(day),
            Kind = kind,
            TaskId = taskId,
            TaskName = "task",
            GoalId = goalId
        };

    [Fact]
    public async Task BothHalvesOfTheWindowAreReadFromTheSameInstant()
    {
        var store = new FakeStore();

        await Reader(store)
            .ReadAsync(User, Today, 30, Zone, TestContext.Current.CancellationToken);

        Assert.Equal(Assert.Single(store.Since), Assert.Single(store.OpenBefore));
    }

    [Fact]
    public async Task TheInboxCapturesSplitOnTheSameInstantAsTheTaskCharts()
    {
        var store = new FakeStore();
        var inbox = new FakeInboxStore();

        await Reader(store, inbox: inbox)
            .ReadAsync(User, Today, 30, Zone, TestContext.Current.CancellationToken);

        Assert.Equal(Assert.Single(store.Since), Assert.Single(inbox.Since));
    }

    [Fact]
    public async Task CapturesOlderThanTheWindowAreNeverRead()
    {
        var inbox = new FakeInboxStore(
            Capture(1, Today.AddDays(-90)),
            Capture(2, Today.AddDays(-80)));

        var overview = await Reader(new FakeStore(), inbox: inbox)
            .ReadAsync(User, Today, 30, Zone, TestContext.Current.CancellationToken);

        Assert.Equal(5, overview.InboxCaptures.Count);
        Assert.All(overview.InboxCaptures, week => Assert.Equal(0, week.Count));
    }

    [Fact]
    public async Task CapturesInsideTheWindowLandOnTheirOwnWeek()
    {
        var inbox = new FakeInboxStore(
            Capture(1, Today.AddDays(-20)),
            Capture(2, Today),
            Capture(3, Today));

        var overview = await Reader(new FakeStore(), inbox: inbox)
            .ReadAsync(User, Today, 30, Zone, TestContext.Current.CancellationToken);

        Assert.Equal(1, overview.InboxCaptures[1].Count);
        Assert.Equal(2, overview.InboxCaptures[^1].Count);
    }

    [Fact]
    public async Task ATaskOpenedBeforeTheWindowCountsOnceOnEveryChartedDay()
    {
        var store = new FakeStore(
            Record(1, RecordKind.Created, Today.AddDays(-40), Guid.NewGuid()));

        var overview = await Reader(store)
            .ReadAsync(User, Today, 30, Zone, TestContext.Current.CancellationToken);

        Assert.All(overview.Outstanding, point => Assert.Equal(1, point.Count));
        Assert.All(overview.Opened, point => Assert.Equal(0, point.Count));
    }

    [Fact]
    public async Task ATaskOpenedBeforeTheWindowAndFinishedInsideItLeavesTheLine()
    {
        var taskId = Guid.NewGuid();
        var store = new FakeStore(
            Record(1, RecordKind.Created, Today.AddDays(-40), taskId),
            Record(2, RecordKind.Completed, Today, taskId));

        var overview = await Reader(store)
            .ReadAsync(User, Today, 30, Zone, TestContext.Current.CancellationToken);

        Assert.Equal(1, overview.Outstanding[0].Count);
        Assert.Equal(0, overview.Outstanding[^1].Count);
        Assert.Equal(-1, overview.Tiles.NetChange);
        Assert.Equal(1, overview.Tiles.DoneToday);
    }

    [Fact]
    public async Task AnUnrecognisedRangeFallsBackToThirtyDays()
    {
        var overview = await Reader(new FakeStore())
            .ReadAsync(User, Today, 7, Zone, TestContext.Current.CancellationToken);

        Assert.Equal(30, overview.Completions.Count);
        Assert.Equal(30, overview.Heatmap.Count);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(90)]
    [InlineData(365)]
    public async Task TheOfferedRangesAreHonoured(int days)
    {
        var overview = await Reader(new FakeStore())
            .ReadAsync(User, Today, days, Zone, TestContext.Current.CancellationToken);

        Assert.Equal(days, overview.Opened.Count);
        Assert.Equal(days, overview.Outstanding.Count);
    }

    [Fact]
    public async Task TheGoalBarsCarryTheirLabelName()
    {
        var goalId = Guid.NewGuid();
        var store = new FakeStore(
            Record(1, RecordKind.Completed, Today, Guid.NewGuid(), goalId));
        var labels = new FakeLabelStore(new StatisticsLabel
        {
            Id = goalId, UserId = User, Kind = LabelKind.Goal, Name = "Eat better"
        });

        var overview = await Reader(store, labels)
            .ReadAsync(User, Today, 30, Zone, TestContext.Current.CancellationToken);

        Assert.Contains(overview.ByGoal, bar => bar.GoalId == goalId && bar.Name == "Eat better");
        Assert.Contains(overview.ByGoal, bar => bar.GoalId is null && bar.Count == 0);
    }
}
