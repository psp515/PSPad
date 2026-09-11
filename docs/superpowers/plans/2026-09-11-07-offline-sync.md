# Offline Replica & Sync Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The app keeps working with no network — every screen reads a durable local replica, every action is applied locally and queued, and the queue drains in order when the connection returns.

**Architecture:** IndexedDB holds three stores: `replica` (the folded aggregate state), `outbox` (commands not yet acknowledged), and `meta` (the last server version marker). A command is applied to the replica through the domain, appended to the outbox, and persisted before the UI returns. A background sync loop drains the outbox to `/api/commands`, then pulls `/api/sync?since={version}` and overwrites the affected replica rows with server state. The server is always the winner (AD-5, AD-6).

**Tech Stack:** .NET 10, Blazor WebAssembly, IndexedDB via JS interop, xUnit, Shouldly

**Spec:** `docs/superpowers/specs/2026-09-11-gtd-core-design.md` §9

## Global Constraints

- .NET 10 (LTS). All projects target `net10.0`.
- `PSPad.Domain` and `PSPad.Contracts` must compile for WebAssembly.
- The client never invents a rule the server does not have — offline decisions use the same `Decide` functions.
- Commands drain **in issue order**; a rejected command does not block the ones behind it, but is surfaced, never dropped.
- The version marker is the server's event sequence, never a client clock.
- Code, comments, commits and docs in English.
- GPL v3 — every dependency must be license-compatible.

**Depends on:** plans 04 (`/api/commands`, `/api/sync`), 06 (`LocalStore`, `CommandDispatcher`).

---

### Task 1: Persistence abstraction and IndexedDB implementation

**Files:**
- Create: `src/PSPad.Client/Storage/IClientStorage.cs`
- Create: `src/PSPad.Client/Storage/IndexedDbStorage.cs`
- Create: `src/PSPad.Client/Storage/InMemoryStorage.cs`
- Create: `src/PSPad.Client/wwwroot/js/storage.js`
- Modify: `src/PSPad.Client/wwwroot/index.html`
- Modify: `src/PSPad.Client/Program.cs`
- Test: `test/PSPad.Client.Tests/Storage/InMemoryStorageTests.cs`

**Interfaces:**
- Consumes: plan 06 Task 1
- Produces: `IClientStorage` with `Task<T?> GetAsync<T>(string store, string key)`, `Task PutAsync<T>(string store, string key, T value)`, `Task DeleteAsync(string store, string key)`, `Task<IReadOnlyList<T>> AllAsync<T>(string store)`, `Task ClearAsync(string store)`; the store names `"replica"`, `"outbox"`, `"meta"`

Every test in this plan runs against `InMemoryStorage`, because a headless test
host has no IndexedDB. `IndexedDbStorage` is a thin interop shim with no logic of
its own, which is what makes that substitution honest rather than a dodge.

- [ ] **Step 1: Write the failing test**

`test/PSPad.Client.Tests/Storage/InMemoryStorageTests.cs`:

```csharp
using PSPad.Client.Storage;
using Shouldly;

namespace PSPad.Client.Tests.Storage;

public class InMemoryStorageTests
{
    record Thing(string Name, int Count);

    [Fact]
    public async Task Round_trips_a_value()
    {
        IClientStorage storage = new InMemoryStorage();

        await storage.PutAsync("replica", "one", new Thing("filament", 3));

        (await storage.GetAsync<Thing>("replica", "one"))!.Name.ShouldBe("filament");
    }

    [Fact]
    public async Task Returns_null_for_a_missing_key()
    {
        IClientStorage storage = new InMemoryStorage();

        (await storage.GetAsync<Thing>("replica", "missing")).ShouldBeNull();
    }

    [Fact]
    public async Task Lists_everything_in_one_store_without_leaking_another()
    {
        IClientStorage storage = new InMemoryStorage();

        await storage.PutAsync("replica", "a", new Thing("a", 1));
        await storage.PutAsync("outbox", "b", new Thing("b", 2));

        (await storage.AllAsync<Thing>("replica")).ShouldHaveSingleItem().Name.ShouldBe("a");
    }

    [Fact]
    public async Task Deleting_and_clearing_behave()
    {
        IClientStorage storage = new InMemoryStorage();

        await storage.PutAsync("outbox", "a", new Thing("a", 1));
        await storage.PutAsync("outbox", "b", new Thing("b", 2));

        await storage.DeleteAsync("outbox", "a");
        (await storage.AllAsync<Thing>("outbox")).Count.ShouldBe(1);

        await storage.ClearAsync("outbox");
        (await storage.AllAsync<Thing>("outbox")).ShouldBeEmpty();
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test test/PSPad.Client.Tests --filter InMemoryStorageTests`
Expected: FAIL — `IClientStorage` does not exist.

- [ ] **Step 3: Write the abstraction and the in-memory implementation**

`src/PSPad.Client/Storage/IClientStorage.cs`:

```csharp
namespace PSPad.Client.Storage;

public interface IClientStorage
{
    Task<T?> GetAsync<T>(string store, string key);
    Task PutAsync<T>(string store, string key, T value);
    Task DeleteAsync(string store, string key);
    Task<IReadOnlyList<T>> AllAsync<T>(string store);
    Task ClearAsync(string store);
}

public static class Stores
{
    public const string Replica = "replica";
    public const string Outbox = "outbox";
    public const string Meta = "meta";
}
```

`src/PSPad.Client/Storage/InMemoryStorage.cs`:

```csharp
using System.Text.Json;

namespace PSPad.Client.Storage;

/// <summary>
/// Test double and fallback for browsers with IndexedDB disabled. Serializes
/// through JSON so it behaves like the real store, including losing reference
/// identity.
/// </summary>
public sealed class InMemoryStorage : IClientStorage
{
    readonly Dictionary<string, Dictionary<string, string>> _stores = [];

    public Task<T?> GetAsync<T>(string store, string key) =>
        Task.FromResult(Bucket(store).TryGetValue(key, out var json)
            ? JsonSerializer.Deserialize<T>(json)
            : default);

    public Task PutAsync<T>(string store, string key, T value)
    {
        Bucket(store)[key] = JsonSerializer.Serialize(value);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string store, string key)
    {
        Bucket(store).Remove(key);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<T>> AllAsync<T>(string store) =>
        Task.FromResult<IReadOnlyList<T>>(
            [.. Bucket(store).Values.Select(json => JsonSerializer.Deserialize<T>(json)!)]);

    public Task ClearAsync(string store)
    {
        Bucket(store).Clear();
        return Task.CompletedTask;
    }

    Dictionary<string, string> Bucket(string store)
    {
        if (!_stores.TryGetValue(store, out var bucket))
            _stores[store] = bucket = [];

        return bucket;
    }
}
```

