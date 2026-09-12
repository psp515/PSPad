# Offline and Sync Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the client work with the network off — edits land in IndexedDB and
an ordered outbox, and reconcile with the server when it comes back.

**Architecture:** `IReplica` gains an IndexedDB implementation behind the same
interface plan 07 introduced, so no screen changes. `ReplicaUnitOfWork` stops
posting directly and appends to an outbox instead. A sync service flushes the
outbox in order, then pulls by marker and overwrites the replica. The server is
truth; the replica is disposable.

**Tech Stack:** .NET 10 Blazor WebAssembly, JS interop over IndexedDB, xUnit v3,
bUnit, Testcontainers for the round-trip proof.

**Spec:** `docs/superpowers/specs/2026-09-12-slice-1-design.md`

## Global Constraints

- Target framework `net10.0`, `Nullable` enable, `TreatWarningsAsErrors` true.
- No comments in code, except one line for a genuinely counter-intuitive constraint saying *why*.
- Every test class carries `[UnitTest]` or `[IntegrationTest]`.
- The outbox ships in order and stops at the first rejection. A command that failed never gets overtaken by one that depended on it.
- Rejections surface to the user. Never drop one silently (AD-5).
- The sync marker is the server's sequence. Never a clock, never a local counter.
- The replica is disposable: a pull overwrites it, and losing it costs nothing but a full pull.
- Code, comments, commits and docs in English.

---

### Task 1: IndexedDB replica

**Files:**
- Create: `src/PSPad.App/wwwroot/js/replica.js`
- Create: `src/PSPad.App/State/IndexedDbReplica.cs`
- Modify: `src/PSPad.App/wwwroot/index.html`
- Modify: `src/PSPad.App/Program.cs`
- Test: `test/PSPad.App.Tests/State/IndexedDbReplicaTests.cs`

**Interfaces:**
- Consumes: `IReplica` from plan 07.
- Produces: `IndexedDbReplica : IReplica` backed by the object stores `documents` (keyed by aggregate id) and `meta` (holding the marker), and the JS module `replica.js` exporting `get`, `getAll`, `put`, `putMany`, `getMeta` and `setMeta`.

Documents are stored as `{ id, type, userId, json }`. The type is the aggregate's
CLR name so a typed read can filter without deserialising every row.

- [ ] **Step 1: Write the JS module**

`src/PSPad.App/wwwroot/js/replica.js`:

```javascript
const DB_NAME = 'pspad';
const VERSION = 1;

function open() {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, VERSION);
    request.onupgradeneeded = () => {
      const db = request.result;
      if (!db.objectStoreNames.contains('documents')) {
        const documents = db.createObjectStore('documents', { keyPath: 'id' });
        documents.createIndex('type_user', ['type', 'userId']);
      }
      if (!db.objectStoreNames.contains('meta')) {
        db.createObjectStore('meta', { keyPath: 'key' });
      }
      if (!db.objectStoreNames.contains('outbox')) {
        db.createObjectStore('outbox', { keyPath: 'position', autoIncrement: true });
      }
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

function run(store, mode, work) {
  return open().then(db => new Promise((resolve, reject) => {
    const transaction = db.transaction(store, mode);
    const request = work(transaction.objectStore(store));
    transaction.oncomplete = () => resolve(request ? request.result : undefined);
    transaction.onerror = () => reject(transaction.error);
  }));
}

export function get(id) {
  return run('documents', 'readonly', documents => documents.get(id));
}

export function getAll(type, userId) {
  return run('documents', 'readonly', documents =>
    documents.index('type_user').getAll([type, userId]));
}

export function put(document) {
  return run('documents', 'readwrite', documents => documents.put(document));
}

export function putMany(items) {
  return run('documents', 'readwrite', documents => {
    items.forEach(item => documents.put(item));
    return null;
  });
}

export function getMeta(key) {
  return run('meta', 'readonly', meta => meta.get(key));
}

export function setMeta(key, value) {
  return run('meta', 'readwrite', meta => meta.put({ key, value }));
}
```

