# Offline-First Session Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the installed PWA open offline with its data, gating on a durable local session instead of a live OIDC access token.

**Architecture:** A `LocalSession` record in its own IndexedDB object store carries user id, display name, email, time zone, refresh token and last-server-contact time. A bootstrapper resolves it before the first render and decides app-or-login; a custom `AuthenticationStateProvider` reports authenticated from it; a `TokenRefresher` renews the access token by a direct call to Keycloak's token endpoint and hands it to our own `DelegatingHandler`. Network failure means offline, never sign-out.

**Tech Stack:** .NET 10, Blazor WebAssembly, MudBlazor, IndexedDB via JS interop, Keycloak (OIDC), xUnit v3 + bUnit.

**Spec:** `specs/offline-first-session-design.md` (decision of record: `adr/0027-local-session-gates-the-app-not-the-access-token.md`)

## Global Constraints

- Offline trust window is exactly **7 days**. Comparison is `>` — a session at exactly 7 days is still fresh.
- Realm lifetimes: `ssoSessionIdleTimeout` **2592000** (30 days), `ssoSessionMaxLifespan` **7776000** (90 days).
- All new client types live in `src/PSPad.App/Auth/`. One type per file, filename matches the type.
- **No comments in code.** Exception: one line stating *why* for a genuinely counter-intuitive constraint.
- Every test class carries `[UnitTest]` from `PSPad.TestInfrastructure`. Unit tests only — no Testcontainers, no Docker, no network.
- Time comes from the injected `IClock` (`UtcNow` is `DateTimeOffset`). Never `DateTime.UtcNow`.
- A refresh failure is a sign-out **only** on HTTP 400 carrying `invalid_grant`. Every transport failure, timeout and 5xx is offline.
- On trust-window expiry: clear the replica and the session, **never** the outbox.
- **Every component that reads `ILocalSessionStore` treats a throwing store as "no session", never as an exception** (spec §7: "IndexedDB unreadable → treat as no session. Login."). In production the store is IndexedDB JS interop and can throw `JSException` when site data is blocked. The catch is narrow — around the store call only, never a blanket catch that would swallow the component's own programming errors. Each such component carries a regression test using a throwing `ILocalSessionStore` fake.
- English in code, comments, commits and docs.
- Run `dotnet test --project test/PSPad.App.Tests --filter Category=Unit` before each commit. The `--project` flag is required on this SDK; the bare path form fails.

---

### Task 1: LocalSession store

The session must live in its own object store. `clearReplica` in `replica.js:89` wipes both `documents` and `meta`, so a session kept in `meta` would be destroyed by every replica purge — including ADR-0018's user-switch path — re-creating the bug this plan fixes.

**Files:**
- Modify: `src/PSPad.App/wwwroot/js/replica.js:1-33` (version bump, new store, export `run`)
- Create: `src/PSPad.App/wwwroot/js/session.js`
- Create: `src/PSPad.App/Auth/LocalSession.cs`
- Create: `src/PSPad.App/Auth/ILocalSessionStore.cs`
- Create: `src/PSPad.App/Auth/LocalSessionStore.cs`
- Create: `test/PSPad.App.Tests/Auth/InMemoryLocalSessionStore.cs`
- Test: `test/PSPad.App.Tests/Auth/ReplicaScriptTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `LocalSession(Guid UserId, string DisplayName, string Email, string TimeZone, string RefreshToken, DateTimeOffset LastServerContactUtc)`; `ILocalSessionStore` with `Task<LocalSession?> LoadAsync()`, `Task SaveAsync(LocalSession session)`, `Task ClearAsync()`; test fake `InMemoryLocalSessionStore`.

- [ ] **Step 1: Write the failing test**

`test/PSPad.App.Tests/Auth/ReplicaScriptTests.cs`:

```csharp
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Auth;

[UnitTest]
public class ReplicaScriptTests
{
    static readonly string Replica = File.ReadAllText(PathTo("replica.js"));
    static readonly string Session = File.ReadAllText(PathTo("session.js"));

    [Fact]
    public void ItBumpsTheDatabaseVersionForTheSessionStore()
    {
        Assert.Contains("const VERSION = 2;", Replica);
    }

    [Fact]
    public void ItCreatesTheSessionStore()
    {
        Assert.Contains("'session'", Replica);
    }

    [Fact]
    public void ItKeepsTheSessionOutOfTheClearedStores()
    {
        var clear = Replica[Replica.IndexOf("export function clearReplica", StringComparison.Ordinal)..];
        Assert.DoesNotContain("session", clear);
    }

    [Fact]
    public void ItSharesOneDatabaseOpener()
    {
        Assert.Contains("from './replica.js'", Session);
    }

    static string PathTo(string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "src", "PSPad.App", "wwwroot", "js", file);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/PSPad.App.Tests --filter Category=Unit`
Expected: FAIL — `session.js` does not exist.

- [ ] **Step 3: Bump the database and add the store**

In `src/PSPad.App/wwwroot/js/replica.js`, change `const VERSION = 1;` to `const VERSION = 2;`, add to `onupgradeneeded` after the `outbox` block:

```js
      if (!db.objectStoreNames.contains('session')) {
        db.createObjectStore('session', { keyPath: 'key' });
      }
```

and change `function open()` to `export function open()` and `function run(` to `export function run(`.

- [ ] **Step 4: Create `session.js`**

```js
import { run } from './replica.js';

export function load() {
  return run('session', 'readonly', session => session.get('current'));
}

export function save(value) {
  return run('session', 'readwrite', session => session.put({ key: 'current', value }));
}

export function clear() {
  return run('session', 'readwrite', session => session.clear());
}
```

- [ ] **Step 5: Create the record and interface**

`src/PSPad.App/Auth/LocalSession.cs`:

```csharp
namespace PSPad.App.Auth;

public sealed record LocalSession(
    Guid UserId,
    string DisplayName,
    string Email,
    string TimeZone,
    string RefreshToken,
    DateTimeOffset LastServerContactUtc);
```

`src/PSPad.App/Auth/ILocalSessionStore.cs`:

```csharp
namespace PSPad.App.Auth;

public interface ILocalSessionStore
{
    Task<LocalSession?> LoadAsync();

    Task SaveAsync(LocalSession session);

    Task ClearAsync();
}
```

- [ ] **Step 6: Create the IndexedDB store**

`src/PSPad.App/Auth/LocalSessionStore.cs`, following `IndexedDbReplica`'s module pattern:

```csharp
using Microsoft.JSInterop;

namespace PSPad.App.Auth;

public sealed class LocalSessionStore(IJSRuntime js) : ILocalSessionStore, IAsyncDisposable
{
    IJSObjectReference? _module;

    public async Task<LocalSession?> LoadAsync()
    {
        var module = await ModuleAsync();
        var row = await module.InvokeAsync<SessionRow?>("load");
        return row?.Value;
    }

    public async Task SaveAsync(LocalSession session)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("save", session);
    }

    public async Task ClearAsync()
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("clear");
    }

    async Task<IJSObjectReference> ModuleAsync() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/session.js");

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }

    public sealed record SessionRow(string Key, LocalSession Value);
}
```

- [ ] **Step 7: Create the test fake**

`test/PSPad.App.Tests/Auth/InMemoryLocalSessionStore.cs`:

```csharp
using PSPad.App.Auth;

namespace PSPad.App.Tests.Auth;

public sealed class InMemoryLocalSessionStore(LocalSession? session = null) : ILocalSessionStore
{
    public LocalSession? Current { get; private set; } = session;

    public int Clears { get; private set; }

    public Task<LocalSession?> LoadAsync() => Task.FromResult(Current);

    public Task SaveAsync(LocalSession value)
    {
        Current = value;
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        Current = null;
        Clears++;
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 8: Run tests to verify they pass**

Run: `dotnet test test/PSPad.App.Tests --filter Category=Unit`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add src/PSPad.App/wwwroot/js/replica.js src/PSPad.App/wwwroot/js/session.js src/PSPad.App/Auth test/PSPad.App.Tests/Auth
git commit -m "feat(auth): add durable LocalSession store in its own IndexedDB store"
```

---

### Task 2: TokenRefresher

**Files:**
- Create: `src/PSPad.App/Auth/RefreshOutcome.cs`
- Create: `src/PSPad.App/Auth/TokenRefresher.cs`
- Test: `test/PSPad.App.Tests/Auth/TokenRefresherTests.cs`

**Interfaces:**
- Consumes: `IClock` from `PSPad.Abstractions`.
- Produces: `RefreshOutcome` (`Renewed(string AccessToken, DateTimeOffset ExpiresAt, string RefreshToken)`, `Offline`, `Revoked`); `TokenRefresher(HttpClient http, IClock clock, string authority, string clientId)` with `Task<RefreshOutcome> RefreshAsync(string refreshToken)`, `string? AccessToken`, `DateTimeOffset AccessTokenExpiresAt`.

- [ ] **Step 1: Write the failing test**

`test/PSPad.App.Tests/Auth/TokenRefresherTests.cs`:

```csharp
using System.Net;
using PSPad.Abstractions;
using PSPad.App.Auth;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Auth;

[UnitTest]
public class TokenRefresherTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ItRenewsFromASuccessfulExchange()
    {
        var refresher = Refresher(Respond(HttpStatusCode.OK,
            """{"access_token":"at","expires_in":300,"refresh_token":"rotated"}"""));

        var outcome = await refresher.RefreshAsync("stored");

        var renewed = Assert.IsType<RefreshOutcome.Renewed>(outcome);
        Assert.Equal("at", renewed.AccessToken);
        Assert.Equal("rotated", renewed.RefreshToken);
        Assert.Equal(Now.AddSeconds(300), renewed.ExpiresAt);
    }

    [Fact]
    public async Task ItTreatsInvalidGrantAsRevoked()
    {
        var refresher = Refresher(Respond(HttpStatusCode.BadRequest,
            """{"error":"invalid_grant","error_description":"Token is not active"}"""));

        Assert.IsType<RefreshOutcome.Revoked>(await refresher.RefreshAsync("stored"));
    }

    [Fact]
    public async Task ItTreatsATransportFailureAsOfflineNotSignOut()
    {
        var refresher = Refresher(new ThrowingHandler(new HttpRequestException("offline")));

        Assert.IsType<RefreshOutcome.Offline>(await refresher.RefreshAsync("stored"));
    }

    [Fact]
    public async Task ItTreatsAServerErrorAsOffline()
    {
        var refresher = Refresher(Respond(HttpStatusCode.InternalServerError, ""));

        Assert.IsType<RefreshOutcome.Offline>(await refresher.RefreshAsync("stored"));
    }

    [Fact]
    public async Task ItCachesTheAccessTokenForTheHandler()
    {
        var refresher = Refresher(Respond(HttpStatusCode.OK,
            """{"access_token":"at","expires_in":300,"refresh_token":"rotated"}"""));

        await refresher.RefreshAsync("stored");

        Assert.Equal("at", refresher.AccessToken);
        Assert.Equal(Now.AddSeconds(300), refresher.AccessTokenExpiresAt);
    }

    static TokenRefresher Refresher(HttpMessageHandler handler) =>
        new(new HttpClient(handler), new FixedClock(Now),
            "http://localhost:8080/realms/psplace", "pspad-frontend");

    static StubHandler Respond(HttpStatusCode status, string body) => new(status, body);

    sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }

    sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw exception;
    }

    sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/PSPad.App.Tests --filter Category=Unit`
Expected: FAIL — `RefreshOutcome` and `TokenRefresher` do not exist.

- [ ] **Step 3: Write the outcome type**

`src/PSPad.App/Auth/RefreshOutcome.cs`:

```csharp
namespace PSPad.App.Auth;

