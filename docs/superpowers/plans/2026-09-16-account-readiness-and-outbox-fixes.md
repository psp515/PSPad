# Account Readiness and Outbox Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix four bugs found in manual testing of `psp/feature`: a crash opening History, a permanently-broken user icon/header on first login, a wedged sync outbox that repeats "That command is for a different user." forever, and a missing-padding/misaligned avatar on the mobile app bar.

**Architecture:** Three of the four bugs (crash, missing user data, wedged outbox) trace to one root cause: `AppShell.OnInitializedAsync` sets `_ready = true` — which unconditionally renders `@Body` (every page in the app) — even when fetching the signed-in user's account (`/api/me`) failed or hadn't resolved yet, leaving `AppState` at its all-defaults construction values (`UserId = Guid.Empty`, `Today = default(DateOnly)` = `0001-01-01`). Task 1 fixes the root cause: retry the account fetch a bounded number of times (the WASM authentication library can have a brief race on first login where the access token isn't cached yet), and never advance past the loading screen with unpopulated state — show a distinct, recoverable error message instead. Task 2 is a narrow recovery fix for users already affected: today, ANY rejected command sits in the outbox forever and is resent — and re-rejected, and re-warned-about — on every single sync cycle, because nothing ever removes it. For the four *structural* rejections in `CommandDispatcher` (unknown command, malformed payload, wrong user, no handler), retrying the identical command can never produce a different verdict, so those are now dropped from the outbox once surfaced to the user. Genuine domain-level rejections (the kind ADR-0005's last-write-wins design already covers) are untouched — they keep exactly their current queued-and-retried behavior. Task 3 is an unrelated, purely cosmetic CSS/markup fix to the mobile app bar's avatar.

**Tech Stack:** .NET 10, Blazor WebAssembly, MudBlazor, bUnit (component tests), xUnit.

