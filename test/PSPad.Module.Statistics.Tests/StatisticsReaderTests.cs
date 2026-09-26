using PSPad.TestInfrastructure;

namespace PSPad.Module.Statistics.Tests;

[UnitTest]
public class StatisticsReaderTests
{
    static readonly DateTimeOffset At = new(2026, 9, 24, 8, 0, 0, TimeSpan.Zero);
    static readonly Guid User = Guid.NewGuid();

    sealed class FakeStore(params StatisticsRecord[] records) : IStatisticsStore
    {
        public List<(long? Before, int Limit)> Pages { get; } = [];

        public Task SaveAsync(StatisticsRecord record, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<int> CountCompletionsBeforeAsync(Guid userId, Guid taskId, long seq, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<StatisticsRecord>> PageAsync(
            Guid userId, long? before, int limit, CancellationToken ct)
        {
            Pages.Add((before, limit));

            return Task.FromResult<IReadOnlyList<StatisticsRecord>>(records
                .Where(record => record.UserId == userId && (before is null || record.Id < before))
                .OrderByDescending(record => record.Id)
                .Take(limit)
                .ToArray());
        }

        public Task<IReadOnlyList<StatisticsRecord>> SinceAsync(
            Guid userId, DateTimeOffset from, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlySet<Guid>> OpenTaskIdsBeforeAsync(
            Guid userId, DateTimeOffset from, CancellationToken ct) =>
            throw new NotSupportedException();
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

    sealed class FakeSnapshots(Dictionary<Guid, TaskSnapshot> snapshots) : ITaskSnapshotSource
    {
        public List<Guid[]> Lookups { get; } = [];

        public Task<IReadOnlyDictionary<Guid, TaskSnapshot>> CurrentAsync(
            IReadOnlyCollection<Guid> taskIds, CancellationToken ct)
        {
            Lookups.Add(taskIds.ToArray());

            return Task.FromResult<IReadOnlyDictionary<Guid, TaskSnapshot>>(taskIds.ToDictionary(
                taskId => taskId,
                taskId => snapshots.GetValueOrDefault(taskId, new TaskSnapshot(TaskStatus.Gone, ""))));
        }
    }

    static StatisticsRecord Record(
        long id, RecordKind kind, Guid taskId, string name = "task",
        Guid? listId = null, Guid? goalId = null, int? completionNumber = null) =>
        new()
        {
            Id = id,
            UserId = User,
            At = At,
            Kind = kind,
            TaskId = taskId,
            TaskName = name,
            ListId = listId,
            GoalId = goalId,
            CompletionNumber = completionNumber
        };

    static StatisticsLabel Label(Guid id, LabelKind kind, string name) =>
        new() { Id = id, UserId = User, Kind = kind, Name = name };

    static StatisticsReader ReaderOver(
        FakeStore store, FakeLabelStore labels, FakeSnapshots snapshots) =>
        new(store, labels, snapshots);

    [Fact]
    public async Task ARecordRendersItsOwnStoredName()
    {
        var taskId = Guid.NewGuid();
        var reader = ReaderOver(
            new FakeStore(Record(1, RecordKind.Completed, taskId, "Buy milk")),
            new FakeLabelStore(),
            new FakeSnapshots(new Dictionary<Guid, TaskSnapshot>
            {
                [taskId] = new(TaskStatus.Open, "Buy oat milk")
            }));

        var page = await reader.ReadAsync(User, null, 50, TestContext.Current.CancellationToken);

        Assert.Equal("Buy milk", Assert.Single(page).TaskName);
    }

    [Fact]
    public async Task ARecordWithNoStoredNameBorrowsTheLiveTaskName()
    {
        var taskId = Guid.NewGuid();
        var reader = ReaderOver(
            new FakeStore(Record(1, RecordKind.Completed, taskId, "")),
            new FakeLabelStore(),
            new FakeSnapshots(new Dictionary<Guid, TaskSnapshot>
            {
                [taskId] = new(TaskStatus.Open, "Buy oat milk")
            }));

        var page = await reader.ReadAsync(User, null, 50, TestContext.Current.CancellationToken);

        Assert.Equal("Buy oat milk", Assert.Single(page).TaskName);
    }

    [Fact]
    public async Task ARecordWithNoStoredNameReadsAsADeletedTaskWhenTheTaskIsGone()
    {
        var taskId = Guid.NewGuid();
        var reader = ReaderOver(
            new FakeStore(Record(1, RecordKind.Completed, taskId, "")),
            new FakeLabelStore(),
            new FakeSnapshots([]));

        var page = await reader.ReadAsync(User, null, 50, TestContext.Current.CancellationToken);

        var view = Assert.Single(page);
        Assert.Equal("(deleted task)", view.TaskName);
        Assert.Equal("Gone", view.CurrentStatus);
    }

    [Fact]
    public async Task ListAndGoalNamesComeFromTheLabels()
    {
        var taskId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var reader = ReaderOver(
            new FakeStore(Record(1, RecordKind.Completed, taskId, "Buy milk", listId, goalId, 2)),
            new FakeLabelStore(
                Label(listId, LabelKind.List, "Errands"),
                Label(goalId, LabelKind.Goal, "Eat better")),
            new FakeSnapshots(new Dictionary<Guid, TaskSnapshot>
            {
                [taskId] = new(TaskStatus.Done, "Buy milk")
            }));

        var view = Assert.Single(
            await reader.ReadAsync(User, null, 50, TestContext.Current.CancellationToken));

        Assert.Equal("Errands", view.ListName);
        Assert.Equal("Eat better", view.GoalName);
        Assert.Equal(2, view.CompletionNumber);
        Assert.Equal("Completed", view.Kind);
        Assert.Equal("Done", view.CurrentStatus);
    }

    [Fact]
    public async Task OneLookupCoversEveryDistinctTaskOnThePage()
    {
        var taskId = Guid.NewGuid();
        var snapshots = new FakeSnapshots([]);
        var reader = ReaderOver(
            new FakeStore(
                Record(1, RecordKind.Created, taskId),
                Record(2, RecordKind.Completed, taskId)),
            new FakeLabelStore(),
            snapshots);

        await reader.ReadAsync(User, null, 50, TestContext.Current.CancellationToken);

        Assert.Equal([taskId], Assert.Single(snapshots.Lookups));
    }

    [Fact]
    public async Task AnEmptyPageNeverReachesForSnapshots()
    {
        var snapshots = new FakeSnapshots([]);
        var reader = ReaderOver(new FakeStore(), new FakeLabelStore(), snapshots);

        Assert.Empty(await reader.ReadAsync(User, null, 50, TestContext.Current.CancellationToken));
        Assert.Empty(snapshots.Lookups);
    }

    [Fact]
    public async Task AnAbsurdLimitIsCappedAtTwoHundred()
    {
        var store = new FakeStore();

        await ReaderOver(store, new FakeLabelStore(), new FakeSnapshots([]))
            .ReadAsync(User, null, 5000, TestContext.Current.CancellationToken);

        Assert.Equal(200, store.Pages[0].Limit);
    }

    [Fact]
    public async Task AnAbsentLimitFallsBackToFifty()
    {
        var store = new FakeStore();

        await ReaderOver(store, new FakeLabelStore(), new FakeSnapshots([]))
            .ReadAsync(User, null, 0, TestContext.Current.CancellationToken);

        Assert.Equal(50, store.Pages[0].Limit);
    }

    [Fact]
    public async Task TheBeforeMarkerReachesTheStore()
    {
        var store = new FakeStore();

        await ReaderOver(store, new FakeLabelStore(), new FakeSnapshots([]))
            .ReadAsync(User, 120, 50, TestContext.Current.CancellationToken);

        Assert.Equal(120, store.Pages[0].Before);
    }
}