- [ ] **Step 2: Write the failing test**

```csharp
using System.Text.Json;
using PSPad.App.State;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class IndexedDbReplicaTests
{
    static readonly Guid User = Guid.NewGuid();

    [Fact]
    public void ADocumentSerialisesWithItsTypeAndOwner()
    {
        var area = NewArea("Home");

        var row = ReplicaRow.From(area);

        Assert.Equal(nameof(Area), row.Type);
        Assert.Equal(User, row.UserId);
        Assert.Equal(area.Id, row.Id);
    }

    [Fact]
    public void ARowRoundTripsBackIntoItsAggregate()
    {
        var area = NewArea("Home");

        var restored = ReplicaRow.From(area).To<Area>();

        Assert.Equal("Home", restored.Name);
        Assert.Equal(area.Version, restored.Version);
        Assert.Equal(area.Seq, restored.Seq);
    }

    [Fact]
    public void ARowFromTheServerRestoresTheSameWay()
    {
        var area = NewArea("Home");
        var fromServer = JsonSerializer.SerializeToElement(area);

        var restored = ReplicaRow.FromServer(nameof(Area), User, area.Id, fromServer).To<Area>();

        Assert.Equal("Home", restored.Name);
    }

    static Area NewArea(string name)
    {
        var area = new Area();
        area.ApplyAll(Area.Decide(
            null, new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), name, 0), DateTimeOffset.UnixEpoch));
        return area;
    }
}
```

The IndexedDB calls themselves are not unit-testable without a browser, so the
seam under test is the row shape — the part where a mistake silently loses data.
The browser half is verified by hand in Step 6 and end to end in Task 4.

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test test/PSPad.App.Tests --filter Category=Unit`
Expected: compile error — `ReplicaRow` does not exist.

- [ ] **Step 4: Write the row and the replica**

`src/PSPad.App/State/ReplicaRow.cs`:

```csharp
using System.Text.Json;
using PSPad.Abstractions;

namespace PSPad.App.State;

public sealed record ReplicaRow(Guid Id, string Type, Guid UserId, string Json)
{
    static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        IncludeFields = true
    };

    public static ReplicaRow From(Aggregate aggregate) =>
        new(aggregate.Id, aggregate.GetType().Name, aggregate.UserId,
            JsonSerializer.Serialize(aggregate, aggregate.GetType(), Options));

    public static ReplicaRow FromServer(string type, Guid userId, Guid id, JsonElement document) =>
        new(id, type, userId, document.GetRawText());

    public T To<T>() where T : Aggregate =>
        JsonSerializer.Deserialize<T>(Json, Options)
        ?? throw new InvalidOperationException($"Could not restore a {Type} from the replica.");
}
```

Aggregates expose `private set` properties, so `System.Text.Json` needs help to
write them back. Add `[JsonInclude]` to each property, or give every aggregate a
`[JsonConstructor]`. The round-trip test above is what proves whichever route you
take actually works — a silently empty `Name` here means the whole offline
replica is empty.

`src/PSPad.App/State/IndexedDbReplica.cs`:

```csharp
using Microsoft.JSInterop;
using PSPad.Abstractions;

namespace PSPad.App.State;

public sealed class IndexedDbReplica(IJSRuntime js) : IReplica, IAsyncDisposable
{
    IJSObjectReference? _module;

    public async Task<T?> LoadAsync<T>(Guid id) where T : Aggregate
    {
        var module = await ModuleAsync();
        var row = await module.InvokeAsync<ReplicaRow?>("get", id);
        return row?.To<T>();
    }

    public async Task<IReadOnlyList<T>> LoadAllAsync<T>(Guid userId) where T : Aggregate
    {
        var module = await ModuleAsync();
        var rows = await module.InvokeAsync<ReplicaRow[]>("getAll", typeof(T).Name, userId);
        return rows.Select(row => row.To<T>()).ToArray();
    }

