using System.Text.Json;
using PSPad.App.Api;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.App.Sync;
using PSPad.Contracts;
using PSPad.Module.Money;
using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.Module.Money.Preferences;
using PSPad.Module.Money.Values;
using PSPad.Module.Presentation.AreaViews;
using PSPad.Abstractions;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Sync;

[UnitTest]
public class SyncServiceTests
{
    static readonly Guid User = Guid.NewGuid();

    sealed class FakeApi : ISyncApi
    {
        public List<IReadOnlyList<CommandEnvelope>> Sent { get; } = [];

        public Func<IReadOnlyList<CommandEnvelope>, IReadOnlyList<CommandResponse>> Respond { get; set; } =
            envelopes => envelopes.Select(_ => new CommandResponse(Guid.NewGuid(), true, null)).ToArray();

        public SyncResponse Pull { get; set; } =
            new(0, new Dictionary<string, JsonElement[]>());

        public Queue<SyncResponse> Pages { get; } = new();

        public List<long> Requests { get; } = [];

        public long? RequestedSince { get; private set; }

        public Task<IReadOnlyList<CommandResponse>> SendAsync(IReadOnlyList<CommandEnvelope> envelopes)
        {
            Sent.Add(envelopes);
            return Task.FromResult(Respond(envelopes));
        }

        public Task<SyncResponse?> SyncAsync(long since, IReadOnlyCollection<Guid> full)
        {
            RequestedSince = since;
            Requests.Add(since);
            return Task.FromResult<SyncResponse?>(Pages.TryDequeue(out var page) ? page : Pull);
        }

        public Task<JoinOutcome> JoinAsync(string token, string code) => Task.FromResult<JoinOutcome>(new JoinOutcome.Invalid());
    }