public abstract record RefreshOutcome
{
    public sealed record Renewed(string AccessToken, DateTimeOffset ExpiresAt, string RefreshToken)
        : RefreshOutcome;

    public sealed record Offline : RefreshOutcome;

    public sealed record Revoked : RefreshOutcome;
}
```

- [ ] **Step 4: Write the refresher**

`src/PSPad.App/Auth/TokenRefresher.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PSPad.Abstractions;

namespace PSPad.App.Auth;

public sealed class TokenRefresher(HttpClient http, IClock clock, string authority, string clientId)
{
    public string? AccessToken { get; private set; }

    public DateTimeOffset AccessTokenExpiresAt { get; private set; }

    public async Task<RefreshOutcome> RefreshAsync(string refreshToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await http.PostAsync(
                $"{authority.TrimEnd('/')}/protocol/openid-connect/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["client_id"] = clientId,
                    ["refresh_token"] = refreshToken
                }));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return new RefreshOutcome.Offline();
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var error = await response.Content.ReadAsStringAsync();
            return error.Contains("invalid_grant")
                ? new RefreshOutcome.Revoked()
                : new RefreshOutcome.Offline();
        }

        if (!response.IsSuccessStatusCode)
        {
            return new RefreshOutcome.Offline();
        }

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>();

        if (payload is null)
        {
            return new RefreshOutcome.Offline();
        }

        AccessToken = payload.AccessToken;
        AccessTokenExpiresAt = clock.UtcNow.AddSeconds(payload.ExpiresIn);

        return new RefreshOutcome.Renewed(payload.AccessToken, AccessTokenExpiresAt, payload.RefreshToken);
    }

    sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("refresh_token")] string RefreshToken);
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test test/PSPad.App.Tests --filter Category=Unit`
Expected: PASS, 5 new tests.

- [ ] **Step 6: Commit**

```bash
git add src/PSPad.App/Auth/RefreshOutcome.cs src/PSPad.App/Auth/TokenRefresher.cs test/PSPad.App.Tests/Auth/TokenRefresherTests.cs
git commit -m "feat(auth): renew access tokens via Keycloak's token endpoint, not an iframe"
```

---

### Task 3: SessionBootstrapper

**Files:**
- Create: `src/PSPad.App/Auth/SessionStartup.cs`
- Create: `src/PSPad.App/Auth/SessionBootstrapper.cs`
- Test: `test/PSPad.App.Tests/Auth/SessionBootstrapperTests.cs`

**Interfaces:**
- Consumes: `ILocalSessionStore` and `InMemoryLocalSessionStore` (Task 1); `IReplica` and `InMemoryReplica`; `IClock`.
- Produces: `enum SessionStartup { NoSession, Ready }`; `SessionBootstrapper(ILocalSessionStore sessions, IReplica replica, IClock clock)` with `Task<SessionStartup> StartAsync()` and `public static readonly TimeSpan TrustWindow`.

- [ ] **Step 1: Write the failing test**

`test/PSPad.App.Tests/Auth/SessionBootstrapperTests.cs`:

```csharp
using PSPad.Abstractions;
using PSPad.App.Auth;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Auth;

[UnitTest]
public class SessionBootstrapperTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ItReportsNoSessionWhenNoneWasStored()
    {
        var sessions = new InMemoryLocalSessionStore();

        Assert.Equal(SessionStartup.NoSession, await Bootstrapper(sessions).StartAsync());
    }

    [Fact]
    public async Task ItOpensTheAppOnAFreshSessionWithoutTouchingTheNetwork()
    {
        var sessions = new InMemoryLocalSessionStore(Session(Now.AddDays(-3)));

        Assert.Equal(SessionStartup.Ready, await Bootstrapper(sessions).StartAsync());
        Assert.NotNull(sessions.Current);
    }

    [Fact]
    public async Task ItKeepsASessionSittingExactlyOnTheWindow()
    {
        var sessions = new InMemoryLocalSessionStore(Session(Now.AddDays(-7)));

        Assert.Equal(SessionStartup.Ready, await Bootstrapper(sessions).StartAsync());
    }

    [Fact]
    public async Task ItExpiresASessionPastTheWindow()
    {
        var sessions = new InMemoryLocalSessionStore(Session(Now.AddDays(-7).AddSeconds(-1)));
        var replica = new InMemoryReplica();

        Assert.Equal(SessionStartup.NoSession, await Bootstrapper(sessions, replica).StartAsync());
        Assert.Null(sessions.Current);
    }

    [Fact]
    public async Task ItLeavesTheOutboxAloneOnExpiry()
    {
        var sessions = new InMemoryLocalSessionStore(Session(Now.AddDays(-30)));
        var outbox = new InMemoryOutbox();
        await outbox.AppendAsync(Guid.NewGuid(), null!);

        await Bootstrapper(sessions).StartAsync();

        Assert.Equal(1, await outbox.CountAsync());
    }

    static SessionBootstrapper Bootstrapper(
        InMemoryLocalSessionStore sessions, InMemoryReplica? replica = null) =>
        new(sessions, replica ?? new InMemoryReplica(), new FixedClock(Now));

    static LocalSession Session(DateTimeOffset lastContact) =>
        new(Guid.NewGuid(), "Zoe", "zoe@example.com", "Europe/Warsaw", "refresh", lastContact);

    sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
```

If `InMemoryOutbox.AppendAsync` rejects a null envelope, build a real `CommandEnvelope` instead — the assertion is on the count, not the payload.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/PSPad.App.Tests --filter Category=Unit`
Expected: FAIL — `SessionStartup` and `SessionBootstrapper` do not exist.

- [ ] **Step 3: Write the startup enum**

`src/PSPad.App/Auth/SessionStartup.cs`:

```csharp
namespace PSPad.App.Auth;

public enum SessionStartup
{
    NoSession,
    Ready
}
```