    public async Task SaveAsync(Aggregate aggregate)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("put", ReplicaRow.From(aggregate));
    }

    public async Task SaveManyAsync(IReadOnlyList<ReplicaRow> rows)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("putMany", rows);
    }

    public async Task<long> MarkerAsync()
    {
        var module = await ModuleAsync();
        var stored = await module.InvokeAsync<MetaRow?>("getMeta", "marker");
        return stored?.Value ?? 0;
    }

    public async Task SetMarkerAsync(long marker)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("setMeta", "marker", marker);
    }

    async Task<IJSObjectReference> ModuleAsync() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/replica.js");

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }

    public sealed record MetaRow(string Key, long Value);
}
```

In `Program.cs` replace `InMemoryReplica` with `IndexedDbReplica`. Keep
`InMemoryReplica` in the project — the bUnit tests from plan 07 use it.

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test test/PSPad.App.Tests --filter Category=Unit`
Expected: PASS, three tests.

- [ ] **Step 6: Verify the browser half by hand**

Run the app, sign in, create an area, then reload the page with the API stopped.
Expected: the area is still on screen, and the browser's Application tab shows it
under IndexedDB → pspad → documents.

- [ ] **Step 7: Commit**

```bash
git add src test
git commit -m "feat: keep the replica in IndexedDB"
```

---

### Task 2: The ordered outbox

**Files:**
- Modify: `src/PSPad.App/wwwroot/js/replica.js`
- Create: `src/PSPad.App/State/OutboxEntry.cs`
- Create: `src/PSPad.App/State/IOutbox.cs`
- Create: `src/PSPad.App/State/IndexedDbOutbox.cs`
- Create: `src/PSPad.App/State/InMemoryOutbox.cs`
- Modify: `src/PSPad.App/State/ReplicaUnitOfWork.cs`
- Test: `test/PSPad.App.Tests/State/OutboxTests.cs`

**Interfaces:**
- Consumes: `CommandEnvelope` from `PSPad.Contracts`.
- Produces: `OutboxEntry(long Position, Guid CommandId, CommandEnvelope Envelope)`; `IOutbox` with `AppendAsync(Guid commandId, CommandEnvelope)`, `PeekAsync(int limit)`, `RemoveThroughAsync(long position)` and `CountAsync()`; `IndexedDbOutbox` over the `outbox` store; `InMemoryOutbox` for tests.

- [ ] **Step 1: Extend the JS module**

Append to `replica.js`:

```javascript
export function append(entry) {
  return run('outbox', 'readwrite', outbox => outbox.add(entry));
}

export function peek(limit) {
  return open().then(db => new Promise((resolve, reject) => {
    const transaction = db.transaction('outbox', 'readonly');
    const request = transaction.objectStore('outbox').getAll(null, limit);
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  }));
}

export function removeThrough(position) {
  return run('outbox', 'readwrite', outbox =>
    outbox.delete(IDBKeyRange.upperBound(position)));
}

export function count() {
  return run('outbox', 'readonly', outbox => outbox.count());
}
```

`autoIncrement` on the store gives the ordering for free, and `upperBound`
deletes exactly the prefix that was accepted — nothing later is touched.

- [ ] **Step 2: Write the failing test**