- [ ] **Step 4: Write the IndexedDB shim**

`src/PSPad.Client/wwwroot/js/storage.js`:

```javascript
const DB_NAME = 'pspad';
const DB_VERSION = 1;
const STORES = ['replica', 'outbox', 'meta'];

let dbPromise = null;

function open() {
    if (dbPromise) return dbPromise;

    dbPromise = new Promise((resolve, reject) => {
        const request = indexedDB.open(DB_NAME, DB_VERSION);

        request.onupgradeneeded = () => {
            for (const name of STORES) {
                if (!request.result.objectStoreNames.contains(name)) {
                    request.result.createObjectStore(name);
                }
            }
        };

        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });

    return dbPromise;
}

async function run(store, mode, action) {
    const db = await open();

    return new Promise((resolve, reject) => {
        const transaction = db.transaction(store, mode);
        const request = action(transaction.objectStore(store));

        request.onsuccess = () => resolve(request.result ?? null);
        request.onerror = () => reject(request.error);
    });
}

export async function get(store, key) {
    return run(store, 'readonly', s => s.get(key));
}

export async function put(store, key, value) {
    await run(store, 'readwrite', s => s.put(value, key));
}

export async function remove(store, key) {
    await run(store, 'readwrite', s => s.delete(key));
}

export async function all(store) {
    return run(store, 'readonly', s => s.getAll());
}

export async function clear(store) {
    await run(store, 'readwrite', s => s.clear());
}
```

`src/PSPad.Client/Storage/IndexedDbStorage.cs`:

```csharp
using System.Text.Json;
using Microsoft.JSInterop;

namespace PSPad.Client.Storage;

/// <summary>
/// Interop shim over js/storage.js. Holds no logic — everything interesting is
/// tested through InMemoryStorage.
/// </summary>
public sealed class IndexedDbStorage(IJSRuntime js) : IClientStorage, IAsyncDisposable
{
    IJSObjectReference? _module;

    async Task<IJSObjectReference> ModuleAsync() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/storage.js");

    public async Task<T?> GetAsync<T>(string store, string key)
    {
        var module = await ModuleAsync();
        var json = await module.InvokeAsync<string?>("get", store, key);

        return json is null ? default : JsonSerializer.Deserialize<T>(json);
    }

    public async Task PutAsync<T>(string store, string key, T value)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("put", store, key, JsonSerializer.Serialize(value));
    }

    public async Task DeleteAsync(string store, string key)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("remove", store, key);
    }

    public async Task<IReadOnlyList<T>> AllAsync<T>(string store)
    {
        var module = await ModuleAsync();
        var items = await module.InvokeAsync<string[]>("all", store);

        return [.. items.Select(json => JsonSerializer.Deserialize<T>(json)!)];
    }

    public async Task ClearAsync(string store)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("clear", store);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
            await _module.DisposeAsync();
    }
}
```

Register in `Program.cs`:

```csharp
builder.Services.AddScoped<IClientStorage, IndexedDbStorage>();
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test test/PSPad.Client.Tests --filter InMemoryStorageTests`
Expected: PASS, 4 tests.

- [ ] **Step 6: Commit**

```bash
git add src/PSPad.Client/Storage/ src/PSPad.Client/wwwroot/js/ src/PSPad.Client/Program.cs test/PSPad.Client.Tests/Storage/
git commit -m "feat(client): add client storage abstraction over indexeddb"
```

---

### Task 2: The outbox

**Files:**
- Create: `src/PSPad.Client/Sync/OutboxEntry.cs`
- Create: `src/PSPad.Client/Sync/Outbox.cs`
- Test: `test/PSPad.Client.Tests/Sync/OutboxTests.cs`

**Interfaces:**
- Consumes: Task 1
- Produces: `OutboxEntry(Guid CommandId, string Type, string Payload, DateTimeOffset IssuedAt, int Sequence)`; `Outbox.EnqueueAsync(ICommand)`, `Outbox.PendingAsync()` returning entries in issue order, `Outbox.AcknowledgeAsync(Guid commandId)`, `Outbox.CountAsync()`

Ordering matters: `CreateList` must reach the server before the `CreateTask` that
targets it. A monotonic sequence stamped at enqueue time preserves that even
though IndexedDB returns records unordered.

- [ ] **Step 1: Write the failing test**

`test/PSPad.Client.Tests/Sync/OutboxTests.cs`:

```csharp
using PSPad.Client.Storage;
using PSPad.Client.Sync;
using PSPad.Domain.Areas;
using PSPad.Domain.Common;
using Shouldly;

namespace PSPad.Client.Tests.Sync;

public class OutboxTests
{
    static readonly UserId User = UserId.New();
    static DateTimeOffset Now => DateTimeOffset.UtcNow;

    static CreateArea Area(string name) =>
        new(User, Guid.NewGuid(), Now, AreaId.New(), name, 0);

    [Fact]
    public async Task Pending_entries_come_back_in_the_order_they_were_queued()
    {
        var outbox = new Outbox(new InMemoryStorage());

        await outbox.EnqueueAsync(Area("first"));
        await outbox.EnqueueAsync(Area("second"));
        await outbox.EnqueueAsync(Area("third"));

        var pending = await outbox.PendingAsync();

        pending.Select(entry => entry.Sequence).ShouldBe([0, 1, 2]);
    }

    [Fact]
    public async Task Acknowledging_removes_only_that_command()
    {
        var outbox = new Outbox(new InMemoryStorage());
        var kept = Area("kept");
        var acknowledged = Area("acknowledged");

        await outbox.EnqueueAsync(kept);
        await outbox.EnqueueAsync(acknowledged);
        await outbox.AcknowledgeAsync(acknowledged.CommandId);

        var pending = await outbox.PendingAsync();

        pending.ShouldHaveSingleItem().CommandId.ShouldBe(kept.CommandId);
    }

    [Fact]
    public async Task Entries_survive_a_new_outbox_over_the_same_storage()
    {
        var storage = new InMemoryStorage();
        var command = Area("survivor");

        await new Outbox(storage).EnqueueAsync(command);

        var reopened = await new Outbox(storage).PendingAsync();

        reopened.ShouldHaveSingleItem().CommandId.ShouldBe(command.CommandId);
    }

    [Fact]
    public async Task An_entry_can_be_turned_back_into_its_command()
    {
        var outbox = new Outbox(new InMemoryStorage());
        var command = Area("Work");

        await outbox.EnqueueAsync(command);
        var entry = (await outbox.PendingAsync()).ShouldHaveSingleItem();

        var restored = Outbox.ToCommand(entry).ShouldBeOfType<CreateArea>();
        restored.Name.ShouldBe("Work");
        restored.CommandId.ShouldBe(command.CommandId);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test test/PSPad.Client.Tests --filter OutboxTests`