    [Fact]
    public async Task NothingIsSentWhenTheOutboxIsEmpty()
    {
        var api = new FakeApi();
        var outcome = await ServiceFor(api, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.Empty(api.Sent);
        Assert.Equal(0, outcome.Pushed);
    }

    [Fact]
    public async Task AcceptedCommandsLeaveTheOutbox()
    {
        var outbox = new InMemoryOutbox();
        await outbox.AppendAsync(Guid.NewGuid(), Envelope());
        await outbox.AppendAsync(Guid.NewGuid(), Envelope());
        var api = new FakeApi();

        var outcome = await ServiceFor(api, outbox).SyncAsync(CancellationToken.None);

        Assert.Equal(2, outcome.Pushed);
        Assert.Equal(0, await outbox.CountAsync());
    }

    [Fact]
    public async Task TheOutboxStopsAtTheFirstRejectionAndKeepsWhatFollows()
    {
        var outbox = new InMemoryOutbox();
        await outbox.AppendAsync(Guid.NewGuid(), Envelope());
        await outbox.AppendAsync(Guid.NewGuid(), Envelope());
        await outbox.AppendAsync(Guid.NewGuid(), Envelope());
        var api = new FakeApi
        {
            Respond = envelopes =>
            [
                new CommandResponse(Guid.NewGuid(), true, null),
                new CommandResponse(Guid.NewGuid(), false, "That list no longer exists."),
                new CommandResponse(Guid.NewGuid(), true, null)
            ]
        };

        var outcome = await ServiceFor(api, outbox).SyncAsync(CancellationToken.None);

        Assert.Equal(1, outcome.Pushed);
        Assert.Equal("That list no longer exists.", Assert.Single(outcome.Rejections));
        Assert.Equal(2, await outbox.CountAsync());
    }

    [Fact]
    public async Task TheOutcomeNamesTheRejectedCommand()
    {
        var outbox = new InMemoryOutbox();
        var rejected = Guid.NewGuid();
        await outbox.AppendAsync(Guid.NewGuid(), Envelope());
        await outbox.AppendAsync(rejected, Envelope());
        var api = new FakeApi
        {
            Respond = envelopes =>
            [
                new CommandResponse(Guid.NewGuid(), true, null),
                new CommandResponse(Guid.NewGuid(), false, "That list no longer exists.")
            ]
        };

        var outcome = await ServiceFor(api, outbox).SyncAsync(CancellationToken.None);

        Assert.Equal(rejected, Assert.Single(outcome.RejectedCommands));
    }

    [Fact]
    public async Task AnUnrecoverableRejectionIsDroppedInsteadOfWedgingTheOutbox()
    {
        var outbox = new InMemoryOutbox();
        await outbox.AppendAsync(Guid.NewGuid(), Envelope());
        await outbox.AppendAsync(Guid.NewGuid(), Envelope());
        await outbox.AppendAsync(Guid.NewGuid(), Envelope());
        var api = new FakeApi
        {
            Respond = envelopes =>
            [
                new CommandResponse(Guid.NewGuid(), true, null),
                new CommandResponse(Guid.NewGuid(), false, "That command is for a different user.", Unrecoverable: true),
                new CommandResponse(Guid.NewGuid(), true, null)
            ]
        };

        var outcome = await ServiceFor(api, outbox).SyncAsync(CancellationToken.None);

        Assert.Equal(1, outcome.Pushed);
        Assert.Equal("That command is for a different user.", Assert.Single(outcome.Rejections));
        Assert.Equal(1, await outbox.CountAsync());
    }

    [Fact]
    public async Task PulledDocumentsOverwriteTheReplicaAndAdvanceTheMarker()
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(
            null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), "Home", 0), DateTimeOffset.UnixEpoch));
        var replica = new InMemoryReplica();
        var api = new FakeApi
        {
            Pull = new SyncResponse(
                17,
                new Dictionary<string, JsonElement[]>
                {
                    ["areas"] = [JsonSerializer.SerializeToElement(area)]
                })
        };

        var outcome = await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.Equal(1, outcome.Pulled);
        Assert.Equal(17, await replica.MarkerAsync());
        Assert.Equal("Home", (await replica.LoadAsync<Area>(area.Id))!.Name);
    }

    [Fact]
    public async Task PullKeepsAskingForPagesUntilTheServerHasNoMore()
    {
        var replica = new InMemoryReplica();
        var api = new FakeApi();
        api.Pages.Enqueue(new SyncResponse(10, AreaRows("One"), HasMore: true));
        api.Pages.Enqueue(new SyncResponse(20, AreaRows("Two"), HasMore: true));
        api.Pages.Enqueue(new SyncResponse(25, AreaRows("Three")));

        var outcome = await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.Equal([0L, 10L, 20L], api.Requests);
        Assert.Equal(3, outcome.Pulled);
        Assert.Equal(25, await replica.MarkerAsync());
    }

    [Fact]
    public async Task AFailedPageKeepsTheMarkerOfThePagesAlreadySaved()
    {
        var replica = new InMemoryReplica();
        var api = new FailingAfterFirstPageApi(new SyncResponse(10, AreaRows("One"), HasMore: true));

        var outcome = await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.False(outcome.ReachedServer);
        Assert.Equal(10, await replica.MarkerAsync());
        Assert.Equal(SyncService.CollectionsFingerprint, await replica.CollectionsFingerprintAsync());
    }

    [Fact]
    public async Task APageThatDoesNotAdvanceTheMarkerEndsThePull()
    {
        var api = new FakeApi { Pull = new SyncResponse(0, new Dictionary<string, JsonElement[]>(), HasMore: true) };

        await new SyncService(api, new InMemoryReplica(), new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.Single(api.Requests);
    }

    sealed class FailingAfterFirstPageApi(SyncResponse first) : ISyncApi
    {
        int _calls;

        public Task<IReadOnlyList<CommandResponse>> SendAsync(IReadOnlyList<CommandEnvelope> envelopes) =>
            Task.FromResult<IReadOnlyList<CommandResponse>>([]);

        public Task<SyncResponse?> SyncAsync(long since, IReadOnlyCollection<Guid> full) =>
            Task.FromResult(_calls++ == 0 ? first : null);

        public Task<JoinOutcome> JoinAsync(string token, string code) =>
            Task.FromResult<JoinOutcome>(new JoinOutcome.Invalid());
    }

    static Dictionary<string, JsonElement[]> AreaRows(string name)
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(
            null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), name, 0), DateTimeOffset.UnixEpoch));
        return new Dictionary<string, JsonElement[]> { ["areas"] = [JsonSerializer.SerializeToElement(area)] };
    }

    [Fact]
    public async Task APulledAreaViewLandsInTheReplica()
    {
        var replica = new InMemoryReplica();
        var list = Guid.NewGuid();
        var view = new AreaView();
        view.Apply(new ListsReordered(Guid.NewGuid(), User, DateTimeOffset.UnixEpoch, Guid.NewGuid(), [list]));
        var api = new FakeApi
        {
            Pull = new(5, new Dictionary<string, JsonElement[]>
            {
                ["areaviews"] = [JsonSerializer.SerializeToElement(view, new JsonSerializerOptions(JsonSerializerDefaults.Web))]
            })
        };

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        var stored = await replica.LoadAsync<AreaView>(view.Id);
        Assert.Equal([list], stored!.Order);
    }

    [Fact]
    public async Task APulledBudgetLandsInTheReplicaWithItsCategories()
    {
        var replica = new InMemoryReplica();
        var budget = new Budget();
        budget.ApplyAll(Budget.Decide(null, new CreateBudget(Guid.NewGuid(), User, Guid.NewGuid(), "Personal"),
            DateTimeOffset.UnixEpoch));
        var row = JsonSerializer.SerializeToElement(budget,
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { IncludeFields = true });
        var api = new FakeApi
        {
            Pull = new(5, new Dictionary<string, JsonElement[]> { ["budgets"] = [row] })
        };

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        var stored = await replica.LoadAsync<Budget>(budget.Id);
        Assert.NotNull(stored);
        Assert.Equal(8, stored.ExpenseCategories.Count);
    }

    [Fact]
    public void TheFingerprintNamesTheMoneyCollections()
    {
        Assert.Contains("budgets", SyncService.CollectionsFingerprint);
        Assert.Contains("moneypreferences", SyncService.CollectionsFingerprint);
        Assert.Contains("moneyentries", SyncService.CollectionsFingerprint);
        Assert.Contains("balancesnapshots", SyncService.CollectionsFingerprint);
    }

    [Fact]
    public async Task APulledMoneyEntryLandsInTheReplicaWithItsMoney()
    {
        var replica = new InMemoryReplica();
        var budget = new Budget();
        budget.ApplyAll(Budget.Decide(null, new CreateBudget(Guid.NewGuid(), User, Guid.NewGuid(), "Personal"),
            DateTimeOffset.UnixEpoch));
        var day = new DateOnly(2026, 10, 9);
        var entry = new MoneyEntry();
        entry.ApplyAll(MoneyEntry.Decide(null, budget, new RecordExpense(Guid.NewGuid(), User, Guid.NewGuid(), budget.Id,
            "Lego", "Hobby", new Money(12.5m, "EUR", 4.2512m, day), day, null), DateTimeOffset.UnixEpoch));
        var row = JsonSerializer.SerializeToElement(entry,
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { IncludeFields = true });
        var api = new FakeApi
        {
            Pull = new(5, new Dictionary<string, JsonElement[]> { ["moneyentries"] = [row] })
        };

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        var stored = await replica.LoadAsync<MoneyEntry>(entry.Id);
        Assert.Equal(new Money(12.5m, "EUR", 4.2512m, day), stored!.Money);
        Assert.Equal(53.14m, stored.Money.InPln);
        Assert.Equal("Hobby", stored.Category);
    }

    [Fact]
    public async Task APulledDefaultCurrencyLandsInTheReplica()
    {
        var replica = new InMemoryReplica();
        var preferences = new MoneyPreferences();
        preferences.ApplyAll(MoneyPreferences.Decide(null, new SetDefaultCurrency(Guid.NewGuid(), User, "EUR"),
            DateTimeOffset.UnixEpoch));
        var row = JsonSerializer.SerializeToElement(preferences,
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { IncludeFields = true });
        var api = new FakeApi
        {
            Pull = new(5, new Dictionary<string, JsonElement[]> { ["moneypreferences"] = [row] })
        };

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        var stored = await replica.LoadAsync<MoneyPreferences>(MoneyPreferences.IdFor(User));
        Assert.Equal("EUR", stored!.DefaultCurrency);
    }

    [Fact]
    public async Task AStoredFingerprintMissingANewCollectionPullsFromZeroOnce()
    {
        var replica = new InMemoryReplica();
        await replica.SetMarkerAsync(42);
        await replica.SetCollectionsFingerprintAsync("areas,goals,inboxes,tasklists,todotasks");
        var api = new FakeApi();

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.Equal(0, api.RequestedSince);
    }

    [Fact]
    public async Task AMatchingStoredFingerprintPullsFromTheStoredMarker()
    {
        var replica = new InMemoryReplica();
        await replica.SetMarkerAsync(42);
        await replica.SetCollectionsFingerprintAsync(SyncService.CollectionsFingerprint);
        var api = new FakeApi();

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.Equal(42, api.RequestedSince);
    }

    [Fact]
    public async Task APullStoresTheCurrentFingerprintAlongsideTheMarker()
    {
        var replica = new InMemoryReplica();
        var api = new FakeApi();

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.Equal(SyncService.CollectionsFingerprint, await replica.CollectionsFingerprintAsync());
    }

    [Fact]
    public async Task ABatchStopsBeforeItsEnvelopesPassTheByteBudget()
    {
        var outbox = new InMemoryOutbox();
        for (var index = 0; index < 20; index++)
        {
            await outbox.AppendAsync(Guid.NewGuid(), LongestDescription());
        }

        var api = new FakeApi();

        var outcome = await ServiceFor(api, outbox).SyncAsync(CancellationToken.None);

        var batch = Assert.Single(api.Sent);
        Assert.InRange(batch.Count, 1, 19);
        Assert.True(batch.Sum(SizeOf) <= SyncService.BatchBytes);
        Assert.Equal(batch.Count, outcome.Pushed);
        Assert.Equal(20 - batch.Count, await outbox.CountAsync());
    }

    [Fact]
    public async Task AnEnvelopeOverTheByteBudgetStillGoesAloneRatherThanWedgingTheOutbox()
    {
        var outbox = new InMemoryOutbox();
        var huge = new CommandEnvelope("Huge", JsonSerializer.SerializeToElement(new string('x', SyncService.BatchBytes + 1)));
        await outbox.AppendAsync(Guid.NewGuid(), huge);
        await outbox.AppendAsync(Guid.NewGuid(), Envelope());
        var api = new FakeApi();

        await ServiceFor(api, outbox).SyncAsync(CancellationToken.None);

        Assert.Single(Assert.Single(api.Sent));
    }

    static int SizeOf(CommandEnvelope envelope) =>
        JsonSerializer.SerializeToUtf8Bytes(envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web)).Length;

    static CommandEnvelope LongestDescription() =>
        new(nameof(SetTaskDescription), JsonSerializer.SerializeToElement(
            new SetTaskDescription(Guid.NewGuid(), User, Guid.NewGuid(), new string('ł', ContentLimits.Description))));

    [Fact]
    public async Task APulledCopyOlderThanTheLocalOneIsIgnored()
    {
        var replica = new InMemoryReplica();
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), "Buy milk"),
            DateTimeOffset.UnixEpoch));
        var stale = Row(task);
        task.ApplyAll(TodoTask.Decide(task, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, new DateOnly(2026, 10, 30)),
            DateTimeOffset.UnixEpoch));
        await replica.SaveAsync(task);
        var api = new FakeApi { Pull = new(5, new Dictionary<string, JsonElement[]> { ["todotasks"] = [stale] }) };

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        var stored = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Equal(new DateOnly(2026, 10, 30), stored!.DueOn);
        Assert.Equal(2, stored.Version);
    }

    [Fact]
    public async Task APulledCopyNewerThanTheLocalOneReplacesIt()
    {
        var replica = new InMemoryReplica();
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), "Buy milk"),
            DateTimeOffset.UnixEpoch));
        await replica.SaveAsync(task);
        var elsewhere = JsonSerializer.Deserialize<TodoTask>(Row(task).GetRawText(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { IncludeFields = true })!;
        elsewhere.ApplyAll(TodoTask.Decide(elsewhere, new RenameTask(Guid.NewGuid(), User, task.Id, "Buy oat milk"),
            DateTimeOffset.UnixEpoch));
        var api = new FakeApi { Pull = new(5, new Dictionary<string, JsonElement[]> { ["todotasks"] = [Row(elsewhere)] }) };

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.Equal("Buy oat milk", (await replica.LoadAsync<TodoTask>(task.Id))!.Name);
    }

    [Fact]
    public async Task ALocalCommitLandingWhileAPullInspectsTheReplicaIsKept()
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(null, new CreateTask(Guid.NewGuid(), User, Guid.NewGuid(), Guid.NewGuid(), "Buy milk"),
            DateTimeOffset.UnixEpoch));
        var pulled = Row(task);
        var replica = new InterleavingReplica();
        await replica.SaveAsync(task);
        var edited = JsonSerializer.Deserialize<TodoTask>(pulled.GetRawText(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { IncludeFields = true })!;
        edited.ApplyAll(TodoTask.Decide(edited, new SetTaskDueDate(Guid.NewGuid(), User, task.Id, new DateOnly(2026, 10, 30)),
            DateTimeOffset.UnixEpoch));
        replica.CommitWhileInspected = edited;
        var api = new FakeApi { Pull = new(5, new Dictionary<string, JsonElement[]> { ["todotasks"] = [pulled] }) };

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        var stored = await replica.LoadAsync<TodoTask>(task.Id);
        Assert.Equal(2, stored!.Version);
        Assert.Equal(new DateOnly(2026, 10, 30), stored.DueOn);
    }

    // A command handler's commit can run between any two awaits of a pull; this fake lands one at the pull's first look at the document.
    sealed class InterleavingReplica : IReplica
    {
        readonly InMemoryReplica _inner = new();

        public Aggregate? CommitWhileInspected { get; set; }

        public Task<T?> LoadAsync<T>(Guid id) where T : Aggregate => _inner.LoadAsync<T>(id);

        public async Task<Aggregate?> LoadAsync(Type type, Guid id)
        {
            var held = await _inner.LoadAsync(type, id);
            await LandCommitAsync();
            return held;
        }

        public Task<IReadOnlyList<T>> LoadAllAsync<T>(Guid userId) where T : Aggregate => _inner.LoadAllAsync<T>(userId);

        public Task SaveAsync(Aggregate aggregate) => _inner.SaveAsync(aggregate);

        public async Task<bool> SaveUnlessNewerAsync(Aggregate aggregate)
        {
            await LandCommitAsync();
            return await _inner.SaveUnlessNewerAsync(aggregate);
        }

        async Task LandCommitAsync()
        {
            if (CommitWhileInspected is { } commit)
            {
                CommitWhileInspected = null;
                await _inner.SaveAsync(commit);
            }
        }

        public Task RemoveAsync(Guid id) => _inner.RemoveAsync(id);

        public Task<long> MarkerAsync() => _inner.MarkerAsync();

        public Task SetMarkerAsync(long marker) => _inner.SetMarkerAsync(marker);

        public Task<string?> CollectionsFingerprintAsync() => _inner.CollectionsFingerprintAsync();

        public Task SetCollectionsFingerprintAsync(string fingerprint) => _inner.SetCollectionsFingerprintAsync(fingerprint);

        public Task<IReadOnlyCollection<Guid>> OwedWholeAsync() => _inner.OwedWholeAsync();

        public Task SetOwedWholeAsync(IReadOnlyCollection<Guid> containers) => _inner.SetOwedWholeAsync(containers);

        public Task<DateTimeOffset?> LastSyncedAtAsync() => _inner.LastSyncedAtAsync();

        public Task SetLastSyncedAtAsync(DateTimeOffset at) => _inner.SetLastSyncedAtAsync(at);

        public Task<Guid?> OwnerAsync() => _inner.OwnerAsync();

        public Task SetOwnerAsync(Guid userId) => _inner.SetOwnerAsync(userId);

        public Task ClearAsync() => _inner.ClearAsync();
    }

    static JsonElement Row(Aggregate aggregate) =>
        JsonSerializer.SerializeToElement(aggregate, aggregate.GetType(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { IncludeFields = true });

    static SyncService ServiceFor(ISyncApi api, IOutbox outbox) =>
        new(api, new InMemoryReplica(), outbox);

    static CommandEnvelope Envelope() =>
        new(nameof(CreateArea), JsonSerializer.SerializeToElement(
            new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), "Home", 0)));

    [Fact]
    public async Task APulledBalanceLandsInTheReplicaWithItsMonthAndHoldings()
    {
        var replica = new InMemoryReplica();
        var budget = new Budget();
        budget.ApplyAll(Budget.Decide(null, new CreateBudget(Guid.NewGuid(), User, Guid.NewGuid(), "Personal"),
            DateTimeOffset.UnixEpoch));
        var october = new YearMonth(2026, 10);
        var holding = new Holding(Guid.NewGuid(), "Konto", HoldingType.Savings, new Money(10m, "EUR", 4.25m, october.LastDay));
        var snapshot = new BalanceSnapshot();
        snapshot.ApplyAll(BalanceSnapshot.Decide(null, budget,
            new StartBalanceMonth(Guid.NewGuid(), User, budget.Id, october), DateTimeOffset.UnixEpoch));
        snapshot.ApplyAll(BalanceSnapshot.Decide(snapshot, budget, new AddHolding(Guid.NewGuid(), User, budget.Id, october,
            holding.HoldingId, holding.Name, holding.Type, holding.Money), DateTimeOffset.UnixEpoch));
        var row = JsonSerializer.SerializeToElement(snapshot,
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { IncludeFields = true });
        var api = new FakeApi
        {
            Pull = new(5, new Dictionary<string, JsonElement[]> { ["balancesnapshots"] = [row] })
        };

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        var stored = await replica.LoadAsync<BalanceSnapshot>(snapshot.Id);
        Assert.Equal(october, stored!.Month);
        Assert.Equal(holding, Assert.Single(stored.Holdings));
        Assert.Equal(42.50m, stored.Holdings[0].Money.InPln);
    }
}