```csharp
using System.Text.Json;
using PSPad.App.State;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class OutboxTests
{
    [Fact]
    public async Task EntriesComeBackInTheOrderTheyWereAppended()
    {
        var outbox = new InMemoryOutbox();
        await outbox.AppendAsync(Guid.NewGuid(), Envelope("first"));
        await outbox.AppendAsync(Guid.NewGuid(), Envelope("second"));

        var peeked = await outbox.PeekAsync(10);

        Assert.Equal(["first", "second"], peeked.Select(NameIn));
    }

    [Fact]
    public async Task RemovingThroughAPositionLeavesEverythingAfterIt()
    {
        var outbox = new InMemoryOutbox();
        await outbox.AppendAsync(Guid.NewGuid(), Envelope("first"));
        await outbox.AppendAsync(Guid.NewGuid(), Envelope("second"));
        var peeked = await outbox.PeekAsync(10);

        await outbox.RemoveThroughAsync(peeked[0].Position);

        Assert.Equal(["second"], (await outbox.PeekAsync(10)).Select(NameIn));
    }

    [Fact]
    public async Task CommittingQueuesInsteadOfPosting()
    {
        var replica = new InMemoryReplica();
        var outbox = new InMemoryOutbox();
        var work = new ReplicaUnitOfWork(replica, outbox);
        var command = new CreateArea(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Home", 0);
        var area = new Area();
        var events = Area.Decide(null, command, DateTimeOffset.UnixEpoch);
        area.ApplyAll(events);

        work.Queue(command);
        work.Stage(area, events);
        await work.CommitAsync(command.CommandId, command.UserId, CancellationToken.None);

        Assert.Equal(1, await outbox.CountAsync());
        Assert.NotNull(await replica.LoadAsync<Area>(area.Id));
    }

    static CommandEnvelope Envelope(string name) =>
        new(nameof(CreateArea), JsonSerializer.SerializeToElement(new { Name = name }));

    static string NameIn(OutboxEntry entry) =>
        entry.Envelope.Payload.GetProperty("Name").GetString()!;
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test test/PSPad.App.Tests --filter Category=Unit`
Expected: compile error — `IOutbox` does not exist.

- [ ] **Step 4: Write the outbox**

```csharp
using PSPad.Contracts;

namespace PSPad.App.State;

public sealed record OutboxEntry(long Position, Guid CommandId, CommandEnvelope Envelope);
```

```csharp
using PSPad.Contracts;

namespace PSPad.App.State;

public interface IOutbox
{
    Task AppendAsync(Guid commandId, CommandEnvelope envelope);

    Task<IReadOnlyList<OutboxEntry>> PeekAsync(int limit);

    Task RemoveThroughAsync(long position);

    Task<int> CountAsync();
}
```

Write `InMemoryOutbox` over a `List<OutboxEntry>` with an incrementing position,
and `IndexedDbOutbox` delegating to the four JS functions from Step 1.

- [ ] **Step 5: Rework the unit of work**

`ReplicaUnitOfWork` takes `(IReplica replica, IOutbox outbox)` and, on commit,
saves each staged aggregate and appends each queued envelope. It no longer knows
about `PSPadApiClient` at all — pushing is the sync service's job from here on.