Expected: FAIL — `Outbox` does not exist.

- [ ] **Step 3: Write the outbox**

`src/PSPad.Client/Sync/OutboxEntry.cs`:

```csharp
namespace PSPad.Client.Sync;

/// <summary>
/// One queued command. `Sequence` is assigned at enqueue time and is what keeps
/// drain order stable — IndexedDB itself gives no ordering guarantee.
/// </summary>
public sealed record OutboxEntry(
    Guid CommandId,
    string Type,
    string Payload,
    DateTimeOffset IssuedAt,
    int Sequence);
```

`src/PSPad.Client/Sync/Outbox.cs`:

```csharp
using System.Text.Json;
using PSPad.Client.Storage;
using PSPad.Domain;
using PSPad.Domain.Common;

namespace PSPad.Client.Sync;

public sealed class Outbox(IClientStorage storage)
{
    const string SequenceKey = "outbox-sequence";

    static readonly IReadOnlyDictionary<string, Type> CommandTypes =
        typeof(DomainMarker).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(typeof(ICommand).IsAssignableFrom)
            .ToDictionary(type => type.Name, type => type);

    public async Task EnqueueAsync(ICommand command)
    {
        var sequence = await NextSequenceAsync();

        var entry = new OutboxEntry(
            command.CommandId,
            command.GetType().Name,
            JsonSerializer.Serialize(command, command.GetType()),
            command.IssuedAt,
            sequence);

        await storage.PutAsync(Stores.Outbox, command.CommandId.ToString(), entry);
    }

    public async Task<IReadOnlyList<OutboxEntry>> PendingAsync()
    {
        var entries = await storage.AllAsync<OutboxEntry>(Stores.Outbox);

        return [.. entries.OrderBy(entry => entry.Sequence)];
    }

    public Task AcknowledgeAsync(Guid commandId) =>
        storage.DeleteAsync(Stores.Outbox, commandId.ToString());

    public async Task<int> CountAsync() => (await PendingAsync()).Count;

    public static ICommand ToCommand(OutboxEntry entry)
    {
        if (!CommandTypes.TryGetValue(entry.Type, out var type))
            throw new InvalidOperationException($"Unknown queued command type {entry.Type}.");

        return (ICommand)JsonSerializer.Deserialize(entry.Payload, type)!;
    }

    async Task<int> NextSequenceAsync()
    {
        var current = await storage.GetAsync<int?>(Stores.Meta, SequenceKey) ?? 0;
        await storage.PutAsync(Stores.Meta, SequenceKey, current + 1);

        return current;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test test/PSPad.Client.Tests --filter OutboxTests`
Expected: PASS, 4 tests.

- [ ] **Step 5: Commit**

```bash
git add src/PSPad.Client/Sync/ test/PSPad.Client.Tests/Sync/OutboxTests.cs
git commit -m "feat(client): queue commands in a durable ordered outbox"
```

---

### Task 3: Offline-aware dispatcher

**Files:**
- Create: `src/PSPad.Client/Sync/OfflineCommandDispatcher.cs`
- Create: `src/PSPad.Client/Sync/IConnectivity.cs`
- Modify: `src/PSPad.Client/Program.cs`
- Test: `test/PSPad.Client.Tests/Sync/OfflineDispatcherTests.cs`

**Interfaces:**
- Consumes: Tasks 1-2, plan 06 Task 2
- Produces: `OfflineCommandDispatcher : CommandDispatcher` overriding `SendAsync` to apply locally, enqueue, and drain when online; `DrainAsync()` draining the outbox in order; `IConnectivity` with `bool IsOnline` and `event Action? Changed`

- [ ] **Step 1: Write the failing test**

`test/PSPad.Client.Tests/Sync/OfflineDispatcherTests.cs`:

```csharp
using PSPad.Client.State;
using PSPad.Client.Storage;
using PSPad.Client.Sync;
using PSPad.Domain.Areas;
using PSPad.Domain.Common;
using PSPad.Domain.Lists;
using Shouldly;

namespace PSPad.Client.Tests.Sync;

public class OfflineDispatcherTests
{
    static readonly UserId User = UserId.New();
    static DateTimeOffset Now => DateTimeOffset.UtcNow;

    sealed class FakeConnectivity : IConnectivity
    {
        public bool IsOnline { get; set; } = true;
        public event Action? Changed;
        public void Toggle(bool online)
        {
            IsOnline = online;
            Changed?.Invoke();
        }
    }

    sealed class RecordingApi : IApiClient
    {
        public List<ICommand> Sent { get; } = [];
        public bool Fail { get; set; }

        public Task<IReadOnlyList<CommandOutcomeDto>> SendAsync(
            IReadOnlyList<ICommand> commands, CancellationToken cancellationToken)
        {
            if (Fail)
                throw new HttpRequestException("offline");

            Sent.AddRange(commands);

            return Task.FromResult<IReadOnlyList<CommandOutcomeDto>>(
                [.. commands.Select(c => new CommandOutcomeDto(c.CommandId, true, null, null))]);
        }
    }

    static (OfflineCommandDispatcher Dispatcher, LocalStore Store, Outbox Outbox,
            RecordingApi Api, FakeConnectivity Connectivity) Build()
    {
        var storage = new InMemoryStorage();
        var store = new LocalStore();
        var outbox = new Outbox(storage);
        var api = new RecordingApi();
        var connectivity = new FakeConnectivity();

        return (new OfflineCommandDispatcher(store, api, outbox, connectivity),
                store, outbox, api, connectivity);
    }

    [Fact]
    public async Task Offline_changes_are_visible_immediately_and_queued()
    {
        var (dispatcher, store, outbox, api, connectivity) = Build();
        connectivity.Toggle(false);

        await dispatcher.SendAsync(new CreateArea(
            User, Guid.NewGuid(), Now, AreaId.New(), "Work", 0));

        store.Areas.ShouldHaveSingleItem().Name.ShouldBe("Work");
        (await outbox.CountAsync()).ShouldBe(1);
        api.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Coming_back_online_drains_the_queue_in_order()
    {
        var (dispatcher, _, outbox, api, connectivity) = Build();
        connectivity.Toggle(false);

        var area = AreaId.New();
        var list = ListId.New();

        await dispatcher.SendAsync(new CreateArea(User, Guid.NewGuid(), Now, area, "Work", 0));
        await dispatcher.SendAsync(new CreateList(User, Guid.NewGuid(), Now, list, area, "Errands", 0));

        connectivity.Toggle(true);
        await dispatcher.DrainAsync();

        api.Sent.Select(command => command.GetType().Name)
            .ShouldBe([nameof(CreateArea), nameof(CreateList)]);
        (await outbox.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task A_failed_send_leaves_the_command_queued_for_the_next_attempt()
    {
        var (dispatcher, _, outbox, api, _) = Build();
        api.Fail = true;

        await dispatcher.SendAsync(new CreateArea(
            User, Guid.NewGuid(), Now, AreaId.New(), "Work", 0));

        (await outbox.CountAsync()).ShouldBe(1);

        api.Fail = false;
        await dispatcher.DrainAsync();

        (await outbox.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task A_rejected_command_leaves_the_queue_and_is_surfaced()
    {
        var storage = new InMemoryStorage();
        var store = new LocalStore();
        var outbox = new Outbox(storage);
        var dispatcher = new OfflineCommandDispatcher(
            store, new RejectingApi(), outbox, new FakeConnectivity());

        await dispatcher.SendAsync(new CreateArea(
            User, Guid.NewGuid(), Now, AreaId.New(), "Work", 0));

        (await outbox.CountAsync()).ShouldBe(0);
        dispatcher.Rejected.ShouldHaveSingleItem().ErrorCode.ShouldBe(ErrorCodes.NotFound);
    }

    sealed class RejectingApi : IApiClient
    {
        public Task<IReadOnlyList<CommandOutcomeDto>> SendAsync(
            IReadOnlyList<ICommand> commands, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CommandOutcomeDto>>(
            [
                .. commands.Select(c => new CommandOutcomeDto(
                    c.CommandId, false, ErrorCodes.NotFound, "Gone."))
            ]);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test test/PSPad.Client.Tests --filter OfflineDispatcherTests`
Expected: FAIL — `OfflineCommandDispatcher` does not exist.

- [ ] **Step 3: Write connectivity**

`src/PSPad.Client/Sync/IConnectivity.cs`:

```csharp
using Microsoft.JSInterop;

namespace PSPad.Client.Sync;

public interface IConnectivity
{
    bool IsOnline { get; }
    event Action? Changed;
}

/// <summary>
/// Reads navigator.onLine and listens for the browser's online/offline events.
/// Treated as a hint only: a send that fails is authoritative, not this flag.
/// </summary>
public sealed class BrowserConnectivity : IConnectivity, IDisposable
{
    readonly IJSRuntime _js;
    DotNetObjectReference<BrowserConnectivity>? _self;

    public BrowserConnectivity(IJSRuntime js)
    {
        _js = js;
        IsOnline = true;
    }

    public bool IsOnline { get; private set; }

    public event Action? Changed;

    public async Task InitializeAsync()
    {
        _self = DotNetObjectReference.Create(this);
        IsOnline = await _js.InvokeAsync<bool>("pspadConnectivity.initialize", _self);
    }

    [JSInvokable]
    public void OnConnectivityChanged(bool online)
    {
        IsOnline = online;
        Changed?.Invoke();
    }

    public void Dispose() => _self?.Dispose();
}
```

Add to `src/PSPad.Client/wwwroot/index.html`, before the Blazor script:

```html
<script>
    window.pspadConnectivity = {
        initialize: function (dotNetRef) {
            window.addEventListener('online', () => dotNetRef.invokeMethodAsync('OnConnectivityChanged', true));
            window.addEventListener('offline', () => dotNetRef.invokeMethodAsync('OnConnectivityChanged', false));
            return navigator.onLine;
        }
    };
</script>
```

- [ ] **Step 4: Write the dispatcher**

`src/PSPad.Client/Sync/OfflineCommandDispatcher.cs`:

```csharp
using PSPad.Client.State;
using PSPad.Domain.Common;

namespace PSPad.Client.Sync;

/// <summary>
/// Spec §9.2. Apply locally, persist to the outbox, then try to ship. A send
/// that throws leaves the command queued; a send that returns a rejection
/// removes it from the queue and surfaces it (spec §9.4).
/// </summary>
public sealed class OfflineCommandDispatcher(
    LocalStore store,
    IApiClient api,
    Outbox outbox,
    IConnectivity connectivity)
    : CommandDispatcher(store, api)
{
    public event Action? PendingChanged;

    public override async Task SendAsync(
        ICommand command, CancellationToken cancellationToken = default)
    {
        foreach (var @event in DecideLocally(command))
            store.ApplyLocally(@event);

        await outbox.EnqueueAsync(command);
        PendingChanged?.Invoke();

        if (connectivity.IsOnline)
            await DrainAsync(cancellationToken);
    }

    /// <summary>
    /// Ships queued commands oldest first, stopping at the first transport
    /// failure so ordering is never broken by a partial drain.
    /// </summary>
    public async Task DrainAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in await outbox.PendingAsync())
        {
            ICommand command;

            try
            {
                command = Outbox.ToCommand(entry);
            }
            catch (InvalidOperationException)
            {
                // A command type that no longer exists cannot be replayed; drop it
                // rather than wedge the queue behind it forever.
                await outbox.AcknowledgeAsync(entry.CommandId);
                continue;
            }

            IReadOnlyList<CommandOutcomeDto> outcomes;

            try
            {
                outcomes = await api.SendAsync([command], cancellationToken);
            }
            catch (HttpRequestException)
            {
                return;
            }

            foreach (var outcome in outcomes)
            {
                await outbox.AcknowledgeAsync(outcome.CommandId);

                if (!outcome.Accepted)
                    Reject(outcome);
            }

            PendingChanged?.Invoke();
        }
    }

    public Task<int> PendingCountAsync() => outbox.CountAsync();
}
```

`CommandDispatcher` (plan 06 Task 2) needs three small changes to support this:

- make `SendAsync` `virtual` (it already is)
- expose the private `Decide` as `protected IReadOnlyList<IDomainEvent> DecideLocally(ICommand)`
- expose rejection recording as `protected void Reject(CommandOutcomeDto outcome)`, moving the body of the existing rejection block into it
- change the `store` constructor parameter to `protected LocalStore store`

- [ ] **Step 5: Register the services**

In `src/PSPad.Client/Program.cs`, replace the plain dispatcher registration:

```csharp
builder.Services.AddScoped<Outbox>();
builder.Services.AddScoped<BrowserConnectivity>();
builder.Services.AddScoped<IConnectivity>(sp => sp.GetRequiredService<BrowserConnectivity>());
builder.Services.AddScoped<OfflineCommandDispatcher>();
builder.Services.AddScoped<CommandDispatcher>(
    sp => sp.GetRequiredService<OfflineCommandDispatcher>());
```

Components keep injecting `CommandDispatcher` and get the offline one.

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test test/PSPad.Client.Tests --filter OfflineDispatcherTests`
Expected: PASS, 4 tests.

- [ ] **Step 7: Commit**

```bash
git add src/PSPad.Client/ test/PSPad.Client.Tests/Sync/OfflineDispatcherTests.cs
git commit -m "feat(client): apply commands offline and drain the outbox in order"
```

---

### Task 4: Replica persistence and delta sync

**Files:**
- Create: `src/PSPad.Client/Sync/SyncService.cs`
- Create: `src/PSPad.Client/Sync/ReplicaSnapshot.cs`
- Modify: `src/PSPad.Client/State/LocalStore.cs`
- Modify: `src/PSPad.Client/State/ApiClient.cs`
- Test: `test/PSPad.Client.Tests/Sync/SyncServiceTests.cs`

**Interfaces:**
- Consumes: Tasks 1-3
- Produces: `LocalStore.Snapshot()` / `LocalStore.Restore(ReplicaSnapshot)`; `SyncService.RestoreAsync()` loading the replica at startup, `SyncService.PullAsync()` fetching `/api/sync?since={version}` and overwriting rows, `SyncService.PersistAsync()` saving the snapshot; `IApiClient.PullAsync(long since, CancellationToken)`

Server rows overwrite local ones unconditionally (AD-5). The outbox is *not*
replayed into the replica after a pull: any command still queued has already been
folded in locally, and the server's version of everything else is the truth.

- [ ] **Step 1: Write the failing test**

`test/PSPad.Client.Tests/Sync/SyncServiceTests.cs`:

```csharp
using PSPad.Client.State;
using PSPad.Client.Storage;
using PSPad.Client.Sync;
using PSPad.Domain.Areas;
using PSPad.Domain.Common;
using PSPad.Domain.Goals;
using PSPad.Domain.Lists;
using PSPad.Domain.Tasks;
using Shouldly;

namespace PSPad.Client.Tests.Sync;

public class SyncServiceTests
{
    static readonly UserId User = UserId.New();
    static DateTimeOffset Now => DateTimeOffset.UtcNow;

    sealed class StubApi(SyncPayload payload) : IApiClient
    {
        public long RequestedSince { get; private set; } = -1;

        public Task<IReadOnlyList<CommandOutcomeDto>> SendAsync(
            IReadOnlyList<ICommand> commands, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CommandOutcomeDto>>([]);

        public Task<SyncPayload> PullAsync(long since, CancellationToken cancellationToken)
        {
            RequestedSince = since;
            return Task.FromResult(payload);
        }
    }

    static SyncPayload PayloadWith(long version, params object[] rows) => new(
        [.. rows.OfType<AreaState>()],
        [.. rows.OfType<TaskListState>()],
        [.. rows.OfType<TodoTaskState>()],
        [.. rows.OfType<GoalState>()],
        [],
        version);

    [Fact]
    public async Task A_pull_writes_server_rows_into_the_store()
    {
        var storage = new InMemoryStorage();
        var store = new LocalStore();
        var area = new AreaState(AreaId.New(), User, "Work", 0, false);
        var api = new StubApi(PayloadWith(42, area));
        var sync = new SyncService(store, api, storage);

        await sync.PullAsync();

        store.Areas.ShouldHaveSingleItem().Name.ShouldBe("Work");
    }

    [Fact]
    public async Task The_next_pull_asks_only_for_changes_after_the_stored_version()
    {
        var storage = new InMemoryStorage();
        var api = new StubApi(PayloadWith(42));
        var sync = new SyncService(new LocalStore(), api, storage);

        await sync.PullAsync();
        api.RequestedSince.ShouldBe(0);

        await sync.PullAsync();
        api.RequestedSince.ShouldBe(42);
    }

    [Fact]
    public async Task Server_rows_overwrite_local_ones()
    {
        var storage = new InMemoryStorage();
        var store = new LocalStore();
        var areaId = AreaId.New();

        store.ApplyLocally(new AreaCreated(User, Now, Guid.NewGuid(), areaId, "Local name", 0));

        var api = new StubApi(PayloadWith(1, new AreaState(areaId, User, "Server name", 0, false)));
        await new SyncService(store, api, storage).PullAsync();

        store.Areas.ShouldHaveSingleItem().Name.ShouldBe("Server name");
    }

