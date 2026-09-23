# Sync-on-Command Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Trigger a sync immediately after every locally-accepted command, instead of waiting up to 60s for the periodic poll, while guarding against overlapping syncs running in parallel.

**Architecture:** `SyncCoordinator.SyncNowAsync()` already does push-then-pull and is public precisely so it can be invoked ad hoc (ADR-0029). It has no re-entrancy guard today, and `SyncService.PushAsync` peeks the outbox without removing entries until the response comes back — two overlapping `SyncNowAsync()` calls could both peek the same outbox batch and push it twice. Fix: make `SyncNowAsync()` coalesce overlapping calls into at most one queued follow-up pass, sharing one `Task` so every caller's own state is guaranteed synced by the time their awaited task completes. Then hook `CommandSender.SendAsync` to call it after a successful command, via a new minimal interface (`ISyncTrigger`) so `CommandSender`'s unit tests don't need to construct a full `SyncCoordinator`.

**Tech Stack:** .NET 10, Blazor WebAssembly, xUnit, bUnit (`Bunit.TestContext`), MudBlazor snackbar.

**Spec:** No spec doc — this is a bounded change approved in chat against the existing sync design (`specs/slice-design.md` §7 "Sync", `adr/0029-*.md`, `adr/0030-*.md`, `adr/0031-*.md`). This plan is the only artifact; read those ADRs for background on `SyncCoordinator`'s existing contract if anything below is unclear.

## Global Constraints

- Every test class needs a `[UnitTest]` category trait (AGENTS.md §7). Both new test classes touched here are already `[UnitTest]`.
- No comments except a one-line "why" on genuinely counter-intuitive constraints (AGENTS.md §11). Do not add "what" comments.
- `PSPad.Module.Tasks` purity (AD-4) is not touched by this plan — all changes are in `PSPad.App` and its tests.
- TDD: failing test before implementation, every step (AGENTS.md §11).

---

## Task 1: Coalescing guard on `SyncCoordinator.SyncNowAsync`

**Files:**
- Modify: `src/PSPad.App/Sync/SyncCoordinator.cs`
- Test: `test/PSPad.App.Tests/Sync/SyncCoordinatorTests.cs`