Update the registration in `Program.cs` accordingly.

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test test/PSPad.App.Tests --filter Category=Unit`
Expected: PASS, three tests.

- [ ] **Step 7: Commit**

```bash
git add src test
git commit -m "feat: queue commands in an ordered outbox"
```

---

### Task 3: The sync service

**Files:**
- Create: `src/PSPad.App/Sync/SyncService.cs`
- Create: `src/PSPad.App/Sync/SyncOutcome.cs`
- Create: `src/PSPad.App/Sync/IConnectivity.cs`
- Create: `src/PSPad.App/Sync/BrowserConnectivity.cs`
- Modify: `src/PSPad.App/wwwroot/index.html`
- Modify: `src/PSPad.App/Program.cs`
- Test: `test/PSPad.App.Tests/Sync/SyncServiceTests.cs`

**Interfaces:**
- Consumes: `IOutbox`, `IReplica`, `PSPadApiClient`.
- Produces: `SyncOutcome(int Pushed, int Pulled, IReadOnlyList<string> Rejections)`; `SyncService.SyncAsync(CancellationToken)`; `IConnectivity` with `bool IsOnline` and `event Action? CameOnline`.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Text.Json;
using PSPad.App.State;
using PSPad.App.Sync;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
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
            new(0, new Dictionary<string, JsonElement[]>(), []);

        public Task<IReadOnlyList<CommandResponse>> SendAsync(IReadOnlyList<CommandEnvelope> envelopes)
        {
            Sent.Add(envelopes);
            return Task.FromResult(Respond(envelopes));
        }

        public Task<SyncResponse?> SyncAsync(long since) => Task.FromResult<SyncResponse?>(Pull);
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
                },
                [])
        };

        var outcome = await new SyncService(api, replica, new InMemoryOutbox()).SyncAsync(CancellationToken.None);

        Assert.Equal(1, outcome.Pulled);
        Assert.Equal(17, await replica.MarkerAsync());
        Assert.Equal("Home", (await replica.LoadAsync<Area>(area.Id))!.Name);
    }

    static SyncService ServiceFor(ISyncApi api, IOutbox outbox) =>
        new(api, new InMemoryReplica(), outbox);

    static CommandEnvelope Envelope() =>
        new(nameof(CreateArea), JsonSerializer.SerializeToElement(
            new CreateArea(Guid.NewGuid(), User, Guid.NewGuid(), "Home", 0)));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/PSPad.App.Tests --filter Category=Unit`
Expected: compile error — `SyncService` and `ISyncApi` do not exist.

- [ ] **Step 3: Write the API seam**

`src/PSPad.App/Api/ISyncApi.cs`:

```csharp
using PSPad.Contracts;

namespace PSPad.App.Api;

public interface ISyncApi
{
    Task<IReadOnlyList<CommandResponse>> SendAsync(IReadOnlyList<CommandEnvelope> envelopes);

    Task<SyncResponse?> SyncAsync(long since);
}
```

`PSPadApiClient` implements it; register it in `Program.cs`.

- [ ] **Step 4: Write the sync service**

```csharp
using System.Text.Json;
using PSPad.Abstractions;
using PSPad.App.Api;
using PSPad.App.State;

namespace PSPad.App.Sync;

public sealed record SyncOutcome(int Pushed, int Pulled, IReadOnlyList<string> Rejections);

public sealed class SyncService(ISyncApi api, IReplica replica, IOutbox outbox)
{
    const int BatchSize = 50;

    static readonly Dictionary<string, Type> Collections = new()
    {
        ["areas"] = typeof(Module.Tasks.Areas.Area),
        ["tasklists"] = typeof(Module.Tasks.Lists.TaskList),
        ["todotasks"] = typeof(Module.Tasks.Tasks.TodoTask),
        ["goals"] = typeof(Module.Tasks.Goals.Goal),
        ["inboxes"] = typeof(Module.Tasks.Inbox.Inbox)
    };

    public async Task<SyncOutcome> SyncAsync(CancellationToken ct)
    {
        var (pushed, rejections) = await PushAsync();
        var pulled = await PullAsync(ct);

        return new SyncOutcome(pushed, pulled, rejections);
    }

    async Task<(int Pushed, IReadOnlyList<string> Rejections)> PushAsync()
    {
        var batch = await outbox.PeekAsync(BatchSize);
        if (batch.Count == 0)
        {
            return (0, []);
        }

        var responses = await api.SendAsync(batch.Select(entry => entry.Envelope).ToArray());
        var accepted = 0;

        while (accepted < responses.Count && responses[accepted].Accepted)
        {
            accepted++;
        }

        if (accepted > 0)
        {
            await outbox.RemoveThroughAsync(batch[accepted - 1].Position);
        }

        var rejections = accepted < responses.Count && responses[accepted].Rejection is { } reason
            ? new[] { reason }
            : Array.Empty<string>();

        return (accepted, rejections);
    }

    async Task<int> PullAsync(CancellationToken ct)
    {
        var since = await replica.MarkerAsync();
        var response = await api.SyncAsync(since);

        if (response is null)
        {
            return 0;
        }

        var pulled = 0;

        foreach (var (collection, documents) in response.Documents)
        {
            if (!Collections.TryGetValue(collection, out var type))
            {
                continue;
            }

            foreach (var document in documents)
            {
                var aggregate = (Aggregate)JsonSerializer.Deserialize(document.GetRawText(), type,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                await replica.SaveAsync(aggregate);
                pulled++;
            }
        }

        await replica.SetMarkerAsync(response.Marker);
        return pulled;
    }
}
```