**Spec:** No design spec covers this — these are bug fixes to already-shipped behavior (from `specs/ui-polish-design.md`'s settings/nav work and the identity fixes on `psp/feature`), found via manual testing per that branch's own `PLAN.md`. Root cause for each bug was established via `superpowers:systematic-debugging` before this plan was written; each task below states the causal chain, not just the symptom.

## Global Constraints

- No comments in code except a genuinely non-obvious WHY (AGENTS.md). Every code block below that includes a comment has one because the reasoning isn't obvious from the code alone — keep those, don't add others.
- TDD: write the failing test first, watch it fail, then implement (AGENTS.md, this repo's binding rule).
- Every test class needs a category trait: `[UnitTest]` for these three tasks — none of this touches MongoDB or needs Docker.
- One type per file, following existing folder conventions — none of these tasks add new files, all are edits to existing files.
- Be terse. No speculative abstraction, no "while I'm here" cleanup beyond what's specified.
- Task 2 changes `PSPad.Contracts.CommandResponse`, a shared wire contract between `PSPad.Api` and `PSPad.App` (AD-3). The new field must default to `false` so every existing 3-argument call site (production and test) keeps compiling unchanged.
- Task 2 does **not** require a new or updated ADR: `adr/0005-offline-conflicts-last-write-wins.md` (AD-5) governs genuine last-write-wins domain conflicts between two devices, and requires only that rejections are "always surfaced to the user — never dropped silently." Task 2 keeps every rejection surfaced (the snackbar warning is unchanged) and only stops *retrying* the four rejections that can never succeed on retry (they aren't domain conflicts at all — they're a malformed envelope, an unknown command type, a missing handler, or a command stamped with the wrong user id). Domain rejections — the actual subject of ADR-0005 — keep their current queued-forever-until-surfaced-and-untouched behavior exactly as today. If you disagree with this reading while implementing, stop and flag it rather than silently writing an ADR or silently skipping it.

---

### Task 1: AppShell never renders with an unpopulated account

**Files:**
- Modify: `src/PSPad.App/Layout/AppShell.razor`
- Test: `test/PSPad.App.Tests/Layout/AppShellTests.cs`

**Interfaces:**
- Consumes: `PSPadApiClient.MeAsync()` (`src/PSPad.App/Api/PSPadApiClient.cs:9`) — returns `Task<MeResponse?>`, already exists, unchanged. `AppState` (`src/PSPad.App/State/AppState.cs`) — `UserId`, `DisplayName`, `Email`, `TimeZone`, `Today` — already exists, unchanged.
- Produces: nothing new consumed by later tasks. This task is self-contained.

Today, `AppShell.OnInitializedAsync` does this (`src/PSPad.App/Layout/AppShell.razor:88-120`):

```csharp
protected override async Task OnInitializedAsync()
{
    Coordinator.Changed += OnSyncChanged;
    Preference.Changed += OnPreferenceChanged;
    Sender.Sent += OnCommandSent;
    Navigation.LocationChanged += OnLocationChanged;

    await Viewport.SubscribeAsync(OnDesktopChanged);

    var authState = AuthStateTask is null ? null : await AuthStateTask;
    if (authState?.User.Identity?.IsAuthenticated == true)
    {
        var me = await Api.MeAsync();

        if (me is not null)
        {
            _displayName = me.DisplayName;
            _email = me.Email;
            State.UserId = me.UserId;
            State.DisplayName = me.DisplayName;
            State.Email = me.Email;
            State.TimeZone = me.TimeZone;
            State.Today = TodayRule.TodayIn(
                DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(me.TimeZone));
            await Ownership.EnsureCurrentUserAsync(me.UserId);
            await ReloadAreasAndCountsAsync();
        }
    }

    _ready = true;
    Coordinator.Start();
    ReadOpenTask();
}
```

`_ready = true` runs unconditionally, even when `me is null`. `AppState.Today` defaults to `DateOnly` (`0001-01-01`) and `AppState.UserId` defaults to `Guid.Empty` (`src/PSPad.App/State/AppState.cs`). `MudMainContent` renders `@Body` — every routed page in the app — whenever `_ready` is true (`AppShell.razor:59-70`). `src/PSPad.App/Pages/HistoryPage.razor:73-83`'s `OnInitializedAsync` calls `BurndownRule.Build(_tasks, State.Today, _days, zone)`, and `BurndownRule.Build` (`src/modules/PSPad.Module.Tasks/Analytics/BurndownRule.cs:13-15`) does `today.AddDays(-offset)` for `offset` up to `days - 1` (29 for the default 30-day window) — `DateOnly(0001-01-01).AddDays(-29)` underflows below `DateOnly.MinValue` and throws `ArgumentOutOfRangeException`, which is the exact crash reported. The same unset `State.UserId` is also what gets stamped onto any command a user creates in this state (e.g. `new CreateArea(Guid.NewGuid(), State.UserId, ...)` in this same file) — when that command reaches the server, `CommandDispatcher.DispatchAsync` (`src/PSPad.Api/Commands/CommandDispatcher.cs:26-29`) rejects it with "That command is for a different user." because `command.UserId` (`Guid.Empty`) doesn't match the real authenticated user id.

`me` can be null for two reasons, both real: `PSPadApiClient.GetAsync<T>` (`src/PSPad.App/Api/PSPadApiClient.cs:59-70`) catches `AccessTokenNotAvailableException` and returns `default` — this is the Blazor WASM authentication library's own signal that it needs to redirect to refresh the token, which can happen transiently right after a first sign-in redirect completes, before the token is cached. It does **not** catch `HttpRequestException` — a genuine offline/network failure propagates out of `MeAsync()` uncaught, which today would also crash `OnInitializedAsync` (a second, currently-unreported but directly related bug in the same code path — fix both in the same method since a correct fix for "handle `Api.MeAsync()` failing" has to cover both failure modes, not just the one that happened to get noticed first).

**Fix:** retry `Api.MeAsync()` a bounded number of times (the token race is expected to resolve within milliseconds), catching `HttpRequestException` as one of the retry-worthy outcomes. If it still hasn't succeeded after the retries, don't set `_ready = true` — show a distinct "couldn't load your account" message with a manual retry action, and never populate `State` with anything partial.

- [ ] **Step 1: Write the failing tests**

Add `using PSPad.Contracts;` to the top of `test/PSPad.App.Tests/Layout/AppShellTests.cs` if it isn't already imported (check first — `MeResponse` is referenced there already via `new MeResponse(...)`, so it may already be present via another using; if the file compiles today referencing `MeResponse` with no explicit `PSPad.Contracts` using, it's coming from an existing using in that file — check before adding a duplicate).

Replace the existing `FakeMeHandler` class (`test/PSPad.App.Tests/Layout/AppShellTests.cs:251-261`) with two small fakes — one whose failure count can be lowered after construction (for the retry tests), and one that hangs forever (for the existing `resolveMe: false` behavior needed by `ItShowsTheBrandLoaderUntilTheShellIsReady`):

```csharp
sealed class FakeMeHandler(MeResponse response) : HttpMessageHandler
{
    public int FailuresRemaining { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (FailuresRemaining > 0)
        {
            FailuresRemaining--;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(response)
        });
    }
}

sealed class HangingMeHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
        new TaskCompletionSource<HttpResponseMessage>().Task;
}
```

Change the `Arrange` method (`test/PSPad.App.Tests/Layout/AppShellTests.cs:190-218`) from `bool resolveMe = true` to `int meFailures = 0, bool meHangs = false`, keep a reference to the failing handler so a test can later make it succeed, and build whichever handler the parameters ask for:

```csharp
FakeMeHandler? _meHandler;

void Arrange(
    string displayName = "Ada Lovelace",
    string email = "ada@example.com",
    string? emailClaim = "ada@example.com",
    int meFailures = 0,
    bool meHangs = false,
    params Aggregate[] documents)
{
    var today = new DateOnly(2026, 9, 12);
    var replica = AppTestHost.Arrange(this, User, today, documents);
    Services.AddSingleton(new ThemePreference(JSInterop.JSRuntime));
    Services.AddSingleton(new SidebarCounts(
        new ReplicaDocumentStore<Module.Tasks.Tasks.TodoTask>(replica),
        new ReplicaDocumentStore<Module.Tasks.Inbox.Inbox>(replica),
        new AppState { UserId = User, Today = today }));

    var meResponse = new MeResponse(User, displayName, email, "UTC");
    HttpMessageHandler handler;
    if (meHangs)
    {
        handler = new HangingMeHandler();
    }
    else
    {
        _meHandler = new FakeMeHandler(meResponse) { FailuresRemaining = meFailures };
        handler = _meHandler;
    }

    Services.AddSingleton(new PSPadApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") }));
    Services.AddSingleton<ISyncApi>(sp => sp.GetRequiredService<PSPadApiClient>());
    Services.AddSingleton<IConnectivity>(new FakeConnectivity());
    Services.AddScoped<SyncService>();
    Services.AddScoped<SyncCoordinator>();

    var authStateTask = AuthenticatedAs(displayName, emailClaim);
    RenderTree.Add<CascadingValue<Task<AuthenticationState>>>(parameters =>
        parameters.Add(cascade => cascade.Value, authStateTask));
}
```

Update the one existing caller of the old `resolveMe` parameter, `ItShowsTheBrandLoaderUntilTheShellIsReady` (`test/PSPad.App.Tests/Layout/AppShellTests.cs:163`):

```csharp
Arrange(meHangs: true);
```

Now add three new tests, placed after `ItShowsTheBrandLoaderUntilTheShellIsReady`:

```csharp
[Fact]
public void ARetriedAccountFetchStillLoadsNormallyOnceItSucceeds()
{
    Arrange(meFailures: 1);
    var authStateTask = AuthenticatedAs("Ada Lovelace", "ada@example.com");

    var shell = Render<AppShell>(parameters => parameters.AddCascadingValue(authStateTask));

    shell.WaitForAssertion(() =>
    {
        var sidebar = shell.FindComponents<NavSidebar>()[0].Instance;
        Assert.Equal(User, sidebar.UserId);
        Assert.Equal("Ada Lovelace", sidebar.DisplayName);
    }, TimeSpan.FromSeconds(2));
}

[Fact]
public void WhenTheAccountNeverLoadsTheShellShowsARecoverableMessageInsteadOfRenderingBroken()
{
    Arrange(meFailures: 10);
    var authStateTask = AuthenticatedAs("Ada Lovelace", "ada@example.com");

    var shell = Render<AppShell>(parameters => parameters.AddCascadingValue(authStateTask));

    shell.WaitForAssertion(() =>
    {
        Assert.Empty(shell.FindComponents<BrandLoader>());
        Assert.Single(shell.FindComponents<MudAlert>());
        Assert.Contains("couldn't load your account", shell.Markup, StringComparison.OrdinalIgnoreCase);
    }, TimeSpan.FromSeconds(2));

    var sidebar = shell.FindComponents<NavSidebar>()[0].Instance;
    Assert.Equal(Guid.Empty, sidebar.UserId);
}

[Fact]
public void RetryingFromTheRecoverableMessageLoadsTheAccount()
{
    Arrange(meFailures: 10);
    var authStateTask = AuthenticatedAs("Ada Lovelace", "ada@example.com");
    var shell = Render<AppShell>(parameters => parameters.AddCascadingValue(authStateTask));
    shell.WaitForAssertion(() => Assert.Single(shell.FindComponents<MudAlert>()), TimeSpan.FromSeconds(2));

    _meHandler!.FailuresRemaining = 0;
    shell.Find(".pspad-account-retry").Click();

    shell.WaitForAssertion(() =>
    {
        var sidebar = shell.FindComponents<NavSidebar>()[0].Instance;
        Assert.Equal(User, sidebar.UserId);
    }, TimeSpan.FromSeconds(2));
}
```

This last test requires the retry control in the markup (Step 3, below) to carry a `pspad-account-retry` CSS class so the test can find it — add that class when you write the markup.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~AppShellTests" --filter Category=Unit`
Expected: `ARetriedAccountFetchStillLoadsNormallyOnceItSucceeds`, `WhenTheAccountNeverLoadsTheShellShowsARecoverableMessageInsteadOfRenderingBroken`, and `RetryingFromTheRecoverableMessageLoadsTheAccount` all FAIL (the first two because there's no retry yet — one 503 response is treated as `me is null` today and the shell renders "ready" anyway with default state, so `sidebar.UserId` won't equal `User`; the recoverable-message test fails because no `MudAlert` exists yet). Confirm the two pre-existing tests you touched (`ItShowsTheBrandLoaderUntilTheShellIsReady` and anything else referencing the old `FakeMeHandler`/`resolveMe`) still compile and pass — if they don't compile, that's expected at this point since you haven't changed `AppShell.razor` yet only if the compile error is about `AppShell.razor`, not the test file itself.

- [ ] **Step 3: Implement the minimal fix**

Add `@using PSPad.Contracts` near the top of `src/PSPad.App/Layout/AppShell.razor` (needed because the new `FetchMeAsync` method below writes the `MeResponse` type name explicitly, unlike the current code which only ever uses it through `var`).

Add a field:

```csharp
bool _accountUnavailable;
```

Replace the `MudMainContent` block (`AppShell.razor:59-70`):

```razor
<MudMainContent>
    <MudContainer MaxWidth="MaxWidth.Large" Class="my-4 pspad-content">
        @if (_ready)
        {
            @Body
        }
        else if (_accountUnavailable)
        {
            <MudAlert Severity="Severity.Warning" Class="pspad-account-unavailable">
                Couldn't load your account. Check your connection, then
                <MudLink Class="pspad-account-retry" OnClick="@RetryAccountAsync">try again</MudLink>.
            </MudAlert>
        }
        else
        {
            <BrandLoader />
        }
    </MudContainer>
</MudMainContent>
```

Replace `OnInitializedAsync` and add the three new methods it calls (`AppShell.razor:88-120`):

```csharp
protected override async Task OnInitializedAsync()
{
    Coordinator.Changed += OnSyncChanged;
    Preference.Changed += OnPreferenceChanged;
    Sender.Sent += OnCommandSent;
    Navigation.LocationChanged += OnLocationChanged;

    await Viewport.SubscribeAsync(OnDesktopChanged);

    var authState = AuthStateTask is null ? null : await AuthStateTask;
    if (authState?.User.Identity?.IsAuthenticated == true)
    {
        await LoadAccountAsync();
    }
    else
    {
        _ready = true;
    }

    Coordinator.Start();
    ReadOpenTask();
}

async Task LoadAccountAsync()
{
    var me = await FetchAccountAsync();

    if (me is null)
    {
        _accountUnavailable = true;
        return;
    }

    _accountUnavailable = false;
    _displayName = me.DisplayName;
    _email = me.Email;
    State.UserId = me.UserId;
    State.DisplayName = me.DisplayName;
    State.Email = me.Email;
    State.TimeZone = me.TimeZone;
    State.Today = TodayRule.TodayIn(
        DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(me.TimeZone));
    await Ownership.EnsureCurrentUserAsync(me.UserId);
    await ReloadAreasAndCountsAsync();

    _ready = true;
}

async Task<MeResponse?> FetchAccountAsync()
{
    const int attempts = 3;

    for (var attempt = 0; attempt < attempts; attempt++)
    {
        try
        {
            var me = await Api.MeAsync();
            if (me is not null)
            {
                return me;
            }
        }
        catch (HttpRequestException)
        {
        }

        if (attempt < attempts - 1)
        {
            // The WASM auth library can briefly report "authenticated" before its token cache
            // catches up right after a sign-in redirect -- this closes that window without a
            // long, user-visible stall.
            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }
    }

    return null;
}

async Task RetryAccountAsync()
{
    await LoadAccountAsync();
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~AppShellTests" --filter Category=Unit`
Expected: PASS — all tests in the file, including the three new ones and every pre-existing test.

- [ ] **Step 5: Run the full unit suite to confirm no regression**

Run: `dotnet test --filter Category=Unit`
Expected: PASS, same total count as before plus 3.

- [ ] **Step 6: Commit**

```bash
git add src/PSPad.App/Layout/AppShell.razor test/PSPad.App.Tests/Layout/AppShellTests.cs
git commit -m "fix: never render the app with an account that failed to load"
```

---

### Task 2: A structurally-rejected command stops wedging the whole outbox

**Files:**
- Modify: `src/shared/PSPad.Contracts/CommandResponse.cs`
- Modify: `src/PSPad.Api/Commands/CommandDispatcher.cs`
- Modify: `src/PSPad.App/Sync/SyncService.cs`
- Test: `test/PSPad.Api.Tests/Commands/CommandDispatcherTests.cs`
- Test: `test/PSPad.App.Tests/Sync/SyncServiceTests.cs`

**Interfaces:**
- Consumes: nothing from Task 1 or Task 3 — independent.
- Produces: nothing consumed by other tasks.

Today, `SyncService.PushAsync` (`src/PSPad.App/Sync/SyncService.cs:31-60`) sends a batch of outbox commands, and on the first rejection it removes only the *accepted* commands before it, leaving the rejected command (and everything after it) in the outbox permanently. The rejected command is resent, identically, on every subsequent sync cycle — 60 seconds later, and every 60 seconds after that (`SyncCoordinator.LoopAsync`, `src/PSPad.App/Sync/SyncCoordinator.cs:28-38`) — and rejected identically every time, showing the same snackbar warning (`SyncCoordinator.RunAsync:47-51`) over and over. This is "frequently" seeing the same rejection message. It also means every command queued *behind* the stuck one never gets a chance to sync, forever, since the batch always starts from the same stuck position.

For four specific rejections — `CommandDispatcher.DispatchAsync`'s early returns for an unknown command type, a malformed payload, a command whose `UserId` doesn't match the authenticated user, and a missing handler (`src/PSPad.Api/Commands/CommandDispatcher.cs:16-42`) — retrying the *exact same command* can never produce a different verdict: the command's own contents are permanently wrong, not the server's current state. (Task 1 stops new "wrong user" commands from ever being created, but Task 1 doesn't help anyone who already has one of these four stuck in their outbox from before that fix shipped — this task does.) A genuine domain rejection (a business rule enforced by the command's own handler, e.g. "That list no longer exists.") is different: it's a judgment about current server state, not the command's own well-formedness, and per ADR-0005 it should keep being surfaced and stays queued exactly as it does today — this task does not change that path at all.

**Fix:** add an `Unrecoverable` flag to `CommandResponse`, set it on the four structural rejections only, and have `SyncService.PushAsync` drop that one outbox entry (after surfacing its rejection message, same as today) instead of leaving it queued.

- [ ] **Step 1: Write the failing tests**

In `test/PSPad.Api.Tests/Commands/CommandDispatcherTests.cs`, extend the existing `AnEnvelopeForSomebodyElsesUserIdIsRejected` test and add three more for the other structural cases:

```csharp
[Fact]
public async Task AnEnvelopeForSomebodyElsesUserIdIsRejected()
{
    var services = new ServiceCollection().AddPSPadCommands().BuildServiceProvider();
    var envelope = new CommandEnvelope(
        nameof(CreateArea),
        JsonSerializer.SerializeToElement(
            new CreateArea(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Home", 0)));

    var response = await new CommandDispatcher(services).DispatchAsync(
        envelope, Guid.NewGuid(), CancellationToken.None);

    Assert.False(response.Accepted);
    Assert.True(response.Unrecoverable);
}

[Fact]
public async Task AnUnknownCommandTypeIsRejectedRatherThanThrown()
{
    var services = new ServiceCollection().AddPSPadCommands().BuildServiceProvider();

    var response = await new CommandDispatcher(services).DispatchAsync(
        new CommandEnvelope("DropDatabase", JsonSerializer.SerializeToElement(new { })),
        Guid.NewGuid(),
        CancellationToken.None);

    Assert.False(response.Accepted);
    Assert.True(response.Unrecoverable);
}

[Fact]
public async Task AMalformedPayloadIsRejectedAsUnrecoverable()
{
    var services = new ServiceCollection().AddPSPadCommands().BuildServiceProvider();
    var envelope = new CommandEnvelope(nameof(CreateArea), JsonSerializer.SerializeToElement(new { }));

    var response = await new CommandDispatcher(services).DispatchAsync(
        envelope, Guid.NewGuid(), CancellationToken.None);

    Assert.False(response.Accepted);
    Assert.True(response.Unrecoverable);
}

[Fact]
public async Task ACommandWithNoRegisteredHandlerIsRejectedAsUnrecoverable()
{
    var services = new ServiceCollection().BuildServiceProvider();
    var user = Guid.NewGuid();
    var envelope = new CommandEnvelope(
        nameof(CreateArea),
        JsonSerializer.SerializeToElement(new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), "Home", 0)));

    var response = await new CommandDispatcher(services).DispatchAsync(
        envelope, user, CancellationToken.None);

    Assert.False(response.Accepted);
    Assert.True(response.Unrecoverable);
}

[Fact]
public async Task AnAcceptedCommandIsNotMarkedUnrecoverable()
{
    var work = new FakeUnitOfWork();
    var services = new ServiceCollection()
        .AddSingleton<IUnitOfWork>(work)
        .AddSingleton<IClock>(new FixedClock(DateTimeOffset.UnixEpoch))
        .AddSingleton<IDocumentStore<Area>>(new FakeDocumentStore<Area>())
        .AddPSPadCommands()
        .BuildServiceProvider();
    var user = Guid.NewGuid();
    var command = new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), "Home", 0);
    var envelope = new CommandEnvelope(nameof(CreateArea), JsonSerializer.SerializeToElement(command));

    var response = await new CommandDispatcher(services).DispatchAsync(
        envelope, user, CancellationToken.None);

    Assert.True(response.Accepted);
    Assert.False(response.Unrecoverable);
}
```

`AMalformedPayloadIsRejectedAsUnrecoverable` relies on `CreateArea`'s JSON shape being different enough from `new { }` that deserialization fails to produce a valid command — this matches how `CommandDispatcher.DispatchAsync` already checks `envelope.Payload.Deserialize(type, Json) is not ICommand command` (`CommandDispatcher.cs:21-24`). If deserializing `new { }` into `CreateArea` unexpectedly succeeds (all-default fields) instead of failing, that's a real behavior discovery worth a one-line note in your task report, but it does not block this task — the important cases (`wrong user`, `unknown command`, `no handler`) are the ones the actual bug report needs, and `AnAcceptedCommandIsNotMarkedUnrecoverable` guards the untouched path (`Unrecoverable` stays `false` for successful commands) either way.

In `test/PSPad.App.Tests/Sync/SyncServiceTests.cs`, add a new test after `TheOutboxStopsAtTheFirstRejectionAndKeepsWhatFollows`:

```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~CommandDispatcherTests" --filter Category=Unit`
Expected: the four new/extended tests FAIL to compile (`CommandResponse` has no `Unrecoverable` member yet).

Run: `dotnet test --filter "FullyQualifiedName~SyncServiceTests" --filter Category=Unit`
Expected: `AnUnrecoverableRejectionIsDroppedInsteadOfWedgingTheOutbox` FAILS to compile for the same reason.

- [ ] **Step 3: Implement the minimal fix**

`src/shared/PSPad.Contracts/CommandResponse.cs` — add the field with a default so every existing 3-argument call site keeps compiling:

```csharp
namespace PSPad.Contracts;

public sealed record CommandResponse(Guid CommandId, bool Accepted, string? Rejection, bool Unrecoverable = false);
```

`src/PSPad.Api/Commands/CommandDispatcher.cs` — mark the four structural early returns `Unrecoverable: true` (leave the final, handler-produced response — line 48 — untouched, so a genuine domain rejection keeps `Unrecoverable` at its default `false`):

```csharp
public async Task<CommandResponse> DispatchAsync(
    CommandEnvelope envelope, Guid userId, CancellationToken ct)
{
    var type = CommandCatalogue.Resolve(envelope.Type);
    if (type is null)
    {
        return new CommandResponse(Guid.Empty, false, $"Unknown command {envelope.Type}.", Unrecoverable: true);
    }

    if (envelope.Payload.Deserialize(type, Json) is not ICommand command)
    {
        return new CommandResponse(Guid.Empty, false, $"Malformed payload for {envelope.Type}.", Unrecoverable: true);
    }

    if (command.UserId != userId)
    {
        return new CommandResponse(command.CommandId, false, "That command is for a different user.", Unrecoverable: true);
    }

    var work = services.GetService<IUnitOfWork>();
    if (work is not null && await work.IsProcessedAsync(command.CommandId, ct))
    {
        // Checked here, not in Decide: by replay time the aggregate already reflects the
        // first application, so the domain layer alone cannot tell a replay from a genuine conflict.
        return new CommandResponse(command.CommandId, true, null);
    }

    var handler = services.GetService(typeof(ICommandHandler<>).MakeGenericType(type));
    if (handler is null)
    {
        return new CommandResponse(command.CommandId, false, $"No handler for {envelope.Type}.", Unrecoverable: true);
    }

    var method = handler.GetType().GetMethod(nameof(ICommandHandler<ICommand>.HandleAsync))!;
    var result = await (Task<CommandResult>)method.Invoke(handler, [command, ct])!;

    return new CommandResponse(command.CommandId, result.Accepted, result.Rejection);
}
```

`src/PSPad.App/Sync/SyncService.cs` — replace `PushAsync` (`SyncService.cs:31-60`):

```csharp
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

    var rejected = accepted < responses.Count ? responses[accepted] : null;

    // A structural rejection (unknown command, malformed payload, wrong user, no handler) is a
    // verdict against the command's own contents -- resending it unchanged rejects it
    // identically forever, so it is dropped once surfaced instead of blocking everything behind
    // it. A domain rejection from the command's own handler stays queued, unchanged from before.
    var removeThrough = rejected is { Unrecoverable: true } ? accepted : accepted - 1;
    if (removeThrough >= 0)
    {
        await outbox.RemoveThroughAsync(batch[removeThrough].Position);
    }

    var rejections = rejected?.Rejection is { } reason ? new[] { reason } : Array.Empty<string>();
    return (accepted, rejections);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~CommandDispatcherTests" --filter Category=Unit`
Expected: PASS, all 6 tests in the file (the original 3, 2 of them now with an added assertion, plus 3 new).

Run: `dotnet test --filter "FullyQualifiedName~SyncServiceTests" --filter Category=Unit`
Expected: PASS, all 5 tests in the file — including the pre-existing `TheOutboxStopsAtTheFirstRejectionAndKeepsWhatFollows`, which must still assert `Assert.Equal(2, await outbox.CountAsync())` unchanged (that test's rejection is a domain rejection with `Unrecoverable` defaulting to `false`, so this fix must not touch its outcome).

- [ ] **Step 5: Run the full unit suite to confirm no regression**

Run: `dotnet test --filter Category=Unit`
Expected: PASS, same total count as before Task 1 and Task 2's additions, plus all new tests from both.

- [ ] **Step 6: Commit**

```bash
git add src/shared/PSPad.Contracts/CommandResponse.cs src/PSPad.Api/Commands/CommandDispatcher.cs src/PSPad.App/Sync/SyncService.cs test/PSPad.Api.Tests/Commands/CommandDispatcherTests.cs test/PSPad.App.Tests/Sync/SyncServiceTests.cs
git commit -m "fix: drop a structurally-rejected command instead of retrying it forever"
```

---

### Task 3: Padding and end-alignment for the mobile app bar's avatar

**Files:**
- Modify: `src/PSPad.App/Layout/AccountMenu.razor`
- Test: `test/PSPad.App.Tests/Layout/AccountMenuTests.cs`

**Interfaces:**
- Consumes: nothing from Task 1 or Task 2 — independent, purely cosmetic.
- Produces: nothing consumed by other tasks.

`AccountMenu.razor`'s `AvatarOnly` branch (used only by the mobile `MudAppBar` in `AppShell.razor:36-37`) renders a bare `MudAvatar` with no padding:

```razor
@if (AvatarOnly)
{
    <MudAvatar Style="@($"background:{AvatarColor.For(UserId)};color:#FFFFFF")" Size="Size.Medium">
        @AvatarColor.InitialOf(DisplayName.Length > 0 ? DisplayName : Email)
    </MudAvatar>
}
```

The non-avatar-only branch, immediately below, wraps its content in `class="d-flex align-center gap-2 px-3 py-2 pspad-account-activator"` (`AccountMenu.razor:14`) — giving it `12px` horizontal / `8px` vertical padding. The mobile app bar's `MudIconButton` (the hamburger menu, `AppShell.razor:33-34`) has its own built-in button padding. The bare avatar has neither, so it sits flush against the app bar's edge with no breathing room, asymmetric with the icon button on the opposite side — this is the reported "no padding for user icon inside header," and the missing padding is also why it doesn't read as sitting "at the end" of the bar the way a padded element would.

**Fix:** wrap the avatar in the same padding the non-avatar-only branch already uses.

- [ ] **Step 1: Write the failing test**

Add this test to `test/PSPad.App.Tests/Layout/AccountMenuTests.cs`, after `TheAvatarFallsBackToTheEmailWhenThereIsNoName`:

```csharp
[Fact]
public void TheAvatarOnlyActivatorHasPadding()
{
    Arrange();

    var menu = Render<AccountMenu>(parameters => parameters
        .Add(account => account.DisplayName, "Ada Lovelace")
        .Add(account => account.Email, "ada@example.com")
        .Add(account => account.UserId, User)
        .Add(account => account.AvatarOnly, true));

    var wrapper = menu.Find(".pspad-account-avatar-only");
    Assert.Contains("px-3", wrapper.ClassList);
    Assert.Contains("py-2", wrapper.ClassList);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test --filter "FullyQualifiedName~TheAvatarOnlyActivatorHasPadding" --filter Category=Unit`
Expected: FAIL with a `Bunit.ElementNotFoundException` (or equivalent) — `.pspad-account-avatar-only` doesn't exist yet.

- [ ] **Step 3: Implement the minimal fix**

Replace the `AvatarOnly` branch in `src/PSPad.App/Layout/AccountMenu.razor:6-11`:

```razor
@if (AvatarOnly)
{
    <div class="px-3 py-2 pspad-account-avatar-only">
        <MudAvatar Style="@($"background:{AvatarColor.For(UserId)};color:#FFFFFF")" Size="Size.Medium">
            @AvatarColor.InitialOf(DisplayName.Length > 0 ? DisplayName : Email)
        </MudAvatar>
    </div>
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test --filter "FullyQualifiedName~TheAvatarOnlyActivatorHasPadding" --filter Category=Unit`
Expected: PASS.

- [ ] **Step 5: Run the full AccountMenu and AppShell test files to confirm no regression**

Run: `dotnet test --filter "FullyQualifiedName~AccountMenuTests|FullyQualifiedName~AppShellTests" --filter Category=Unit`
Expected: PASS, every test in both files (`AppShell.razor` renders `AccountMenu` with `AvatarOnly="true"` in its mobile app bar, so confirm nothing there broke).

- [ ] **Step 6: Run the full unit suite to confirm no regression**

Run: `dotnet test --filter Category=Unit`
Expected: PASS, same total as after Task 2 plus 1.

- [ ] **Step 7: Commit**

```bash
git add src/PSPad.App/Layout/AccountMenu.razor test/PSPad.App.Tests/Layout/AccountMenuTests.cs
git commit -m "fix: pad the mobile app bar's avatar-only activator"
```

---

## After all three tasks

Run the full suite one more time (`dotnet test --filter Category=Unit` and, if Docker is available, `dotnet test --filter Category=Integration`) and confirm a clean `dotnet build` (0 warnings, 0 errors) before considering this plan done.

**Not covered by this plan, and not automatable:** actually reproducing the first-login race against a real Keycloak realm (Task 1's fix is inferred from the code's structure and the reported crash's exact stack trace, not from an observed live trace of the token race itself — this is the strongest explanation the evidence supports, but if the crash recurs after this ships, the next step is adding temporary logging around `Api.MeAsync()`'s failure path in a real deployment to see which of the two failure modes — token race or something else — is actually firing). Also not covered: manually confirming the avatar padding/alignment looks right on a real phone/tablet screen (Task 3's fix targets the padding asymmetry found by reading the two branches side by side; render it and eyeball it once implemented).