- [ ] **Step 4: Write the bootstrapper**

`src/PSPad.App/Auth/SessionBootstrapper.cs`:

```csharp
using PSPad.Abstractions;
using PSPad.App.State.Replica;

namespace PSPad.App.Auth;

public sealed class SessionBootstrapper(ILocalSessionStore sessions, IReplica replica, IClock clock)
{
    public static readonly TimeSpan TrustWindow = TimeSpan.FromDays(7);

    public async Task<SessionStartup> StartAsync()
    {
        var session = await sessions.LoadAsync();

        if (session is null)
        {
            return SessionStartup.NoSession;
        }

        if (clock.UtcNow - session.LastServerContactUtc > TrustWindow)
        {
            // The outbox survives: the replica is re-fetchable from the server, locally authored commands are not.
            await replica.ClearAsync();
            await sessions.ClearAsync();
            return SessionStartup.NoSession;
        }

        return SessionStartup.Ready;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test test/PSPad.App.Tests --filter Category=Unit`
Expected: PASS, 5 new tests.

- [ ] **Step 6: Commit**

```bash
git add src/PSPad.App/Auth/SessionStartup.cs src/PSPad.App/Auth/SessionBootstrapper.cs test/PSPad.App.Tests/Auth/SessionBootstrapperTests.cs
git commit -m "feat(auth): decide app-or-login from the local session before first render"
```

---

### Task 4: LocalAuthenticationStateProvider

**Files:**
- Create: `src/PSPad.App/Auth/LocalAuthenticationStateProvider.cs`
- Test: `test/PSPad.App.Tests/Auth/LocalAuthenticationStateProviderTests.cs`

**Interfaces:**
- Consumes: `ILocalSessionStore`, `InMemoryLocalSessionStore` (Task 1).
- Produces: `LocalAuthenticationStateProvider(ILocalSessionStore sessions) : AuthenticationStateProvider` with `SignedIn(LocalSession session)` and `SignedOut()`.

- [ ] **Step 1: Write the failing test**

`test/PSPad.App.Tests/Auth/LocalAuthenticationStateProviderTests.cs`:

```csharp
using System.Security.Claims;
using PSPad.App.Auth;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Auth;

[UnitTest]
public class LocalAuthenticationStateProviderTests
{
    static readonly LocalSession Stored = new(
        Guid.NewGuid(), "Zoe", "zoe@example.com", "Europe/Warsaw", "refresh",
        new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task ItAuthenticatesFromTheSessionAloneWithNoAccessToken()
    {
        var provider = new LocalAuthenticationStateProvider(new InMemoryLocalSessionStore(Stored));

        var state = await provider.GetAuthenticationStateAsync();

        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.Equal(Stored.UserId.ToString(), state.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("Zoe", state.User.Identity?.Name);
    }

    [Fact]
    public async Task ItStaysAnonymousWithoutASession()
    {
        var provider = new LocalAuthenticationStateProvider(new InMemoryLocalSessionStore());

        var state = await provider.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task ItPublishesSignIn()
    {
        var provider = new LocalAuthenticationStateProvider(new InMemoryLocalSessionStore());
        var notified = 0;
        provider.AuthenticationStateChanged += _ => notified++;

        provider.SignedIn(Stored);

        Assert.Equal(1, notified);
        Assert.True((await provider.GetAuthenticationStateAsync()).User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task ItPublishesSignOut()
    {
        var provider = new LocalAuthenticationStateProvider(new InMemoryLocalSessionStore(Stored));
        await provider.GetAuthenticationStateAsync();

        provider.SignedOut();

        Assert.False((await provider.GetAuthenticationStateAsync()).User.Identity?.IsAuthenticated);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/PSPad.App.Tests --filter Category=Unit`
Expected: FAIL — `LocalAuthenticationStateProvider` does not exist.

- [ ] **Step 3: Write the provider**

`src/PSPad.App/Auth/LocalAuthenticationStateProvider.cs`:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace PSPad.App.Auth;

public sealed class LocalAuthenticationStateProvider(ILocalSessionStore sessions)
    : AuthenticationStateProvider
{
    LocalSession? _session;
    bool _loaded;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_loaded)
        {
            _session = await sessions.LoadAsync();
            _loaded = true;
        }

        return new AuthenticationState(Principal(_session));
    }

    public void SignedIn(LocalSession session)
    {
        _session = session;
        _loaded = true;
        NotifyAuthenticationStateChanged(
            Task.FromResult(new AuthenticationState(Principal(session))));
    }

    public void SignedOut()
    {
        _session = null;
        _loaded = true;
        NotifyAuthenticationStateChanged(
            Task.FromResult(new AuthenticationState(Principal(null))));
    }

    static ClaimsPrincipal Principal(LocalSession? session) =>
        session is null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, session.UserId.ToString()),
                    new Claim(ClaimTypes.Name, session.DisplayName),
                    new Claim(ClaimTypes.Email, session.Email)
                ],
                "pspad-local", ClaimTypes.Name, ClaimTypes.Role));
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test test/PSPad.App.Tests --filter Category=Unit`
Expected: PASS, 4 new tests.

- [ ] **Step 5: Commit**

```bash
git add src/PSPad.App/Auth/LocalAuthenticationStateProvider.cs test/PSPad.App.Tests/Auth/LocalAuthenticationStateProviderTests.cs
git commit -m "feat(auth): report authentication from the local session, not the token cache"
```

---

### Task 5: SessionAuthorizationHandler

Replacing `AuthorizationMessageHandler` is what keeps the library's `IAccessTokenProvider` from ever resolving on normal routes, which is what stops its iframe renewal timer arming (spec D7).

**Files:**
- Create: `src/PSPad.App/Auth/SessionAuthorizationHandler.cs`
- Test: `test/PSPad.App.Tests/Auth/SessionAuthorizationHandlerTests.cs`

**Interfaces:**
- Consumes: `TokenRefresher`, `ILocalSessionStore` (Tasks 1-2).
- Produces: `SessionAuthorizationHandler(TokenRefresher refresher, ILocalSessionStore sessions, IClock clock) : DelegatingHandler`.

- [ ] **Step 1: Write the failing test**

`test/PSPad.App.Tests/Auth/SessionAuthorizationHandlerTests.cs`:

```csharp
using System.Net;
using PSPad.Abstractions;
using PSPad.App.Auth;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Auth;