The push stops at the first rejection and keeps everything after it, rather than
dropping the batch or skipping past. A later command usually depends on the one
that failed, and shipping it anyway produces state the server and the client
disagree about — the one outcome the whole design exists to prevent.

The `users` collection is intentionally absent from `Collections`: the client
reads its own user from `/api/me`, and `User` lives in a module `PSPad.App` does
not reference.

- [ ] **Step 5: Write connectivity**

```csharp
namespace PSPad.App.Sync;

public interface IConnectivity
{
    bool IsOnline { get; }

    event Action? CameOnline;
}
```

`BrowserConnectivity` wraps `navigator.onLine` and the `online` window event via
`IJSRuntime` and a `[JSInvokable]` callback, raising `CameOnline`. Add the small
script that forwards the event to `index.html`.

- [ ] **Step 6: Flush on reconnect and on a timer**

Register a hosted loop in `Program.cs` — a `SyncCoordinator` that calls
`SyncService.SyncAsync` on startup, whenever `CameOnline` fires, and every 60
seconds while `IsOnline`. Rejections go to MudBlazor's snackbar, one message each,
and the outbox count shows in the app bar whenever it is above zero.

A rejection the user never sees is the same as a lost edit, so the snackbar is
not optional polish.

- [ ] **Step 7: Run test to verify it passes**

Run: `dotnet test test/PSPad.App.Tests --filter Category=Unit`
Expected: PASS, four tests.

- [ ] **Step 8: Commit**

```bash
git add src test
git commit -m "feat: flush the outbox and pull deltas by marker"
```

---

### Task 4: Round-trip proof

**Files:**
- Test: `test/PSPad.Api.Tests/Sync/OfflineRoundTripTests.cs`
- Modify: `README.md`

**Interfaces:**
- Consumes: `ApiFactory` from plan 04/05, `InMemoryReplica`, `InMemoryOutbox`, `SyncService` from Task 3.
- Produces: nothing new — this task adds the test that proves the pieces meet.

The client's own assemblies are referenced by the API test project for this one
test, which is the only place the two halves are exercised together.

- [ ] **Step 1: Reference the client from the API tests**

```bash
dotnet add test/PSPad.Api.Tests reference src/PSPad.App
```

