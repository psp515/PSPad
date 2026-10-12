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
using PSPad.Module.Money.Values;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Sync;

[UnitTest]
public class MembershipReconcileTests
{
    static readonly Guid Me = Guid.NewGuid();
    static readonly Guid Owner = Guid.NewGuid();

    [Fact]
    public async Task AListThatLeftTheSetIsPurgedWithItsChildren()
    {
        var listId = Guid.NewGuid();
        var replica = new InMemoryReplica();
        await replica.SetOwnerAsync(Me);
        await replica.SaveAsync(ListOwnedBy(Owner, listId));
        await replica.SaveAsync(TaskIn(listId));
        await replica.SaveAsync(ItemIn(listId));
        var api = new ScriptedApi(new SyncResponse(1, new Dictionary<string, JsonElement[]>(), MemberListIds: []));

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.Null(await replica.LoadAsync<TaskList>(listId));
        Assert.Empty(await replica.LoadAllAsync<TodoTask>(Guid.Empty));
        Assert.Empty(await replica.LoadAllAsync<ReferenceItem>(Guid.Empty));
    }

    [Fact]
    public async Task ARowMovedIntoAListIDoNotHoldIsDropped()
    {
        var sharedId = Guid.NewGuid();
        var privateId = Guid.NewGuid();
        var replica = new InMemoryReplica();
        await replica.SetOwnerAsync(Me);
        await replica.SaveAsync(ListOwnedBy(Owner, sharedId));
        var task = TaskIn(sharedId);
        var item = ItemIn(sharedId);
        await replica.SaveAsync(task);
        await replica.SaveAsync(item);
        task.ApplyAll(TodoTask.Decide(
            task, new MoveTaskToList(Guid.NewGuid(), Owner, task.Id, privateId), DateTimeOffset.UnixEpoch));
        item.ApplyAll(ReferenceItem.Decide(
            item, new MoveReferenceItemToList(Guid.NewGuid(), Owner, item.Id, privateId), DateTimeOffset.UnixEpoch));
        var api = new ScriptedApi(new SyncResponse(2, new Dictionary<string, JsonElement[]>
        {
            ["todotasks"] = [Serialize(task)],
            ["referenceitems"] = [Serialize(item)]
        }, MemberListIds: [sharedId]));

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.NotNull(await replica.LoadAsync<TaskList>(sharedId));
        Assert.Null(await replica.LoadAsync<TodoTask>(task.Id));
        Assert.Null(await replica.LoadAsync<ReferenceItem>(item.Id));
    }

    [Fact]
    public async Task AMovedOutStubIsDropped()
    {
        var sharedId = Guid.NewGuid();
        var replica = new InMemoryReplica();
        await replica.SetOwnerAsync(Me);
        await replica.SaveAsync(ListOwnedBy(Owner, sharedId));
        var task = TaskIn(sharedId);
        await replica.SaveAsync(task);
        var stub = JsonSerializer.SerializeToElement(new
        {
            id = task.Id, userId = Owner, listId = Guid.NewGuid(), previousListId = sharedId,
            version = 3, deleted = false, seq = 9L
        });
        var api = new ScriptedApi(new SyncResponse(9, new Dictionary<string, JsonElement[]>
        {
            ["todotasks"] = [stub]
        }, MemberListIds: [sharedId]));

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.Null(await replica.LoadAsync<TodoTask>(task.Id));
    }