[UnitTest]
public class SessionAuthorizationHandlerTests
{
    static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ItAttachesACachedTokenThatIsStillValid()
    {
        var refresher = await RefresherWithToken(expiresIn: 300);
        var captured = new CapturingHandler();
        var client = Client(refresher, new InMemoryLocalSessionStore(Session()), captured);

        await client.GetAsync("http://api.test/me");

        Assert.Equal("Bearer", captured.Request?.Headers.Authorization?.Scheme);
        Assert.Equal("at", captured.Request?.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task ItSendsUnauthenticatedWhenTheSessionStoreThrows()
    {
        var refresher = new TokenRefresher(
            new HttpClient(new ThrowingHandler()), new FixedClock(Now),
            "http://localhost:8080/realms/psplace", "pspad-frontend");
        var captured = new CapturingHandler();
        var handler = new SessionAuthorizationHandler(
            refresher, new ThrowingLocalSessionStore(), new FixedClock(Now))
        {
            InnerHandler = captured
        };

        await new HttpClient(handler).GetAsync("http://api.test/me");

        Assert.Null(captured.Request?.Headers.Authorization);
    }

    [Fact]
    public async Task ItSendsUnauthenticatedWhenOfflineWithNoUsableToken()
    {
        var refresher = new TokenRefresher(
            new HttpClient(new ThrowingHandler()), new FixedClock(Now),
            "http://localhost:8080/realms/psplace", "pspad-frontend");
        var captured = new CapturingHandler();
        var client = Client(refresher, new InMemoryLocalSessionStore(Session()), captured);

        await client.GetAsync("http://api.test/me");

        Assert.Null(captured.Request?.Headers.Authorization);
    }

    static async Task<TokenRefresher> RefresherWithToken(int expiresIn)
    {
        var refresher = new TokenRefresher(
            new HttpClient(new StubHandler(
                $$"""{"access_token":"at","expires_in":{{expiresIn}},"refresh_token":"rotated"}""")),
            new FixedClock(Now), "http://localhost:8080/realms/psplace", "pspad-frontend");

        await refresher.RefreshAsync("stored");

        return refresher;
    }

    static HttpClient Client(
        TokenRefresher refresher, InMemoryLocalSessionStore sessions, HttpMessageHandler inner)
    {
        var handler = new SessionAuthorizationHandler(refresher, sessions, new FixedClock(Now))
        {
            InnerHandler = inner
        };

        return new HttpClient(handler);
    }

    static LocalSession Session() =>
        new(Guid.NewGuid(), "Zoe", "zoe@example.com", "Europe/Warsaw", "refresh", Now);

    sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    sealed class StubHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            });
    }

    sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("offline");
    }

    sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/PSPad.App.Tests --filter Category=Unit`
Expected: FAIL — `SessionAuthorizationHandler` does not exist.

- [ ] **Step 3: Write the handler**

`src/PSPad.App/Auth/SessionAuthorizationHandler.cs`:

```csharp
using System.Net.Http.Headers;
using PSPad.Abstractions;

namespace PSPad.App.Auth;