- [ ] **Step 2: Write the failing test**

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using PSPad.App.Api;
using PSPad.App.State;
using PSPad.App.Sync;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Sync;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class OfflineRoundTripTests(MongoFixture fixture)
{
    [Fact]
    public async Task EditsMadeOfflineReachTheServerInOrderWhenSyncRuns()
    {
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me");
        var user = me!.UserId;

        var replica = new InMemoryReplica();
        var outbox = new InMemoryOutbox();
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        await outbox.AppendAsync(Guid.NewGuid(), Envelope(
            new CreateArea(Guid.NewGuid(), user, areaId, "Home", 0)));
        await outbox.AppendAsync(Guid.NewGuid(), Envelope(
            new CreateTaskList(Guid.NewGuid(), user, listId, areaId, "Errands", 0)));
        await outbox.AppendAsync(Guid.NewGuid(), Envelope(
            new CreateTask(Guid.NewGuid(), user, taskId, listId, "Buy milk")));

        var sync = new SyncService(new HttpSyncApi(client), replica, outbox);

        var outcome = await sync.SyncAsync(CancellationToken.None);

        Assert.Equal(3, outcome.Pushed);
        Assert.Empty(outcome.Rejections);
        Assert.Equal(0, await outbox.CountAsync());
        Assert.Equal("Buy milk", (await replica.LoadAsync<TodoTask>(taskId))!.Name);
        Assert.True(await replica.MarkerAsync() > 0);
    }

    [Fact]
    public async Task ASecondSyncAfterNoChangesPushesAndPullsNothing()
    {
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me");
        var replica = new InMemoryReplica();
        var outbox = new InMemoryOutbox();
        await outbox.AppendAsync(Guid.NewGuid(), Envelope(
            new CreateArea(Guid.NewGuid(), me!.UserId, Guid.NewGuid(), "Home", 0)));
        var sync = new SyncService(new HttpSyncApi(client), replica, outbox);
        await sync.SyncAsync(CancellationToken.None);

        var second = await sync.SyncAsync(CancellationToken.None);

        Assert.Equal(0, second.Pushed);
        Assert.Equal(0, second.Pulled);
    }

    [Fact]
    public async Task ARejectedCommandHoldsTheOnesBehindIt()
    {
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me");
        var user = me!.UserId;
        var outbox = new InMemoryOutbox();

        await outbox.AppendAsync(Guid.NewGuid(), Envelope(
            new RenameArea(Guid.NewGuid(), user, Guid.NewGuid(), "Nowhere")));
        await outbox.AppendAsync(Guid.NewGuid(), Envelope(
            new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), "Home", 0)));

        var outcome = await new SyncService(new HttpSyncApi(client), new InMemoryReplica(), outbox)
            .SyncAsync(CancellationToken.None);

        Assert.Equal(0, outcome.Pushed);
        Assert.Single(outcome.Rejections);
        Assert.Equal(2, await outbox.CountAsync());
    }

    sealed class HttpSyncApi(HttpClient http) : ISyncApi
    {
        public async Task<IReadOnlyList<CommandResponse>> SendAsync(IReadOnlyList<CommandEnvelope> envelopes)
        {
            var response = await http.PostAsJsonAsync("/api/commands", envelopes);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CommandResponse[]>() ?? [];
        }

        public Task<SyncResponse?> SyncAsync(long since) =>
            http.GetFromJsonAsync<SyncResponse>($"/api/sync?since={since}");
    }

    static CommandEnvelope Envelope<T>(T command) where T : notnull =>
        new(typeof(T).Name, JsonSerializer.SerializeToElement(command));
}
```

- [ ] **Step 3: Run test to verify it fails or passes honestly**

Run: `dotnet test test/PSPad.Api.Tests --filter Category=Integration`
Expected: PASS if Tasks 1-3 are right. If `SyncService` deserialisation of pulled
documents fails, fix the `[JsonInclude]` question from Task 1 Step 4 — do not
loosen the assertion.

- [ ] **Step 4: Verify offline by hand**

Run the full stack, sign in, open the browser's network tab and set it to
offline. Capture three inbox items, organise one into a task, tick a step. Expect
every change to appear immediately and the app bar to show three pending. Go back
online. Expect the badge to clear within a minute and a reload to show the same
state.

Then, still online, stop the API container and repeat: the app must stay usable
and queue rather than error.

- [ ] **Step 5: Document it**

Add a short "Offline" section to `README.md`: what the replica holds, where the
outbox lives, what happens to a rejected command, and the fact that clearing site
data costs nothing but a full pull.

- [ ] **Step 6: Run the whole suite**

Run: `dotnet test`
Expected: PASS, unit and integration.

- [ ] **Step 7: Commit**

```bash
git add src test README.md
git commit -m "feat: prove the offline round trip end to end"
```

---

## Done when

`dotnet test` passes; a browser with the network off accepts captures, edits and
step ticks, shows a pending count, and reconciles when the network returns; a
rejected command stops the queue behind it and surfaces a message; and a second
sync with nothing to do pushes nothing and pulls nothing.