    [Fact]
    public async Task MyOwnTasksStayEvenWhenTheirListHasNotArrived()
    {
        var replica = new InMemoryReplica();
        await replica.SetOwnerAsync(Me);
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), Me, Guid.NewGuid(), Guid.NewGuid(), "Mine"),
            DateTimeOffset.UnixEpoch));
        await replica.SaveAsync(task);
        var api = new ScriptedApi(new SyncResponse(1, new Dictionary<string, JsonElement[]>(), MemberListIds: []));

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.NotNull(await replica.LoadAsync<TodoTask>(task.Id));
    }

    [Fact]
    public async Task MyOwnListsAreNeverPurged()
    {
        var listId = Guid.NewGuid();
        var replica = new InMemoryReplica();
        await replica.SetOwnerAsync(Me);
        await replica.SaveAsync(ListOwnedBy(Me, listId));
        var api = new ScriptedApi(new SyncResponse(1, new Dictionary<string, JsonElement[]>(), MemberListIds: []));

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.NotNull(await replica.LoadAsync<TaskList>(listId));
    }

    [Fact]
    public async Task AMissingMemberListIsPulledWhole()
    {
        var listId = Guid.NewGuid();
        var replica = new InMemoryReplica();
        await replica.SetOwnerAsync(Me);
        var list = ListOwnedBy(Owner, listId);
        var task = TaskIn(listId);
        var api = new ScriptedApi(
            new SyncResponse(5, new Dictionary<string, JsonElement[]>(), MemberListIds: [listId]),
            new SyncResponse(5, new Dictionary<string, JsonElement[]>
            {
                ["tasklists"] = [Serialize(list)],
                ["todotasks"] = [Serialize(task)]
            }));

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.NotNull(await replica.LoadAsync<TaskList>(listId));
        Assert.NotNull(await replica.LoadAsync<TodoTask>(task.Id));
        Assert.Equal(2, api.FullRequests.Count);
        Assert.Equal(new[] { listId }, api.FullRequests[1]);
    }

    [Fact]
    public async Task AFailedFullPullNeverStallsTheMarker()
    {
        var listId = Guid.NewGuid();
        var replica = new InMemoryReplica();
        await replica.SetOwnerAsync(Me);
        var area = new Module.Tasks.Areas.Area();
        area.ApplyAll(Module.Tasks.Areas.Area.Decide(
            null, new Module.Tasks.Areas.CreateArea(Guid.NewGuid(), Me, Guid.NewGuid(), "Home", 0),
            DateTimeOffset.UnixEpoch));
        var api = new ScriptedApi(
            new SyncResponse(5, new Dictionary<string, JsonElement[]>
            {
                ["areas"] = [Serialize(area)]
            }, MemberListIds: [listId]),
            throwOnFull: true);

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.NotNull(await replica.LoadAsync<Module.Tasks.Areas.Area>(area.Id));
        Assert.Equal(5, await replica.MarkerAsync());
    }

    [Fact]
    public async Task AnOldServerWithoutTheSetPurgesNothing()
    {
        var listId = Guid.NewGuid();
        var replica = new InMemoryReplica();
        await replica.SetOwnerAsync(Me);
        await replica.SaveAsync(ListOwnedBy(Owner, listId));
        var api = new ScriptedApi(new SyncResponse(1, new Dictionary<string, JsonElement[]>(), MemberListIds: null));

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.NotNull(await replica.LoadAsync<TaskList>(listId));
    }

    [Fact]
    public async Task NoRecordedOwnerPurgesNothing()
    {
        var listId = Guid.NewGuid();
        var replica = new InMemoryReplica();
        await replica.SaveAsync(ListOwnedBy(Owner, listId));
        var api = new ScriptedApi(new SyncResponse(1, new Dictionary<string, JsonElement[]>(), MemberListIds: []));

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.NotNull(await replica.LoadAsync<TaskList>(listId));
    }

    [Fact]
    public async Task LoadAllReturnsSharedRowsToo()
    {
        var replica = new InMemoryReplica();
        var listId = Guid.NewGuid();
        await replica.SaveAsync(ListOwnedBy(Owner, listId));

        var all = await replica.LoadAllAsync<TaskList>(Me);

        Assert.Contains(all, list => list.Id == listId);
    }

    [Fact]
    public async Task JoiningWritesTheReturnedDocuments()
    {
        var listId = Guid.NewGuid();
        var list = ListOwnedBy(Owner, listId);
        var replica = new InMemoryReplica();
        var api = new ScriptedApi(join: new JoinOutcome.Joined(listId, new Dictionary<string, JsonElement[]>
        {
            ["tasklists"] = [Serialize(list)]
        }));

        var joined = await new SyncService(api, replica, new InMemoryOutbox())
            .JoinAsync("token", "K7M4PX", CancellationToken.None);

        Assert.Equal(listId, Assert.IsType<JoinOutcome.Joined>(joined).Id);
        Assert.Equal(("token", "K7M4PX"), api.JoinedWith);
        Assert.NotNull(await replica.LoadAsync<TaskList>(listId));
    }

    [Fact]
    public async Task AFailedJoinHandsBackItsOutcome()
    {
        var api = new ScriptedApi(join: new JoinOutcome.Expired());

        var joined = await new SyncService(api, new InMemoryReplica(), new InMemoryOutbox())
            .JoinAsync("token", "K7M4PX", CancellationToken.None);

        Assert.IsType<JoinOutcome.Expired>(joined);
    }

    [Fact]
    public async Task ABudgetThatLeftTheSetIsPurgedWithItsEntriesAndSnapshots()
    {
        var budget = BudgetOwnedBy(Owner);
        var replica = new InMemoryReplica();
        await replica.SetOwnerAsync(Me);
        await replica.SaveAsync(budget);
        await replica.SaveAsync(EntryIn(budget));
        await replica.SaveAsync(SnapshotIn(budget));
        var api = new ScriptedApi(new SyncResponse(1, new Dictionary<string, JsonElement[]>(), MemberBudgetIds: []));

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.Null(await replica.LoadAsync<Budget>(budget.Id));
        Assert.Empty(await replica.LoadAllAsync<MoneyEntry>(Guid.Empty));
        Assert.Empty(await replica.LoadAllAsync<BalanceSnapshot>(Guid.Empty));
    }

    [Fact]
    public async Task MyOwnBudgetsAreNeverPurged()
    {
        var budget = BudgetOwnedBy(Me);
        var replica = new InMemoryReplica();
        await replica.SetOwnerAsync(Me);
        await replica.SaveAsync(budget);
        await replica.SaveAsync(EntryIn(budget));
        var api = new ScriptedApi(new SyncResponse(1, new Dictionary<string, JsonElement[]>(), MemberBudgetIds: []));

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.NotNull(await replica.LoadAsync<Budget>(budget.Id));
        Assert.Single(await replica.LoadAllAsync<MoneyEntry>(Guid.Empty));
    }

    [Fact]
    public async Task AMissingMemberBudgetIsPulledWhole()
    {
        var budget = BudgetOwnedBy(Owner);
        var entry = EntryIn(budget);
        var replica = new InMemoryReplica();
        await replica.SetOwnerAsync(Me);
        var api = new ScriptedApi(
            new SyncResponse(5, new Dictionary<string, JsonElement[]>(), MemberListIds: [], MemberBudgetIds: [budget.Id]),
            new SyncResponse(5, new Dictionary<string, JsonElement[]>
            {
                ["budgets"] = [Serialize(budget)],
                ["moneyentries"] = [Serialize(entry)]
            }));

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.NotNull(await replica.LoadAsync<Budget>(budget.Id));
        Assert.NotNull(await replica.LoadAsync<MoneyEntry>(entry.Id));
        Assert.Equal(new[] { budget.Id }, api.FullRequests[1]);
    }

    [Fact]
    public async Task AnEntryOfABudgetIDoNotHoldIsDropped()
    {
        var budget = BudgetOwnedBy(Owner);
        var replica = new InMemoryReplica();
        await replica.SetOwnerAsync(Me);
        var entry = EntryIn(budget);
        var snapshot = SnapshotIn(budget);
        var api = new ScriptedApi(new SyncResponse(2, new Dictionary<string, JsonElement[]>
        {
            ["moneyentries"] = [Serialize(entry)],
            ["balancesnapshots"] = [Serialize(snapshot)]
        }, MemberBudgetIds: []));

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.Null(await replica.LoadAsync<MoneyEntry>(entry.Id));
        Assert.Null(await replica.LoadAsync<BalanceSnapshot>(snapshot.Id));
    }

    [Fact]
    public async Task AnOldServerWithoutTheBudgetSetPurgesNoBudget()
    {
        var budget = BudgetOwnedBy(Owner);
        var replica = new InMemoryReplica();
        await replica.SetOwnerAsync(Me);
        await replica.SaveAsync(budget);
        var api = new ScriptedApi(new SyncResponse(1, new Dictionary<string, JsonElement[]>(), MemberListIds: []));

        await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.NotNull(await replica.LoadAsync<Budget>(budget.Id));
    }

    [Fact]
    public async Task AdoptingWritesJoinedDocuments()
    {
        var budget = BudgetOwnedBy(Owner);
        var replica = new InMemoryReplica();

        await new SyncService(new ScriptedApi(), replica, new InMemoryOutbox()).AdoptAsync(
            new Dictionary<string, JsonElement[]> { ["budgets"] = [Serialize(budget)] });

        Assert.NotNull(await replica.LoadAsync<Budget>(budget.Id));
    }

    static Budget BudgetOwnedBy(Guid owner)
    {
        var budget = new Budget();
        budget.ApplyAll(Budget.Decide(null, new CreateBudget(Guid.NewGuid(), owner, Guid.NewGuid(), "Household"),
            DateTimeOffset.UnixEpoch));
        return budget;
    }

    static MoneyEntry EntryIn(Budget budget)
    {
        var day = new DateOnly(2026, 10, 9);
        var entry = new MoneyEntry();
        entry.ApplyAll(MoneyEntry.Decide(null, budget, new RecordExpense(Guid.NewGuid(), budget.UserId, Guid.NewGuid(),
            budget.Id, "Bread", "Food", new Money(5m, "PLN", 1m, day), day, null), DateTimeOffset.UnixEpoch));
        return entry;
    }

    static BalanceSnapshot SnapshotIn(Budget budget)
    {
        var snapshot = new BalanceSnapshot();
        snapshot.ApplyAll(BalanceSnapshot.Decide(null, budget,
            new StartBalanceMonth(Guid.NewGuid(), budget.UserId, budget.Id, new YearMonth(2026, 10)), DateTimeOffset.UnixEpoch));
        return snapshot;
    }

    static TaskList ListOwnedBy(Guid owner, Guid listId)
    {
        var list = new TaskList();
        list.ApplyAll(TaskList.Decide(
            null, new CreateTaskList(Guid.NewGuid(), owner, listId, Guid.NewGuid(), "Shared"),
            DateTimeOffset.UnixEpoch));
        return list;
    }

    static TodoTask TaskIn(Guid listId)
    {
        var task = new TodoTask();
        task.ApplyAll(TodoTask.Decide(
            null, new CreateTask(Guid.NewGuid(), Owner, Guid.NewGuid(), listId, "Water the plants"),
            DateTimeOffset.UnixEpoch));
        return task;
    }

    static ReferenceItem ItemIn(Guid listId)
    {
        var item = new ReferenceItem();
        item.ApplyAll(ReferenceItem.Decide(
            null, new CreateReferenceItem(Guid.NewGuid(), Owner, Guid.NewGuid(), listId, "PLA Black", 0),
            DateTimeOffset.UnixEpoch));
        return item;
    }

    static JsonElement Serialize<T>(T value) =>
        JsonSerializer.SerializeToElement(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    sealed class ScriptedApi(
        SyncResponse? first = null,
        SyncResponse? second = null,
        JoinOutcome? join = null,
        bool throwOnFull = false) : ISyncApi
    {
        int _calls;

        public List<IReadOnlyCollection<Guid>> FullRequests { get; } = [];

        public Task<IReadOnlyList<CommandResponse>> SendAsync(IReadOnlyList<CommandEnvelope> envelopes) =>
            Task.FromResult<IReadOnlyList<CommandResponse>>([]);

        public Task<SyncResponse?> SyncAsync(long since, IReadOnlyCollection<Guid> full)
        {
            FullRequests.Add(full);
            _calls++;

            if (_calls > 1 && throwOnFull)
            {
                throw new HttpRequestException("Boom.");
            }

            return Task.FromResult(_calls == 1 ? first : second);
        }

        public (string Token, string Code)? JoinedWith { get; private set; }

        public Task<JoinOutcome> JoinAsync(string token, string code)
        {
            JoinedWith = (token, code);
            return Task.FromResult(join ?? new JoinOutcome.Invalid());
        }
    }
}