public sealed class SessionAuthorizationHandler(
    TokenRefresher refresher, ILocalSessionStore sessions, IClock clock) : DelegatingHandler
{
    static readonly TimeSpan Margin = TimeSpan.FromSeconds(30);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await TokenAsync();

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }

    async Task<string?> TokenAsync()
    {
        if (refresher.AccessToken is not null && refresher.AccessTokenExpiresAt - Margin > clock.UtcNow)
        {
            return refresher.AccessToken;
        }

        LocalSession? session;

        try
        {
            session = await sessions.LoadAsync();
        }
        catch
        {
            return null;
        }

        if (session is null)
        {
            return null;
        }

        var outcome = await refresher.RefreshAsync(session.RefreshToken);

        if (outcome is RefreshOutcome.Renewed renewed)
        {
            await sessions.SaveAsync(session with
            {
                RefreshToken = renewed.RefreshToken,
                LastServerContactUtc = clock.UtcNow
            });

            return renewed.AccessToken;
        }

        return null;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test test/PSPad.App.Tests --filter Category=Unit`
Expected: PASS, 2 new tests.

- [ ] **Step 5: Commit**

```bash
git add src/PSPad.App/Auth/SessionAuthorizationHandler.cs test/PSPad.App.Tests/Auth/SessionAuthorizationHandlerTests.cs
git commit -m "feat(auth): attach bearer tokens from the session, replacing the library handler"
```

---

### Task 6: Wire the boot sequence and hold the splash

`.pspad-boot` currently sits inside `#app` (`index.html:62-74`), so Blazor destroys it the instant the root component renders — which is what lets chrome paint before the auth decision. It moves out of `#app` and is removed explicitly once the decision resolves.

**Files:**
- Modify: `src/PSPad.App/wwwroot/index.html:61-74`
- Create: `src/PSPad.App/wwwroot/js/boot.js`
- Modify: `src/PSPad.App/Program.cs:23-28` and `:41-49`, plus new registrations and the pre-run bootstrap
- Test: `test/PSPad.App.Tests/BootScreenTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 1-5.
- Produces: DI registrations — `ILocalSessionStore` → `LocalSessionStore`, `TokenRefresher`, `SessionBootstrapper`, `LocalAuthenticationStateProvider` registered as `AuthenticationStateProvider`, `SessionAuthorizationHandler` on `PSPadApiClient`.

- [ ] **Step 1: Write the failing test**

Append to `test/PSPad.App.Tests/BootScreenTests.cs`:

```csharp
    [Fact]
    public void ItKeepsTheSplashOutsideTheAppElementSoBlazorCannotDropIt()
    {
        var app = Markup.IndexOf("<div id=\"app\">", StringComparison.Ordinal);
        var boot = Markup.IndexOf("class=\"pspad-boot\"", StringComparison.Ordinal);

        Assert.True(boot > 0);
        Assert.True(boot < app || Markup.IndexOf("</div>", app, StringComparison.Ordinal) < boot);
    }

    [Fact]
    public void ItLoadsTheBootModuleThatTearsTheSplashDown()
    {
        Assert.Contains("js/boot.js", Markup);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/PSPad.App.Tests --filter Category=Unit`
Expected: FAIL — the splash is still inside `#app` and `boot.js` is unreferenced.

- [ ] **Step 3: Move the splash out of `#app`**

In `src/PSPad.App/wwwroot/index.html`, replace lines 62-74 so `#app` is empty and the splash is its sibling:

```html
    <div id="app"></div>

    <div class="pspad-boot">
        <svg class="pspad-boot-mark" viewBox="0 0 512 512" xmlns="http://www.w3.org/2000/svg"
             role="img" aria-label="Loading PSPad">
            <rect width="512" height="512" rx="112" fill="#4E7A5E" />
            <path d="M160,96 h160 l80,80 v208 a32,32 0 0 1 -32,32 h-208 a32,32 0 0 1 -32,-32 v-256 a32,32 0 0 1 32,-32 z"
                  fill="#FFFFFF" />
            <path d="M320,96 l80,80 h-48 a32,32 0 0 1 -32,-32 z" fill="#CFE0D5" />
            <path class="pspad-boot-check" d="M184,272 l48,48 l96,-120" fill="none" stroke="#4E7A5E"
                  stroke-width="40" stroke-linecap="round" stroke-linejoin="round" />
        </svg>
    </div>
```

Add `z-index: 9999;` to the `.pspad-boot` rule in the `<style>` block so it covers the app while held.

- [ ] **Step 4: Create `boot.js`**

```js
export function done() {
  document.querySelector('.pspad-boot')?.remove();
}
```

Add before the closing `</body>`, after the other module scripts:

```html
    <script type="module" src="js/boot.js"></script>
```

- [ ] **Step 5: Wire Program.cs**

In `src/PSPad.App/Program.cs`, after the existing `AddOidcAuthentication` block, register the auth services and replace the API client's handler:

```csharp
var keycloakAuthority = builder.Configuration["Keycloak:Authority"]!;
var keycloakClientId = builder.Configuration["Keycloak:ClientId"]!;

builder.Services.AddScoped<ILocalSessionStore, LocalSessionStore>();
builder.Services.AddScoped(services => new TokenRefresher(
    new HttpClient(), services.GetRequiredService<IClock>(), keycloakAuthority, keycloakClientId));
builder.Services.AddScoped<SessionBootstrapper>();
builder.Services.AddScoped<LocalAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(
    services => services.GetRequiredService<LocalAuthenticationStateProvider>());
builder.Services.AddScoped<SessionAuthorizationHandler>();
```

Change the `AddHttpClient<PSPadApiClient>` block to use the new handler:

```csharp
builder.Services.AddHttpClient<PSPadApiClient>(client => client.BaseAddress = new Uri(apiBaseAddress))
    .AddHttpMessageHandler<SessionAuthorizationHandler>();
```

Replace the final line with the pre-render bootstrap:

```csharp
var host = builder.Build();

var bootstrapper = host.Services.GetRequiredService<SessionBootstrapper>();
var boot = await host.Services.GetRequiredService<IJSRuntime>()
    .InvokeAsync<IJSObjectReference>("import", "./js/boot.js");

try
{
    await bootstrapper.StartAsync();
}
finally
{
    // Any bootstrap failure must still reveal the app: a held splash is an unrecoverable blank screen.
    await boot.InvokeVoidAsync("done");
}

await host.RunAsync();
```

Add `using Microsoft.JSInterop;`, `using Microsoft.AspNetCore.Components.Authorization;` and `using PSPad.App.Auth;` to the usings.

- [ ] **Step 6: Run tests and build**

Run: `dotnet build src/PSPad.App && dotnet test test/PSPad.App.Tests --filter Category=Unit`
Expected: build succeeds, all tests PASS.

- [ ] **Step 7: Commit**

```bash
git add src/PSPad.App/Program.cs src/PSPad.App/wwwroot/index.html src/PSPad.App/wwwroot/js/boot.js test/PSPad.App.Tests/BootScreenTests.cs
git commit -m "feat(auth): resolve the session before first render and hold the splash until it does"
```

---

### Task 7: AppShell reads identity from the session

`AppShell.razor:118-144` gates `_ready` on `/api/me`, so an offline boot never becomes ready (`:124` sets `_accountUnavailable` and returns without `_ready = true`).

**Files:**
- Modify: `src/PSPad.App/Layout/AppShell.razor:60-78` (remove the `_accountUnavailable` branch), `:86-87`, `:95-175`
- Test: `test/PSPad.App.Tests/Layout/AppShellTests.cs`

**Interfaces:**
- Consumes: `ILocalSessionStore` (Task 1).
- Produces: no new public types.

- [ ] **Step 1: Write the failing test**

Add to `test/PSPad.App.Tests/Layout/AppShellTests.cs`, following that file's existing `Arrange()` pattern and registering an `InMemoryLocalSessionStore` holding a session:

```csharp
    [Fact]
    public void ItBecomesReadyOfflineWhenTheAccountFetchFails()
    {
        Arrange(accountFetchFails: true);

        var shell = Render<AppShell>();

        Assert.DoesNotContain("try again", shell.Markup);
        Assert.Empty(shell.FindComponents<BrandLoader>());
    }

    [Fact]
    public void ItTakesIdentityFromTheSessionWithoutTheServer()
    {
        Arrange(accountFetchFails: true);

        var shell = Render<AppShell>();

        Assert.Contains("Zoe", shell.Markup);
    }
```

Extend the existing `Arrange` helper with an `accountFetchFails` parameter that gives `PSPadApiClient` an `HttpClient` whose handler throws `HttpRequestException`.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/PSPad.App.Tests --filter Category=Unit`
Expected: FAIL — the shell renders `BrandLoader` and the retry link.

- [ ] **Step 3: Seed state from the session**

In `AppShell.razor`, inject the store (`@inject ILocalSessionStore Sessions`) and replace `OnInitializedAsync`'s auth branch and `LoadAccountAsync` with the following. `SessionOrNullAsync` is a small helper wrapping `Sessions.LoadAsync()` in a try/catch that returns `null` — a store that cannot be read must leave the shell ready and anonymous, never throw out of `OnInitializedAsync`:

```csharp
        var session = await SessionOrNullAsync();

        if (session is not null)
        {
            _displayName = session.DisplayName;
            _email = session.Email;
            State.UserId = session.UserId;
            State.DisplayName = session.DisplayName;
            State.Email = session.Email;
            State.TimeZone = session.TimeZone;
            State.Today = TodayRule.TodayIn(
                DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(session.TimeZone));
            await Ownership.EnsureCurrentUserAsync(session.UserId);
            await ReloadAreasAndCountsAsync();
        }

        _ready = true;
        Coordinator.Start();

        if (session is not null)
        {
            await RefreshAccountAsync(session);
        }
```

Add `RefreshAccountAsync`:

```csharp
    async Task RefreshAccountAsync(LocalSession session)
    {
        MeResponse? fetched;

        try
        {
            fetched = await Api.MeAsync();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Account refresh failed: {exception.Message}");
            return;
        }

        if (fetched is null)
        {
            return;
        }

        var me = fetched.Sanitized();

        _displayName = me.DisplayName;
        _email = me.Email;
        State.DisplayName = me.DisplayName;
        State.Email = me.Email;
        State.TimeZone = me.TimeZone;
        State.Today = TodayRule.TodayIn(
            DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(me.TimeZone));

        await Sessions.SaveAsync(session with
        {
            DisplayName = me.DisplayName,
            Email = me.Email,
            TimeZone = me.TimeZone
        });

        StateHasChanged();
    }
```

Delete `FetchAccountAsync`, `RetryAccountAsync`, the `_accountUnavailable` field at `:87`, and the `else if (_accountUnavailable)` markup branch at `:64-69`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test test/PSPad.App.Tests --filter Category=Unit`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/PSPad.App/Layout/AppShell.razor test/PSPad.App.Tests/Layout/AppShellTests.cs
git commit -m "feat(shell): take identity from the local session so an offline boot is ready"
```

---

### Task 8: Capture the refresh token and brand the auth screens

No supported API exposes the refresh token (`AccessToken` carries only `Value`, `Expires`, `GrantedScopes`), so it is read once from the library's own `sessionStorage` entry immediately after a successful interactive login.

**Files:**
- Modify: `src/PSPad.App/wwwroot/js/session.js` (add `captureOidcRefreshToken`)
- Modify: `src/PSPad.App/Pages/Authentication.razor`
- Test: `test/PSPad.App.Tests/Pages/AuthenticationTests.cs`

**Interfaces:**
- Consumes: `ILocalSessionStore`, `LocalAuthenticationStateProvider`, `PSPadApiClient`.
- Produces: no new public types.

- [ ] **Step 1: Write the failing test**

`test/PSPad.App.Tests/Pages/AuthenticationTests.cs` — a bUnit render of `Authentication` with `Action="login-callback"` asserting the brand mark renders and the default text does not:

```csharp
    [Fact]
    public void ItShowsTheBrandMarkInsteadOfTheLibraryText()
    {
        var page = Render<PSPad.App.Pages.Authentication>(
            parameters => parameters.Add(p => p.Action, "login-callback"));

        Assert.Contains("pspad-boot-mark", page.Markup);
        Assert.DoesNotContain("Completing login", page.Markup);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/PSPad.App.Tests --filter Category=Unit`
Expected: FAIL — the default fragments render.

- [ ] **Step 3: Add the capture function**

Append to `src/PSPad.App/wwwroot/js/session.js`:

```js
export function captureOidcRefreshToken(authority, clientId) {
  const raw = sessionStorage.getItem(`oidc.user:${authority}:${clientId}`);
  if (!raw) {
    return null;
  }
  try {
    return JSON.parse(raw).refresh_token ?? null;
  } catch {
    return null;
  }
}
```

- [ ] **Step 4: Create the shared brand mark component**

`src/PSPad.App/Components/BrandSplash.razor` — the same SVG as `.pspad-boot`, so the mark is defined once for the auth screens:

```razor
<div class="pspad-boot">
    <svg class="pspad-boot-mark" viewBox="0 0 512 512" xmlns="http://www.w3.org/2000/svg"
         role="img" aria-label="Loading PSPad">
        <rect width="512" height="512" rx="112" fill="#4E7A5E" />
        <path d="M160,96 h160 l80,80 v208 a32,32 0 0 1 -32,32 h-208 a32,32 0 0 1 -32,-32 v-256 a32,32 0 0 1 32,-32 z"
              fill="#FFFFFF" />
        <path d="M320,96 l80,80 h-48 a32,32 0 0 1 -32,-32 z" fill="#CFE0D5" />
        <path class="pspad-boot-check" d="M184,272 l48,48 l96,-120" fill="none" stroke="#4E7A5E"
              stroke-width="40" stroke-linecap="round" stroke-linejoin="round" />
    </svg>
</div>
```

- [ ] **Step 5: Brand the fragments and capture on success**

Replace the body of `src/PSPad.App/Pages/Authentication.razor`, keeping the comment block at `:2-5` — ADR-0024's route arrangement is unchanged:

```razor
@page "/authentication/{action}"
@* AppShell would race the login-callback redirect: its one-shot auth check runs before
   RemoteAuthenticatorView finishes processing, sees "unauthenticated", and locks in an
   anonymous session for the rest of the SPA's lifetime (its own layout is never remounted
   by the client-side redirect that follows). Keeping this route off AppShell removes the race. *@
@layout AuthenticationLayout
@using Microsoft.JSInterop
@using PSPad.Abstractions
@using PSPad.App.Auth
@using PSPad.App.Components
@inject IJSRuntime Js
@inject IConfiguration Configuration
@inject ILocalSessionStore Sessions
@inject LocalAuthenticationStateProvider AuthProvider
@inject PSPadApiClient Api
@inject IClock Clock

<RemoteAuthenticatorView Action="@Action" OnLogInSucceeded="@CaptureSessionAsync">
    <LoggingIn><BrandSplash /></LoggingIn>
    <CompletingLoggingIn><BrandSplash /></CompletingLoggingIn>
    <LogOut><BrandSplash /></LogOut>
    <LogOutSucceeded><BrandSplash /></LogOutSucceeded>
</RemoteAuthenticatorView>

@code {
    [Parameter] public string? Action { get; set; }

    async Task CaptureSessionAsync()
    {
        var module = await Js.InvokeAsync<IJSObjectReference>("import", "./js/session.js");

        var refreshToken = await module.InvokeAsync<string?>(
            "captureOidcRefreshToken",
            Configuration["Keycloak:Authority"],
            Configuration["Keycloak:ClientId"]);

        if (refreshToken is null)
        {
            return;
        }

        var me = await Api.MeAsync();

        if (me is null)
        {
            return;
        }

        var account = me.Sanitized();

        var session = new LocalSession(
            account.UserId, account.DisplayName, account.Email, account.TimeZone,
            refreshToken, Clock.UtcNow);

        await Sessions.SaveAsync(session);
        AuthProvider.SignedIn(session);
    }
}
```

`OnLogInSucceeded` exists on `RemoteAuthenticatorViewCore<TState>` in 10.0.12 (verified against the package's XML docs), as do `LoggingIn`, `CompletingLoggingIn`, `LogOut` and `LogOutSucceeded`. It is an `EventCallback` carrying the remote authentication state — if binding a no-argument method fails to compile, give `CaptureSessionAsync` a `RemoteAuthenticationState` parameter and ignore it.

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test test/PSPad.App.Tests --filter Category=Unit`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/PSPad.App/Pages/Authentication.razor src/PSPad.App/wwwroot/js/session.js src/PSPad.App/Components/BrandSplash.razor test/PSPad.App.Tests/Pages
git commit -m "feat(auth): persist the refresh token after login and brand the auth screens"
```

---

### Task 9: Realm session lifetimes and docs

**Files:**
- Modify: `docker/keycloak/realm-psplace.json`
- Modify: `docs/src/pages/install.astro`
- Modify: `docs/src/pages/features.astro`
- Test: `test/PSPad.App.Tests/Auth/RealmConfigurationTests.cs`

**Interfaces:**
- Consumes: the trust window constant from Task 3.
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Write the failing test**

`test/PSPad.App.Tests/Auth/RealmConfigurationTests.cs`:

```csharp
using System.Text.Json;
using PSPad.App.Auth;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Auth;

[UnitTest]
public class RealmConfigurationTests
{
    static readonly JsonDocument Realm = JsonDocument.Parse(File.ReadAllText(PathToRealm()));

    [Fact]
    public void ItKeepsTheServerSessionLongerThanTheLocalTrustWindow()
    {
        var idle = Realm.RootElement.GetProperty("ssoSessionIdleTimeout").GetInt32();

        Assert.True(TimeSpan.FromSeconds(idle) > SessionBootstrapper.TrustWindow);
    }

    [Fact]
    public void ItCapsTheSessionLifespan()
    {
        Assert.Equal(7776000, Realm.RootElement.GetProperty("ssoSessionMaxLifespan").GetInt32());
    }

    static string PathToRealm()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "docker")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "docker", "keycloak", "realm-psplace.json");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/PSPad.App.Tests --filter Category=Unit`
Expected: FAIL — the properties are absent.

- [ ] **Step 3: Set the realm lifetimes**

Add to the root object of `docker/keycloak/realm-psplace.json`, beside the existing realm-level keys:

```json
  "ssoSessionIdleTimeout": 2592000,
  "ssoSessionMaxLifespan": 7776000,
```

- [ ] **Step 4: Update the docs**

In `docs/src/pages/install.astro`, add prose noting that the bundled realm keeps sessions for 30 days idle / 90 days maximum so installed PWAs stay signed in, and that the realm is shared across applications (ADR-0026), so the lifetime applies to all of them.

In `docs/src/pages/features.astro`, state that the app opens and works offline with data, syncing when connectivity returns, and that a device untouched for more than 7 days asks for sign-in again.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter Category=Unit`
Expected: PASS across all unit projects.

- [ ] **Step 6: Commit**

```bash
git add docker/keycloak/realm-psplace.json docs/src/pages/install.astro docs/src/pages/features.astro test/PSPad.App.Tests/Auth/RealmConfigurationTests.cs
git commit -m "feat(identity): set realm session lifetimes and document offline sessions"
```

---

## Verification

After Task 9, confirm the whole suite:

```bash
dotnet test --filter Category=Unit
```

The manual check cannot be automated and must be done on a real device before merge: install the PWA on a phone, sign in, force-quit, enable airplane mode, reopen. Expected: the Today screen with data — never the login screen.

The spec's "offline indicator" is the existing sync status from ADR-0020, driven by `SyncCoordinator` and `IConnectivity`; no task adds new UI for it. If that status does not visibly distinguish offline from idle once this plan lands, that is a follow-up, not part of this work.