    [Fact]
    public async Task The_replica_survives_a_restart()
    {
        var storage = new InMemoryStorage();
        var store = new LocalStore();
        var api = new StubApi(PayloadWith(7, new AreaState(AreaId.New(), User, "Work", 0, false)));

        var sync = new SyncService(store, api, storage);
        await sync.PullAsync();
        await sync.PersistAsync();

        var reopened = new LocalStore();
        await new SyncService(reopened, api, storage).RestoreAsync();

        reopened.Areas.ShouldHaveSingleItem().Name.ShouldBe("Work");
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test test/PSPad.Client.Tests --filter SyncServiceTests`
Expected: FAIL — `SyncService` and `SyncPayload` do not exist.

- [ ] **Step 3: Add the snapshot and store methods**

`src/PSPad.Client/Sync/ReplicaSnapshot.cs`:

```csharp
using PSPad.Domain.Areas;
using PSPad.Domain.Goals;
using PSPad.Domain.Inbox;
using PSPad.Domain.Lists;
using PSPad.Domain.Tasks;

namespace PSPad.Client.Sync;

public sealed record ReplicaSnapshot(
    IReadOnlyList<AreaState> Areas,
    IReadOnlyList<TaskListState> Lists,
    IReadOnlyList<TodoTaskState> Tasks,
    IReadOnlyList<GoalState> Goals,
    IReadOnlyList<InboxItem> InboxItems);

/// <summary>Rows changed since the client's marker, plus the new marker (spec §9.3).</summary>
public sealed record SyncPayload(
    IReadOnlyList<AreaState> Areas,
    IReadOnlyList<TaskListState> Lists,
    IReadOnlyList<TodoTaskState> Tasks,
    IReadOnlyList<GoalState> Goals,
    IReadOnlyList<InboxItem> InboxItems,
    long Version);
```

Add to `src/PSPad.Client/State/LocalStore.cs`:

```csharp
    public ReplicaSnapshot Snapshot() => new(Areas, Lists, Tasks, Goals, InboxItems);

    public void Restore(ReplicaSnapshot snapshot)
    {
        _areas.Clear();
        _lists.Clear();
        _tasks.Clear();
        _goals.Clear();

        foreach (var area in snapshot.Areas) _areas[area.Id] = area;
        foreach (var list in snapshot.Lists) _lists[list.Id] = list;
        foreach (var task in snapshot.Tasks) _tasks[task.Id] = task;
        foreach (var goal in snapshot.Goals) _goals[goal.Id] = goal;

        _inbox = _inbox with { Items = snapshot.InboxItems };

        Changed?.Invoke();
    }

    /// <summary>Overwrites the named rows with server state, leaving others alone (AD-6).</summary>
    public void Merge(SyncPayload payload)
    {
        foreach (var area in payload.Areas) _areas[area.Id] = area;
        foreach (var list in payload.Lists) _lists[list.Id] = list;
        foreach (var task in payload.Tasks) _tasks[task.Id] = task;
        foreach (var goal in payload.Goals) _goals[goal.Id] = goal;

        if (payload.InboxItems.Count > 0)
            _inbox = _inbox with { Items = payload.InboxItems };

        Changed?.Invoke();
    }
```

Add `using PSPad.Client.Sync;` to the file.

- [ ] **Step 4: Extend the API client**

Add to `IApiClient` in `src/PSPad.Client/State/ApiClient.cs`:

```csharp
    Task<SyncPayload> PullAsync(long since, CancellationToken cancellationToken);
```

and implement it on `ApiClient`:

```csharp
    public async Task<SyncPayload> PullAsync(long since, CancellationToken cancellationToken)
    {
        var response = await http.GetFromJsonAsync<SyncResponse>(
            $"api/sync?since={since}", cancellationToken);

        return new SyncPayload(
            Deserialize<AreaState>(response!.Areas),
            Deserialize<TaskListState>(response.Lists),
            Deserialize<TodoTaskState>(response.Tasks),
            Deserialize<GoalState>(response.Goals),
            InboxItemsFrom(response.Inbox),
            response.Version);
    }

    static IReadOnlyList<T> Deserialize<T>(IReadOnlyList<JsonElement> rows) =>
        [.. rows.Select(row => row.Deserialize<T>()!)];

    /// <summary>The server ships one InboxView per user; the client wants its items.</summary>
    static IReadOnlyList<InboxItem> InboxItemsFrom(IReadOnlyList<JsonElement> rows) =>
    [
        .. rows.SelectMany(row => row.GetProperty("Items").EnumerateArray())
               .Select(item => new InboxItem(
                   item.GetProperty("Id").GetGuid(),
                   item.GetProperty("Text").GetString()!,
                   item.GetProperty("CapturedAt").GetDateTimeOffset()))
    ];
```

The server's `TaskView` is not the same shape as the domain's `TodoTaskState`.
Add a server-side mapping so `/api/sync` emits domain state directly: in
`QueryEndpoints.Changed<TaskView>`, project each row into a `TodoTaskState`
before serializing. If that mapping grows awkward, the alternative is a
client-side `TaskView -> TodoTaskState` adapter; pick one and keep it in a single
file.

Adding a method to `IApiClient` breaks the test doubles written in plan 06 Tasks
2 and 4 (`RecordingApi`, `RejectingApi`, `SilentApi`). Add a `PullAsync` to each
that returns an empty payload; the compiler will point at every one.

- [ ] **Step 5: Write the sync service**

`src/PSPad.Client/Sync/SyncService.cs`:

```csharp
using PSPad.Client.State;
using PSPad.Client.Storage;

namespace PSPad.Client.Sync;

public sealed class SyncService(LocalStore store, IApiClient api, IClientStorage storage)
{
    const string SnapshotKey = "snapshot";
    const string VersionKey = "version";

    public async Task RestoreAsync()
    {
        var snapshot = await storage.GetAsync<ReplicaSnapshot>(Stores.Replica, SnapshotKey);

        if (snapshot is not null)
            store.Restore(snapshot);
    }

    public async Task PullAsync(CancellationToken cancellationToken = default)
    {
        var since = await storage.GetAsync<long?>(Stores.Meta, VersionKey) ?? 0;
        var payload = await api.PullAsync(since, cancellationToken);

        store.Merge(payload);

        await storage.PutAsync(Stores.Meta, VersionKey, payload.Version);
        await PersistAsync();
    }

    public Task PersistAsync() =>
        storage.PutAsync(Stores.Replica, SnapshotKey, store.Snapshot());
}
```

Register in `Program.cs`: `builder.Services.AddScoped<SyncService>();`

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test test/PSPad.Client.Tests --filter SyncServiceTests`
Expected: PASS, 4 tests.

- [ ] **Step 7: Commit**

```bash
git add src/PSPad.Client/ test/PSPad.Client.Tests/Sync/SyncServiceTests.cs
git commit -m "feat(client): persist the replica and pull server deltas by version"
```

---

### Task 5: Wiring sync into the app shell

**Files:**
- Create: `src/PSPad.Client/Components/SyncStatus.razor`
- Create: `src/PSPad.Client/Components/RejectedBanner.razor`
- Modify: `src/PSPad.Client/Layout/MainLayout.razor`
- Modify: `src/PSPad.Client/App.razor`
- Test: `test/PSPad.Client.Tests/Components/SyncStatusTests.cs`

**Interfaces:**
- Consumes: Tasks 1-4
- Produces: a startup sequence — restore replica, then pull, then drain — and an app-bar indicator showing offline state and pending count, plus a dismissible banner listing rejected commands

The indicator matters more than it looks: without it, a user cannot tell whether
what they typed on the train actually reached the server.

- [ ] **Step 1: Write the failing test**

`test/PSPad.Client.Tests/Components/SyncStatusTests.cs`:

```csharp
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PSPad.Client.Components;
using PSPad.Client.State;
using PSPad.Client.Storage;
using PSPad.Client.Sync;
using PSPad.Domain.Areas;
using PSPad.Domain.Common;
using Shouldly;

namespace PSPad.Client.Tests.Components;

public class SyncStatusTests : TestContext
{
    sealed class FakeConnectivity(bool online) : IConnectivity
    {
        public bool IsOnline { get; } = online;
        public event Action? Changed;
    }

    sealed class DeadApi : IApiClient
    {
        public Task<IReadOnlyList<CommandOutcomeDto>> SendAsync(
            IReadOnlyList<ICommand> commands, CancellationToken cancellationToken) =>
            throw new HttpRequestException("offline");

        public Task<SyncPayload> PullAsync(long since, CancellationToken cancellationToken) =>
            throw new HttpRequestException("offline");
    }

    OfflineCommandDispatcher Arrange(bool online)
    {
        var storage = new InMemoryStorage();
        var store = new LocalStore();
        var outbox = new Outbox(storage);
        var dispatcher = new OfflineCommandDispatcher(
            store, new DeadApi(), outbox, new FakeConnectivity(online));

        Services.AddMudServices();
        Services.AddSingleton<IConnectivity>(new FakeConnectivity(online));
        Services.AddSingleton(dispatcher);
        JSInterop.Mode = JSRuntimeMode.Loose;

        return dispatcher;
    }

    [Fact]
    public void Offline_state_is_announced()
    {
        Arrange(online: false);

        RenderComponent<SyncStatus>().Markup.ShouldContain("Offline");
    }

    [Fact]
    public async Task Pending_commands_are_counted()
    {
        var dispatcher = Arrange(online: false);

        await dispatcher.SendAsync(new CreateArea(
            UserId.New(), Guid.NewGuid(), DateTimeOffset.UtcNow, AreaId.New(), "Work", 0));

        var component = RenderComponent<SyncStatus>();
        component.WaitForAssertion(() => component.Markup.ShouldContain("1"));
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test test/PSPad.Client.Tests --filter SyncStatusTests`
Expected: FAIL — `SyncStatus` does not exist.

- [ ] **Step 3: Write the status indicator**

`src/PSPad.Client/Components/SyncStatus.razor`:

```razor
@using PSPad.Client.Sync
@implements IDisposable
@inject IConnectivity Connectivity
@inject OfflineCommandDispatcher Dispatcher

@if (!Connectivity.IsOnline)
{
    <MudChip T="string" Color="Color.Warning" Size="Size.Small"
             Icon="@Icons.Material.Filled.CloudOff">
        Offline@(_pending > 0 ? $" · {_pending}" : string.Empty)
    </MudChip>
}
else if (_pending > 0)
{
    <MudChip T="string" Color="Color.Info" Size="Size.Small"
             Icon="@Icons.Material.Filled.Sync">
        Syncing · @_pending
    </MudChip>
}

@code {
    int _pending;

    protected override async Task OnInitializedAsync()
    {
        Dispatcher.PendingChanged += OnPendingChanged;
        Connectivity.Changed += OnConnectivityChanged;

        _pending = await Dispatcher.PendingCountAsync();
    }

    void OnPendingChanged() => InvokeAsync(async () =>
    {
        _pending = await Dispatcher.PendingCountAsync();
        StateHasChanged();
    });

    void OnConnectivityChanged() => InvokeAsync(async () =>
    {
        if (Connectivity.IsOnline)
            await Dispatcher.DrainAsync();

        StateHasChanged();
    });

    public void Dispose()
    {
        Dispatcher.PendingChanged -= OnPendingChanged;
        Connectivity.Changed -= OnConnectivityChanged;
    }
}
```

- [ ] **Step 4: Write the rejection banner**

`src/PSPad.Client/Components/RejectedBanner.razor`:

```razor
@using PSPad.Client.State
@implements IDisposable
@inject CommandDispatcher Dispatcher

@foreach (var rejected in Dispatcher.Rejected)
{
    <MudAlert Severity="Severity.Warning" ShowCloseIcon="true"
              CloseIconClicked="@(() => Dispatcher.Dismiss(rejected.CommandId))" Class="mb-2">
        @rejected.Message
    </MudAlert>
}

@code {
    protected override void OnInitialized() => Dispatcher.RejectionsChanged += Refresh;

    void Refresh() => InvokeAsync(StateHasChanged);

    public void Dispose() => Dispatcher.RejectionsChanged -= Refresh;
}
```

- [ ] **Step 5: Wire both into the layout and start sync at boot**

In `src/PSPad.Client/Layout/MainLayout.razor`, inside `<MudAppBar>` after the
title:

```razor
        <MudSpacer />
        <SyncStatus />
```

and at the top of `<MudContainer>`:

```razor
            <RejectedBanner />
```

`src/PSPad.Client/App.razor` gains a startup sequence:

```razor
@using PSPad.Client.Sync
@inject SyncService Sync
@inject OfflineCommandDispatcher Dispatcher
@inject BrowserConnectivity Connectivity

<Router AppAssembly="@typeof(App).Assembly">
    <Found Context="routeData">
        <RouteView RouteData="@routeData" DefaultLayout="@typeof(Layout.MainLayout)" />
        <FocusOnNavigate RouteData="@routeData" Selector="h1" />
    </Found>
    <NotFound>
        <LayoutView Layout="@typeof(Layout.MainLayout)">
            <MudAlert Severity="Severity.Info">Nothing here.</MudAlert>
        </LayoutView>
    </NotFound>
</Router>

@code {
    protected override async Task OnInitializedAsync()
    {
        // Show the last known state first, then reconcile. A cold start on a
        // train should render instantly from IndexedDB, not wait on a timeout.
        await Sync.RestoreAsync();
        await Connectivity.InitializeAsync();

        if (!Connectivity.IsOnline)
            return;

        try
        {
            await Dispatcher.DrainAsync();
            await Sync.PullAsync();
        }
        catch (HttpRequestException)
        {
            // Stale-but-usable beats an error screen.
        }
    }
}
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test test/PSPad.Client.Tests --filter SyncStatusTests`
Expected: PASS, 2 tests.

- [ ] **Step 7: Commit**

```bash
git add src/PSPad.Client/ test/PSPad.Client.Tests/Components/
git commit -m "feat(client): restore, drain and pull at startup with a sync indicator"
```

---

### Task 6: End-to-end offline round trip

**Files:**
- Test: `test/PSPad.Server.Tests/Sync/OfflineRoundTripTests.cs`

**Interfaces:**
- Consumes: every prior task and plans 04-05
- Produces: no production code — this task proves the contract between the client's queue and the server

The one test that would have caught every integration mistake in this plan:
commands issued offline, drained in order, landing as correct server state, and
coming back as a delta.

- [ ] **Step 1: Write the failing test**

`test/PSPad.Server.Tests/Sync/OfflineRoundTripTests.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using PSPad.Contracts;
using PSPad.Domain.Areas;
using PSPad.Domain.Common;
using PSPad.Domain.Inbox;
using PSPad.Domain.Lists;
using PSPad.Domain.Tasks;
using Shouldly;

namespace PSPad.Server.Tests.Sync;

[Collection("database")]
public class OfflineRoundTripTests(PostgresFixture fixture)
{
    static CommandEnvelope Envelope<T>(T command) where T : ICommand =>
        new(typeof(T).Name, JsonSerializer.SerializeToElement(command));

    [Fact]
    public async Task A_batch_queued_offline_applies_in_order_and_comes_back_as_a_delta()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", fixture.ConnectionString);
            builder.UseSetting("Jwt:SigningKey", new string('k', 64));
        });

        var client = factory.CreateClient();
        var token = await SignIn(client, $"offline-{Guid.NewGuid():N}");
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var user = await ResolveUserId(client);
        var now = DateTimeOffset.UtcNow;
        var area = AreaId.New();
        var list = ListId.New();
        var task = TaskId.New();
        var itemId = Guid.NewGuid();

        // Exactly what a drained outbox looks like: dependent commands, in order.
        var response = await client.PostAsJsonAsync("/api/commands", new CommandBatch(
        [
            Envelope(new CreateArea(user, Guid.NewGuid(), now, area, "Projects", 0)),
            Envelope(new CreateList(user, Guid.NewGuid(), now, list, area, "3D printing", 0)),
            Envelope(new CaptureToInbox(user, Guid.NewGuid(), now, itemId, "Buy filament")),
            Envelope(new OrganizeInboxItem(
                user, Guid.NewGuid(), now, itemId, list, task, null, Priority.Medium))
        ]));

        response.EnsureSuccessStatusCode();
        var batch = await response.Content.ReadFromJsonAsync<CommandBatchResponse>();
        batch!.Results.ShouldAllBe(result => result.Accepted);

        var delta = await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0");

        delta!.Tasks.ShouldContain(row => row.GetProperty("Name").GetString() == "Buy filament");
        delta.Inbox.ShouldAllBe(row => row.GetProperty("Items").GetArrayLength() == 0);

        // A second pull from the new marker returns nothing.
        var empty = await client.GetFromJsonAsync<SyncResponse>($"/api/sync?since={delta.Version}");
        empty!.Tasks.ShouldBeEmpty();
    }

    [Fact]
    public async Task Replaying_a_drained_batch_after_a_lost_response_is_harmless()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", fixture.ConnectionString);
            builder.UseSetting("Jwt:SigningKey", new string('k', 64));
        });

        var client = factory.CreateClient();
        var token = await SignIn(client, $"replay-{Guid.NewGuid():N}");
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        var user = await ResolveUserId(client);
        var command = new CreateArea(
            user, Guid.NewGuid(), DateTimeOffset.UtcNow, AreaId.New(), "Twice", 0);

        var batch = new CommandBatch([Envelope(command)]);

        await client.PostAsJsonAsync("/api/commands", batch);
        await client.PostAsJsonAsync("/api/commands", batch);

        var delta = await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0");

        delta!.Areas.Count(row => row.GetProperty("Name").GetString() == "Twice").ShouldBe(1);
    }

    static async Task<string> SignIn(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/token", new { username, password = "pw" });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<TokenBody>())!.AccessToken;
    }

    /// <summary>
    /// The first authenticated call provisions the user; /api/sync then reveals
    /// the id the server assigned, which the client stamps onto its commands.
    /// </summary>
    static async Task<UserId> ResolveUserId(HttpClient client)
    {
        var delta = await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0");
        var seeded = delta!.Areas.First();

        return new UserId(seeded.GetProperty("UserId").GetGuid());
    }

    record TokenBody(string AccessToken);
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test test/PSPad.Server.Tests --filter OfflineRoundTripTests`
Expected: FAIL initially — most likely on the `UserId` shape in the serialized
projection, or on the inbox row shape. These are exactly the integration seams
this test exists to pin down.

- [ ] **Step 3: Fix what the test exposes**

Work through the failures in the production code, not the test. The likely
suspects, in order:

1. `UserId` serializing as an object rather than a bare GUID. Add a
   `JsonConverter` for the id structs in `PSPad.Domain/Common/IdJsonConverters.cs`
   and register it on both the Marten serializer (plan 04 Task 1) and the client,
   so an id is always a plain string GUID on the wire.
2. `/api/sync` emitting `TaskView` rather than domain `TodoTaskState` — apply the
   mapping decided in Task 4 Step 4.
3. A `DateOnly` round-trip difference between the server and the client
   serializer settings.

Each fix belongs in one commit with the failure it resolves named in the message.

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test test/PSPad.Server.Tests --filter OfflineRoundTripTests`
Expected: PASS, 2 tests.

- [ ] **Step 5: Run everything**

Run: `dotnet test`
Expected: PASS across all four test projects.

- [ ] **Step 6: Verify by hand in the browser**

With the stack running and the app open at `http://localhost:5000`:

1. Sign in, capture an item in the Inbox, confirm it appears.
2. Open the browser devtools and switch the network to Offline.
3. Capture two more items and complete a task. Confirm the UI updates and the
   app bar shows *Offline · 3*.
4. Reload the page while still offline. Confirm everything is still there.
5. Switch back to Online. Confirm the chip clears and a hard refresh shows the
   same state.

- [ ] **Step 7: Commit**

```bash
git add test/PSPad.Server.Tests/Sync/
git commit -m "test: prove the offline queue round trips through the server"
```

---

## Done when

- `dotnet test` is green across all four test projects.
- The app loads and is fully usable with the network switched off, including after a reload.
- Commands queued offline reach the server in issue order once the connection returns.
- A resent command creates nothing twice.
- A rejected command is shown to the user and leaves the queue.