**Interfaces:**
- Consumes: `SyncService.SyncAsync(CancellationToken)` returning `SyncOutcome` (unchanged), `IConnectivity.IsOnline`, `IOutbox.CountAsync()`.
- Produces: `public Task SyncNowAsync()` — same public signature and return semantics as today (a `Task` the caller can await that completes once this call's own effects have landed), but now safe to call concurrently from multiple call sites without doubling `sync.SyncAsync()` invocations in flight at once.

- [ ] **Step 1: Write the failing test for coalescing**

Add to `test/PSPad.App.Tests/Sync/SyncCoordinatorTests.cs`, inside the `SyncCoordinatorTests` class:

```csharp
    [Fact]
    public async Task OverlappingCallsShareOneRunAndQueueExactlyOneFollowUpPass()
    {
        // CommandSender will call this after every command; two commands queued in quick
        // succession must not run SyncService.SyncAsync twice in parallel -- PushAsync peeks
        // the outbox without removing entries until the response lands, so a second concurrent
        // push would peek and resend the same batch.
        Services.AddMudServices();
        var outbox = new InMemoryOutbox();
        var api = new GatedApi(AnAreaCalled("Dom"));
        var coordinator = new SyncCoordinator(
            new SyncService(api, new InMemoryReplica(), outbox),
            new FixedConnectivity(true),
            outbox,
            Services.GetRequiredService<ISnackbar>());

        var first = coordinator.SyncNowAsync();
        var second = coordinator.SyncNowAsync();

        Assert.Same(first, second);
        Assert.Equal(1, api.SyncCalls);

        api.Release();
        await first;

        Assert.Equal(2, api.SyncCalls);
    }

    sealed class GatedApi(SyncResponse pull) : ISyncApi
    {
        readonly TaskCompletionSource _gate = new();

        public int SyncCalls { get; private set; }

        public Task<IReadOnlyList<CommandResponse>> SendAsync(IReadOnlyList<CommandEnvelope> envelopes) =>
            Task.FromResult<IReadOnlyList<CommandResponse>>([]);

        public async Task<SyncResponse?> SyncAsync(long since)
        {
            SyncCalls++;
            await _gate.Task;
            return pull;
        }

        public void Release() => _gate.SetResult();
    }
```

**Why this shape:** the first `SyncNowAsync()` call starts a run and blocks inside `ISyncApi.SyncAsync` on the gate. The second call, made while the first is still in flight, must return the *same* `Task` (`Assert.Same`) rather than starting a second `sync.SyncAsync()` — so `api.SyncCalls` is still `1` at that point. Releasing the gate lets the first pass finish; because a second call arrived while it was running, exactly one more pass must run automatically (`api.SyncCalls` becomes `2`) so the second caller's own command is guaranteed synced without that caller having to await a separate task.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/PSPad.App.Tests --filter FullyQualifiedName~OverlappingCallsShareOneRunAndQueueExactlyOneFollowUpPass`
Expected: FAIL — either `Assert.Same` fails (two independent tasks returned) or `api.SyncCalls` is `2` before `Release()` is called (no guard at all today).

- [ ] **Step 3: Implement the coalescing guard**

Replace the body of `src/PSPad.App/Sync/SyncCoordinator.cs` (keep `Start()` and `LoopAsync()` unchanged — both already call `SyncNowAsync()` and get the new behavior for free) — rename the current `SyncNowAsync` body to a private `RunOnceAsync`, and add the coalescing wrapper:

```csharp
using MudBlazor;
using PSPad.App.State.Outbox;

namespace PSPad.App.Sync;

public sealed class SyncCoordinator(SyncService sync, IConnectivity connectivity, IOutbox outbox, ISnackbar snackbar)
    : ISyncTrigger
{
    static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

    bool _started;
    Task? _inFlight;
    bool _rerunRequested;

    public int PendingCount { get; private set; }

    public int Revision { get; private set; }

    public Task Started { get; private set; } = Task.CompletedTask;

    public event Action? Changed;

    public void Start()
    {
        if (!_started)
        {
            _started = true;
            connectivity.CameOnline += () => _ = SyncNowAsync();
            _ = LoopAsync();
        }

        // The shell starts the coordinator once for the signed-out redirect it renders and again
        // once signed in. Handing the second caller the first pull would hand it a task that
        // already finished with no session behind it.
        Started = SyncNowAsync();
    }

    async Task LoopAsync()
    {
        while (true)
        {
            await Task.Delay(PollInterval);
            if (connectivity.IsOnline)
            {
                await SyncNowAsync();
            }
        }
    }

    // A command handler and the poll loop can both call this within the same tick. PushAsync
    // peeks the outbox without removing entries until its response lands, so two overlapping
    // runs would peek and resend the same batch -- callers that arrive mid-run share the
    // in-flight task and get one guaranteed follow-up pass instead of a second parallel run.
    public Task SyncNowAsync()
    {
        if (_inFlight is { IsCompleted: false })
        {
            _rerunRequested = true;
            return _inFlight;
        }

        _inFlight = RunAsync();
        return _inFlight;
    }

    async Task RunAsync()
    {
        do
        {
            _rerunRequested = false;
            await RunOnceAsync();
        } while (_rerunRequested);
    }

    async Task RunOnceAsync()
    {
        if (connectivity.IsOnline)
        {
            try
            {
                var outcome = await sync.SyncAsync(CancellationToken.None);

                // Screens read the replica once and keep what they got. Nothing else would tell
                // one rendered from an empty replica -- every screen, right after a sign-in --
                // that its data has since arrived.
                if (outcome.Pulled > 0)
                {
                    Revision++;
                }

                foreach (var rejection in outcome.Rejections)
                {
                    // A rejection the user never sees is the same as a lost edit.
                    snackbar.Add(rejection, Severity.Warning);
                }
            }
            catch (HttpRequestException)
            {
            }
        }

        PendingCount = await outbox.CountAsync();
        Changed?.Invoke();
    }
}
```

Also create `src/PSPad.App/Sync/ISyncTrigger.cs`:

```csharp
namespace PSPad.App.Sync;

public interface ISyncTrigger
{
    Task SyncNowAsync();
}
```

- [ ] **Step 4: Run the new test and the full existing `SyncCoordinatorTests` suite**

Run: `dotnet test test/PSPad.App.Tests --filter FullyQualifiedName~SyncCoordinatorTests`
Expected: PASS — all 8 existing tests plus the new one. The existing tests call `SyncNowAsync()` sequentially (each `await` completes before the next call), so `_inFlight` is always already completed when the next call starts and none of them observe coalescing.

- [ ] **Step 5: Commit**

```bash
git add src/PSPad.App/Sync/SyncCoordinator.cs src/PSPad.App/Sync/ISyncTrigger.cs test/PSPad.App.Tests/Sync/SyncCoordinatorTests.cs
git commit -m "feat: coalesce overlapping SyncCoordinator.SyncNowAsync calls"
```

---

## Task 2: Trigger sync from `CommandSender` after every accepted command

**Files:**
- Modify: `src/PSPad.App/State/Dispatch/CommandSender.cs`
- Modify: `src/PSPad.App/Program.cs:89` (register `ISyncTrigger`)
- Test: `test/PSPad.App.Tests/State/Dispatch/CommandSenderTests.cs`

**Interfaces:**
- Consumes: `ISyncTrigger.SyncNowAsync()` from Task 1 (`src/PSPad.App/Sync/ISyncTrigger.cs`).
- Produces: `CommandSender(IServiceProvider services, ReplicaUnitOfWork work, ISyncTrigger sync)` — constructor signature changes; the `Sent` event and `SendAsync<TCommand>` signature are unchanged.

- [ ] **Step 1: Write the failing tests**

Replace the contents of `test/PSPad.App.Tests/State/Dispatch/CommandSenderTests.cs`:

```csharp
using PSPad.Abstractions;
using PSPad.App.State.Dispatch;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.App.Sync;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State.Dispatch;

[UnitTest]
public class CommandSenderTests
{
    [Fact]
    public async Task SentFiresAndSyncIsTriggeredWhenAHandlerRuns()
    {
        var handled = CommandResult.Ok();
        var sync = new FakeSyncTrigger();
        var sender = new CommandSender(
            new FakeServiceProvider(new FakeCommandHandler(handled)), Work(), sync);
        var fires = 0;
        sender.Sent += () => fires++;

        var result = await sender.SendAsync(
            new FakeCommand(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.Equal(1, fires);
        Assert.Same(handled, result);
        Assert.Equal(1, sync.Calls);
    }

    [Fact]
    public async Task SentStaysSilentAndSyncIsNotTriggeredWhenTheHandlerRejects()
    {
        var rejected = CommandResult.Rejected("nope");
        var sync = new FakeSyncTrigger();
        var sender = new CommandSender(
            new FakeServiceProvider(new FakeCommandHandler(rejected)), Work(), sync);
        var fires = 0;
        sender.Sent += () => fires++;

        var result = await sender.SendAsync(
            new FakeCommand(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.Equal(0, fires);
        Assert.False(result.Accepted);
        Assert.Equal(0, sync.Calls);
    }

    [Fact]
    public async Task SentStaysSilentAndSyncIsNotTriggeredWhenNoHandlerIsRegistered()
    {
        var sync = new FakeSyncTrigger();
        var sender = new CommandSender(new FakeServiceProvider(handler: null), Work(), sync);
        var fires = 0;
        sender.Sent += () => fires++;

        var result = await sender.SendAsync(
            new FakeCommand(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.Equal(0, fires);
        Assert.False(result.Accepted);
        Assert.Equal(0, sync.Calls);
    }

    static ReplicaUnitOfWork Work() => new(new InMemoryReplica(), new InMemoryOutbox());

    sealed record FakeCommand(Guid CommandId, Guid UserId) : ICommand;

    sealed class FakeCommandHandler(CommandResult result) : ICommandHandler<FakeCommand>
    {
        public Task<CommandResult> HandleAsync(FakeCommand command, CancellationToken ct) =>
            Task.FromResult(result);
    }

    sealed class FakeServiceProvider(object? handler) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(ICommandHandler<FakeCommand>) ? handler : null;
    }

    sealed class FakeSyncTrigger : ISyncTrigger
    {
        public int Calls { get; private set; }

        public Task SyncNowAsync()
        {
            Calls++;
            return Task.CompletedTask;
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test test/PSPad.App.Tests --filter FullyQualifiedName~CommandSenderTests`
Expected: FAIL to compile — `CommandSender` has no three-argument constructor yet.

- [ ] **Step 3: Wire the trigger into `CommandSender`**

Replace `src/PSPad.App/State/Dispatch/CommandSender.cs`:

```csharp
using PSPad.Abstractions;
using PSPad.App.Sync;

namespace PSPad.App.State.Dispatch;

public sealed class CommandSender(IServiceProvider services, ReplicaUnitOfWork work, ISyncTrigger sync)
{
    public event Action? Sent;

    public async Task<CommandResult> SendAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : ICommand
    {
        work.Queue(command);
        var handler = services.GetService(typeof(ICommandHandler<TCommand>)) as ICommandHandler<TCommand>;

        if (handler is null)
        {
            return CommandResult.Rejected($"No handler for {typeof(TCommand).Name}.");
        }

        var result = await handler.HandleAsync(command, ct);

        if (result.Accepted)
        {
            Sent?.Invoke();
            _ = sync.SyncNowAsync();
        }

        return result;
    }
}
```

- [ ] **Step 4: Register `ISyncTrigger` in the DI container**

In `src/PSPad.App/Program.cs`, immediately after the existing `builder.Services.AddScoped<SyncCoordinator>();` line (line 89), add:

```csharp
builder.Services.AddScoped<ISyncTrigger>(sp => sp.GetRequiredService<SyncCoordinator>());
```

- [ ] **Step 5: Run the full `PSPad.App.Tests` unit suite**

Run: `dotnet test test/PSPad.App.Tests --filter Category=Unit`
Expected: PASS — all tests including the 3 updated `CommandSenderTests` and the 9 `SyncCoordinatorTests`.

- [ ] **Step 6: Build the whole solution to catch any other `new CommandSender(...)` call site**

Run: `dotnet build PSPad.slnx`
Expected: Build succeeds with 0 errors. (A grep before this plan was written found no other production call site constructing `CommandSender` directly — `Program.cs`'s `AddScoped<CommandSender>()` uses constructor injection — but the build is the actual check.)

- [ ] **Step 7: Commit**

```bash
git add src/PSPad.App/State/Dispatch/CommandSender.cs src/PSPad.App/Program.cs test/PSPad.App.Tests/State/Dispatch/CommandSenderTests.cs
git commit -m "feat: trigger sync immediately after every accepted command"
```

---

## Task 3: Full-suite verification

**Files:** none (verification only).

- [ ] **Step 1: Run the full unit suite**

Run: `dotnet test --filter Category=Unit`
Expected: PASS, 0 failures.

- [ ] **Step 2: Run the full integration suite (requires Docker running)**

Run: `dotnet test --filter Category=Integration`
Expected: PASS, 0 failures. This change touches no server code, no wire contracts, and no MongoDB shape, so no integration test should be affected — a failure here means something outside this plan's stated scope broke and needs investigation before merging.

- [ ] **Step 3: Confirm no other file references `SyncCoordinator`'s old bare-`SyncNowAsync`-as-only-entry-point assumption**

Run: `grep -rn "SyncNowAsync" src/ test/`
Expected: call sites are `CommandSender.SendAsync` (new), `SyncCoordinator.Start()`, `SyncCoordinator.LoopAsync()`, the `CameOnline` handler inside `SyncCoordinator.Start()`, and the test files from Tasks 1–2. No other production code calls it directly (e.g. no manual "sync now" UI button exists per the original exploration — out of scope).
