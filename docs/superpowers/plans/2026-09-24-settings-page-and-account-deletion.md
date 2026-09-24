# Settings Page Fixes & Account Deletion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close GitHub issue #23 — rework `SettingsPage` (theme as a list selector, a searchable time zone, a responsive grid, sign-out relocated into the Account card) and add a working, fully-synchronous account-deletion flow (all of a user's data plus their Keycloak login).

**Architecture:** The UI changes are a straight edit of `SettingsPage.razor` following patterns already established elsewhere in the client (`MudGrid`/`MudItem`, `MudAutocomplete`, the `Dialogs.ShowAsync<T>` idiom). Account deletion is a single non-command endpoint (`DELETE /api/account`, per `adr/0033`) that wipes every MongoDB collection for the caller's `userId` inside one transaction, then calls Keycloak's Admin REST API using the bootstrap master-realm admin credentials already in `docker/.env.example`. The client confirms via a type-your-email dialog, awaits the call inline, then clears its replica, outbox and session before landing on `/welcome`.

**Tech Stack:** .NET 10, Blazor WebAssembly, MudBlazor 9.9.0, MongoDB.Driver, xUnit, bUnit, Testcontainers.

**Spec:** `specs/ui-spec.md` (§2, §4, §5 — grid, theme/time zone controls, Danger zone) and `specs/backend-spec.md` (§6, §7 — `DELETE /api/account` mechanics), `adr/0033-account-deletion-bypasses-the-command-pipeline.md`.

## Global Constraints

- No comments in code except a genuinely counter-intuitive constraint (AGENTS.md) — name things instead.
- One type per file, grouped by operation, namespace stays the containing folder's namespace regardless of nesting (AGENTS.md).
- Every test class carries `[UnitTest]` or `[IntegrationTest]` (AGENTS.md §7). Integration classes join `[Collection(MongoCollection.Name)]` and take `MongoFixture`.
- `TreatWarningsAsErrors` is on repo-wide (`Directory.Build.props`) — nullable warnings are build failures.
- `PSPad.Module.Tasks` purity (AD-4) is not touched by this plan — nothing here lives in that module.
- Reuse existing idioms exactly: `@inject IDialogService Dialogs`, `Dialogs.ShowAsync<T>("Title", parameters)` / `await dialog.Result` / `result.Canceled`, `MudGrid Spacing="4"` + `MudItem xs="12" sm="6" md="4" xl="3"`.
- Account deletion requires connectivity; it is never queued offline (`specs/backend-spec.md` §6).

---

## Task 1: Theme selector becomes a `MudSelect` list

**Files:**
- Modify: `src/PSPad.App/Pages/SettingsPage.razor`
- Test: `test/PSPad.App.Tests/Pages/SettingsPageTests.cs`

**Interfaces:**
- Consumes: `ThemePreference.Mode` (`ThemeMode`), `ThemePreference.SetAsync(ThemeMode)` — both already exist and are unchanged.
- Produces: `SettingsPage.SelectThemeAsync(ThemeMode mode)` — a testable code-behind method later tasks and tests can call directly, mirroring the already-tested `ApplyTimeZoneAsync`.

- [ ] **Step 1: Write the failing test**

Add to `test/PSPad.App.Tests/Pages/SettingsPageTests.cs` (near `ItOffersTheThreeThemeModes`):

```csharp
    [Fact]
    public async Task SelectingATimeZoneAppliesItThroughThePreference()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();
        await page.InvokeAsync(() => page.Instance.SelectThemeAsync(ThemeMode.Dark));

        Assert.Equal(ThemeMode.Dark, Services.GetRequiredService<ThemePreference>().Mode);
    }

    [Fact]
    public void TheThemeControlIsASelectNotAButtonGroup()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();

        Assert.Empty(page.FindAll(".mud-button-group"));
        Assert.NotEmpty(page.FindAll(".mud-select"));
    }
```

Add `@using PSPad.App.Theme` is already present in `SettingsPage.razor`'s usings but the test file needs it too — add `using PSPad.App.Theme;` to the top of `SettingsPageTests.cs` if not already implied (`ThemeMode` lives in `PSPad.App.Theme`, already referenced by `Services.AddSingleton(new ThemePreference(...))` in `AppTestHost`, so the namespace is already in scope via existing usings — no change needed since `PSPad.App.Theme` is not currently imported in the test file; add it explicitly).

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/PSPad.App.Tests --filter "FullyQualifiedName~SettingsPageTests" -v`
Expected: FAIL — `SelectThemeAsync` does not exist, `.mud-button-group` is present.

- [ ] **Step 3: Write minimal implementation**

In `src/PSPad.App/Pages/SettingsPage.razor`, replace the Theme card body:

```razor
<MudPaper Outlined="true" Class="pa-4 mb-4">
    <MudText Typo="Typo.subtitle2" Class="mb-3">Theme</MudText>
    <MudSelect T="ThemeMode" Value="@Preference.Mode" ValueChanged="@SelectThemeAsync"
               Variant="Variant.Outlined" Margin="Margin.Dense">
        @foreach (var mode in Enum.GetValues<ThemeMode>())
        {
            <MudSelectItem T="ThemeMode" Value="@mode">@mode</MudSelectItem>
        }
    </MudSelect>
</MudPaper>
```

Add to `@code`:

```csharp
    public Task SelectThemeAsync(ThemeMode mode) => Preference.SetAsync(mode);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test test/PSPad.App.Tests --filter "FullyQualifiedName~SettingsPageTests" -v`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/PSPad.App/Pages/SettingsPage.razor test/PSPad.App.Tests/Pages/SettingsPageTests.cs
git commit -m "feat: theme picker becomes a select list (#23)"
```

---

## Task 2: Time zone becomes a searchable `MudAutocomplete`

**Files:**
- Modify: `src/PSPad.App/Pages/SettingsPage.razor`
- Test: `test/PSPad.App.Tests/Pages/SettingsPageTests.cs`

**Interfaces:**
- Consumes: `SettingsPage.ApplyTimeZoneAsync(string)` — unchanged signature, already public and tested.
- Produces: nothing new consumed by later tasks.

- [ ] **Step 1: Write the failing test**

```csharp
    [Fact]
    public void TheTimeZonePickerIsSearchableRatherThanAFlatDropdown()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();

        Assert.NotEmpty(page.FindAll(".mud-autocomplete"));
    }

    [Fact]
    public async Task TypingNarrowsTheTimeZoneSearchResultsCaseInsensitively()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();
        var matches = await page.Instance.SearchTimeZonesAsync("warsaw", CancellationToken.None);

        Assert.Contains("Europe/Warsaw", matches);
        Assert.DoesNotContain("Pacific/Kiritimati", matches);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/PSPad.App.Tests --filter "FullyQualifiedName~SettingsPageTests" -v`
Expected: FAIL — `.mud-autocomplete` absent, `SearchTimeZonesAsync` does not exist.

- [ ] **Step 3: Write minimal implementation**

Replace the Time zone card body in `src/PSPad.App/Pages/SettingsPage.razor`:

```razor
<MudPaper Outlined="true" Class="pa-4 mb-4">
    <MudText Typo="Typo.subtitle2" Class="mb-3">Time zone</MudText>
    <MudAutocomplete T="string" Value="@State.TimeZone" ValueChanged="@ApplyTimeZoneAsync"
                      SearchFunc="@SearchTimeZonesAsync"
                      Variant="Variant.Outlined" Margin="Margin.Dense" ResetValueOnEmptyText="false" />
    <MudText Typo="Typo.caption">The time zone is saved on the server, so it needs a connection.</MudText>
    @if (_rejected)
    {
        <MudText Typo="Typo.caption" Color="Color.Error">That change did not go through.</MudText>
    }
</MudPaper>
```

Add to `@code` (remove the old `foreach (var zone in TimeZoneInfo.GetSystemTimeZones()...)` markup, it is now generated in code):

```csharp
    static readonly string[] TimeZoneIds =
        TimeZoneInfo.GetSystemTimeZones().Select(zone => zone.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();

    public Task<IEnumerable<string>> SearchTimeZonesAsync(string value, CancellationToken ct) =>
        Task.FromResult(string.IsNullOrWhiteSpace(value)
            ? TimeZoneIds.AsEnumerable()
            : TimeZoneIds.Where(id => id.Contains(value, StringComparison.OrdinalIgnoreCase)));
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test test/PSPad.App.Tests --filter "FullyQualifiedName~SettingsPageTests" -v`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/PSPad.App/Pages/SettingsPage.razor test/PSPad.App.Tests/Pages/SettingsPageTests.cs
git commit -m "feat: searchable time zone autocomplete (#23)"
```

---

## Task 3: Settings cards wrapped in a responsive `MudGrid`

**Files:**
- Modify: `src/PSPad.App/Pages/SettingsPage.razor`
- Modify: `specs/ui-spec.md` (already updated during brainstorming — no further change needed here)
- Test: `test/PSPad.App.Tests/Pages/SettingsPageTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: nothing new consumed by later tasks — purely a layout change.

- [ ] **Step 1: Write the failing test**

```csharp
    [Fact]
    public void TheCardsSitInAResponsiveGrid()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();

        Assert.NotEmpty(page.FindAll(".mud-grid"));
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/PSPad.App.Tests --filter "FullyQualifiedName~SettingsPageTests" -v`
Expected: FAIL — no `.mud-grid` present yet.

- [ ] **Step 3: Write minimal implementation**

Wrap the four (soon five) `MudPaper` cards in `src/PSPad.App/Pages/SettingsPage.razor` with `MudGrid`/`MudItem`, matching `ListPage`/`Today`/`AreaBoard`/`GoalsPage`:

```razor
<MudGrid Spacing="4">
    <MudItem xs="12" sm="6" md="4" xl="3">
        <MudPaper Outlined="true" Class="pa-4 pspad-account-card">
            ...Account card content (moved/edited in Task 4)...
        </MudPaper>
    </MudItem>
    <MudItem xs="12" sm="6" md="4" xl="3">
        <MudPaper Outlined="true" Class="pa-4">
            ...Time zone card...
        </MudPaper>
    </MudItem>
    <MudItem xs="12" sm="6" md="4" xl="3">
        <MudPaper Outlined="true" Class="pa-4">
            ...Theme card...
        </MudPaper>
    </MudItem>
    <MudItem xs="12" sm="6" md="4" xl="3">
        <MudPaper Outlined="true" Class="pa-4">
            ...Sync card...
        </MudPaper>
    </MudItem>
</MudGrid>
```

Each card keeps its existing inner markup from Tasks 1–2 and the current file — only the wrapping `MudGrid`/`MudItem` and the removal of each card's own `mb-4` (the grid's `Spacing="4"` replaces it) change. Do not alter card content beyond what Tasks 1, 2 and 4 already specify.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test test/PSPad.App.Tests --filter "FullyQualifiedName~SettingsPageTests" -v`
Expected: PASS. Also re-run the full `SettingsPageTests` class to confirm nothing else broke:

Run: `dotnet test test/PSPad.App.Tests --filter "FullyQualifiedName~SettingsPageTests" -v`
Expected: all PASS

- [ ] **Step 5: Commit**

```bash
git add src/PSPad.App/Pages/SettingsPage.razor test/PSPad.App.Tests/Pages/SettingsPageTests.cs
git commit -m "feat: settings cards in a responsive grid (#23)"
```

---

## Task 4: Sign-out moves into the Account card

**Files:**
- Modify: `src/PSPad.App/Pages/SettingsPage.razor`
- Test: `test/PSPad.App.Tests/Pages/SettingsPageTests.cs`

**Interfaces:**
- Consumes: existing `SignOutAsync()`, `LocalSignOut`, `IConnectivity`, `Configuration`, `Navigation` — unchanged.
- Produces: nothing new.

- [ ] **Step 1: Write the failing test**

```csharp
    [Fact]
    public void TheSignOutButtonLivesInsideTheAccountCard()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();

        Assert.NotEmpty(page.FindAll(".pspad-account-card .pspad-sign-out"));
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/PSPad.App.Tests --filter "FullyQualifiedName~SettingsPageTests" -v`
Expected: FAIL — the sign-out button is still a sibling below the Sync card, not inside `.pspad-account-card`.

- [ ] **Step 3: Write minimal implementation**

Move the `<MudButton Class="pspad-sign-out" ...>Sign out</MudButton>` markup from the page bottom into the Account card (the `MudItem` carrying `Class="pa-4 pspad-account-card"` from Task 3), directly under the avatar/name/email `<div>`:

```razor
<MudPaper Outlined="true" Class="pa-4 pspad-account-card">
    <MudText Typo="Typo.subtitle2" Class="mb-3">Account</MudText>
    <div class="d-flex align-center gap-3 mb-3">
        <MudAvatar Style="@($"background:{AvatarColor.For(State.UserId)};color:#FFFFFF")" Size="Size.Large">
            @AvatarColor.InitialOf(State.DisplayName.Length > 0 ? State.DisplayName : State.Email)
        </MudAvatar>
        <div>
            <MudText Typo="Typo.body1">@State.DisplayName</MudText>
            <MudText Typo="Typo.body2" Color="Color.Secondary">@State.Email</MudText>
        </div>
    </div>
    <MudButton Class="pspad-sign-out" OnClick="@SignOutAsync" StartIcon="@Icons.Material.Filled.Logout"
               Color="Color.Error" Variant="Variant.Outlined">
        Sign out
    </MudButton>
</MudPaper>
```

Delete the old standalone `<MudButton Class="pspad-sign-out" ...>` block that previously sat after the closing `</MudGrid>` (or wherever it ended up after Task 3's restructuring).

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test test/PSPad.App.Tests --filter "FullyQualifiedName~SettingsPageTests" -v`
Expected: PASS — including all pre-existing `.pspad-sign-out` tests (`SigningOutClearsTheLocalSessionAndTheReplica`, etc.), which still find the button by the same class, just relocated.

- [ ] **Step 5: Commit**

```bash
git add src/PSPad.App/Pages/SettingsPage.razor test/PSPad.App.Tests/Pages/SettingsPageTests.cs
git commit -m "feat: move sign-out into the Account card (#23)"
```

---

## Task 5: `DeleteAccountResponse` contract

**Files:**
- Create: `src/shared/PSPad.Contracts/DeleteAccountResponse.cs`

**Interfaces:**
- Produces: `DeleteAccountResponse(bool KeycloakRemoved)` — used by the Api endpoint (Task 7), the client (Task 9), and their tests.

No test-first cycle here — it is a one-line data contract with no behavior, matching how `SetTimeZoneRequest`/`MeResponse` were added without their own test files. It is exercised indirectly by Tasks 7 and 9's tests.

- [ ] **Step 1: Create the file**

```csharp
namespace PSPad.Contracts;

public sealed record DeleteAccountResponse(bool KeycloakRemoved);
```

- [ ] **Step 2: Build to confirm it compiles**

Run: `dotnet build src/shared/PSPad.Contracts`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/shared/PSPad.Contracts/DeleteAccountResponse.cs
git commit -m "feat: add DeleteAccountResponse contract (#23)"
```

---

## Task 6: `KeycloakAdminClient` — deletes a Keycloak user, retried inline

**Files:**
- Create: `src/PSPad.Api/Identity/KeycloakAdminOptions.cs`
- Create: `src/PSPad.Api/Identity/IKeycloakAdminClient.cs`
- Create: `src/PSPad.Api/Identity/KeycloakAdminClient.cs`
- Test: `test/PSPad.Api.Tests/Identity/KeycloakAdminClientTests.cs`

**Interfaces:**
- Consumes: `HttpClient` (injected), `IOptions<KeycloakAdminOptions>`.
- Produces: `IKeycloakAdminClient.DeleteUserAsync(string subject, CancellationToken ct) : Task<bool>` — consumed by Task 7's endpoint and by `FakeKeycloakAdminClient` (Task 7's test double).

- [ ] **Step 1: Write the failing tests**

Create `test/PSPad.Api.Tests/Identity/KeycloakAdminClientTests.cs`:

```csharp
using System.Net;
using Microsoft.Extensions.Options;
using PSPad.Api.Identity;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Identity;

[UnitTest]
public class KeycloakAdminClientTests
{
    [Fact]
    public async Task ItDeletesTheUserAfterFetchingAnAdminToken()
    {
        var handler = new ScriptedHandler(
            Respond(HttpStatusCode.OK, """{"access_token":"admin-token"}"""),
            Respond(HttpStatusCode.NoContent, ""));

        var result = await Client(handler).DeleteUserAsync("kc-subject", CancellationToken.None);

        Assert.True(result);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(
            "http://localhost:8080/realms/master/protocol/openid-connect/token",
            handler.Requests[0].RequestUri!.ToString());
        Assert.Equal(
            "http://localhost:8080/admin/realms/psplace/users/kc-subject",
            handler.Requests[1].RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Requests[1].Headers.Authorization!.Scheme);
        Assert.Equal("admin-token", handler.Requests[1].Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task ANotFoundOnDeleteCountsAsSuccessBecauseTheUserIsAlreadyGone()
    {
        var handler = new ScriptedHandler(
            Respond(HttpStatusCode.OK, """{"access_token":"admin-token"}"""),
            Respond(HttpStatusCode.NotFound, ""));

        Assert.True(await Client(handler).DeleteUserAsync("kc-subject", CancellationToken.None));
    }

    [Fact]
    public async Task ItRetriesUpToThreeTimesBeforeGivingUp()
    {
        var handler = new ScriptedHandler(
            Respond(HttpStatusCode.OK, """{"access_token":"admin-token"}"""),
            Respond(HttpStatusCode.InternalServerError, ""),
            Respond(HttpStatusCode.OK, """{"access_token":"admin-token"}"""),
            Respond(HttpStatusCode.InternalServerError, ""),
            Respond(HttpStatusCode.OK, """{"access_token":"admin-token"}"""),
            Respond(HttpStatusCode.InternalServerError, ""));

        var result = await Client(handler).DeleteUserAsync("kc-subject", CancellationToken.None);

        Assert.False(result);
        Assert.Equal(6, handler.Requests.Count);
    }

    [Fact]
    public async Task ItSucceedsOnASubsequentAttemptAfterATransientFailure()
    {
        var handler = new ScriptedHandler(
            Respond(HttpStatusCode.OK, """{"access_token":"admin-token"}"""),
            Respond(HttpStatusCode.InternalServerError, ""),
            Respond(HttpStatusCode.OK, """{"access_token":"admin-token"}"""),
            Respond(HttpStatusCode.NoContent, ""));

        Assert.True(await Client(handler).DeleteUserAsync("kc-subject", CancellationToken.None));
    }

    [Fact]
    public async Task AFailedTokenFetchFailsTheAttemptWithoutCallingDelete()
    {
        var handler = new ScriptedHandler(
            Respond(HttpStatusCode.Unauthorized, ""),
            Respond(HttpStatusCode.Unauthorized, ""),
            Respond(HttpStatusCode.Unauthorized, ""));

        var result = await Client(handler).DeleteUserAsync("kc-subject", CancellationToken.None);

        Assert.False(result);
        Assert.Equal(3, handler.Requests.Count);
    }

    static KeycloakAdminClient Client(HttpMessageHandler handler) =>
        new(new HttpClient(handler), Options.Create(new KeycloakAdminOptions
        {
            Authority = "http://localhost:8080/realms/psplace",
            AdminUser = "admin",
            AdminPassword = "admin"
        }));

    static (HttpStatusCode Status, string Body) Respond(HttpStatusCode status, string body) => (status, body);

    sealed class ScriptedHandler(params (HttpStatusCode Status, string Body)[] responses) : HttpMessageHandler
    {
        int _next;

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var (status, body) = responses[_next];
            _next++;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test test/PSPad.Api.Tests --filter "FullyQualifiedName~KeycloakAdminClientTests" -v`
Expected: FAIL to compile — `KeycloakAdminClient`, `KeycloakAdminOptions` do not exist yet.

- [ ] **Step 3: Write minimal implementation**

Create `src/PSPad.Api/Identity/KeycloakAdminOptions.cs`:

```csharp
namespace PSPad.Api.Identity;

public sealed class KeycloakAdminOptions
{
    public const string Section = "Keycloak";

    public string Authority { get; init; } = "";

    public string AdminUser { get; init; } = "";

    public string AdminPassword { get; init; } = "";
}
```

Create `src/PSPad.Api/Identity/IKeycloakAdminClient.cs`:

```csharp
namespace PSPad.Api.Identity;

public interface IKeycloakAdminClient
{
    Task<bool> DeleteUserAsync(string subject, CancellationToken ct);
}
```

Create `src/PSPad.Api/Identity/KeycloakAdminClient.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace PSPad.Api.Identity;

public sealed class KeycloakAdminClient(HttpClient http, IOptions<KeycloakAdminOptions> options) : IKeycloakAdminClient
{
    const int MaxAttempts = 3;

    public async Task<bool> DeleteUserAsync(string subject, CancellationToken ct)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            if (await TryDeleteAsync(subject, ct))
            {
                return true;
            }
        }

        return false;
    }

    async Task<bool> TryDeleteAsync(string subject, CancellationToken ct)
    {
        var token = await GetAdminTokenAsync(ct);

        if (token is null)
        {
            return false;
        }

        var (baseUrl, realm) = SplitAuthority(options.Value.Authority);
        using var request = new HttpRequestMessage(
            HttpMethod.Delete, $"{baseUrl}/admin/realms/{realm}/users/{subject}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response;

        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return false;
        }

        return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound;
    }

    async Task<string?> GetAdminTokenAsync(CancellationToken ct)
    {
        var (baseUrl, _) = SplitAuthority(options.Value.Authority);

        HttpResponseMessage response;

        try
        {
            response = await http.PostAsync(
                $"{baseUrl}/realms/master/protocol/openid-connect/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "password",
                    ["client_id"] = "admin-cli",
                    ["username"] = options.Value.AdminUser,
                    ["password"] = options.Value.AdminPassword
                }), ct);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        try
        {
            var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);
            return payload?.AccessToken;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static (string BaseUrl, string Realm) SplitAuthority(string authority)
    {
        const string marker = "/realms/";
        var index = authority.IndexOf(marker, StringComparison.Ordinal);

        if (index < 0)
        {
            throw new InvalidOperationException(
                $"Keycloak authority '{authority}' is not shaped like '<base>/realms/<realm>'.");
        }

        return (authority[..index], authority[(index + marker.Length)..].TrimEnd('/'));
    }

    sealed record TokenResponse([property: JsonPropertyName("access_token")] string AccessToken);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test test/PSPad.Api.Tests --filter "FullyQualifiedName~KeycloakAdminClientTests" -v`
Expected: all PASS

- [ ] **Step 5: Commit**

```bash
git add src/PSPad.Api/Identity/KeycloakAdminOptions.cs src/PSPad.Api/Identity/IKeycloakAdminClient.cs src/PSPad.Api/Identity/KeycloakAdminClient.cs test/PSPad.Api.Tests/Identity/KeycloakAdminClientTests.cs
git commit -m "feat: KeycloakAdminClient deletes a Keycloak user, retried inline (#23)"
```

---

## Task 7: `DELETE /api/account` — generic Mongo sweep + Keycloak call

**Files:**
- Create: `src/PSPad.Api/Identity/UserDataWipe.cs`
- Create: `src/PSPad.Api/Endpoints/AccountEndpoints.cs`
- Modify: `test/PSPad.Api.Tests/ApiFactory.cs`
- Create: `test/PSPad.Api.Tests/Identity/FakeKeycloakAdminClient.cs`
- Create: `test/PSPad.Api.Tests/Endpoints/AccountEndpointTests.cs`

**Interfaces:**
- Consumes: `IKeycloakAdminClient` (Task 6), `MongoContext` (existing), `ICurrentUser` (existing).
- Produces: `UserDataWipe.RunAsync(MongoContext, Guid userId, CancellationToken) : Task`, the `DELETE /api/account` route (consumed by Task 9's client method), `ApiFactory(MongoFixture, IKeycloakAdminClient?)` — the added optional constructor parameter later tests can pass a fake through.

- [ ] **Step 1: Write the failing tests**

Create `test/PSPad.Api.Tests/Identity/FakeKeycloakAdminClient.cs`:

```csharp
namespace PSPad.Api.Tests.Identity;

public sealed class FakeKeycloakAdminClient(bool succeeds) : global::PSPad.Api.Identity.IKeycloakAdminClient
{
    public List<string> DeletedSubjects { get; } = [];

    public Task<bool> DeleteUserAsync(string subject, CancellationToken ct)
    {
        DeletedSubjects.Add(subject);
        return Task.FromResult(succeeds);
    }
}
```

Modify `test/PSPad.Api.Tests/ApiFactory.cs` to accept an optional Keycloak client override — add the parameter and register it only when supplied:

```csharp
using PSPad.Api.Identity;
```

```csharp
public sealed class ApiFactory(MongoFixture fixture, IKeycloakAdminClient? keycloakAdminClient = null)
    : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(configuration =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = fixture.ConnectionString,
                ["Mongo:Database"] = "pspad_test"
            }));

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(TestAuthenticationHandler.Scheme)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.Scheme, _ => { });

            if (keycloakAdminClient is not null)
            {
                services.AddSingleton(keycloakAdminClient);
            }
        });

    public HttpClient ClientFor(
        string subject, string zone = "Etc/UTC", string? name = null, string? email = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Subject", subject);
        client.DefaultRequestHeaders.Add("X-Test-Zone", zone);

        if (name is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Name", name);
        }

        if (email is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Email", email);
        }

        return client;
    }
}
```

(`services.AddSingleton(keycloakAdminClient)` registered in `ConfigureTestServices` runs after `Program.cs`'s own `AddHttpClient<IKeycloakAdminClient, KeycloakAdminClient>()` from Task 8, and the last registration for a service type wins when resolved as the interface — matching how `.NET`'s `IServiceCollection` composes `ConfigureTestServices` on top of the app's own registrations.)

Create `test/PSPad.Api.Tests/Endpoints/AccountEndpointTests.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Api.Tests.Identity;
using PSPad.Api.Tests.Persistence;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Endpoints;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class AccountEndpointTests(MongoFixture fixture)
{
    [Fact]
    public async Task DeletingTheAccountRemovesEveryDocumentForThatUser()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var subject = Guid.NewGuid().ToString();
        var keycloak = new FakeKeycloakAdminClient(succeeds: true);
        await using var factory = new ApiFactory(fixture, keycloak);
        var client = factory.ClientFor(subject);
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var userId = me!.UserId;

        await client.PostAsJsonAsync("/api/commands", new[]
        {
            new CommandEnvelope(
                nameof(CreateArea),
                JsonSerializer.SerializeToElement(new CreateArea(Guid.NewGuid(), userId, Guid.NewGuid(), "Home", 0)))
        }, ct);

        var response = await client.DeleteAsync("/api/account", ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<DeleteAccountResponse>(ct);

        Assert.True(result!.KeycloakRemoved);
        Assert.Equal([subject], keycloak.DeletedSubjects);

        var context = TestContext.For(fixture);
        var collectionNames = await (await context.Database.ListCollectionNamesAsync(cancellationToken: ct))
            .ToListAsync(ct);

        foreach (var name in collectionNames)
        {
            var remaining = await context.Collection<BsonDocument>(name)
                .Find(Builders<BsonDocument>.Filter.Eq("userId", userId))
                .CountDocumentsAsync(ct);

            Assert.True(remaining == 0, $"Collection '{name}' still has {remaining} document(s) for the deleted user.");
        }
    }

    [Fact]
    public async Task AKeycloakFailureStillReportsSuccessBecauseTheDataIsAlreadyGone()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var subject = Guid.NewGuid().ToString();
        var keycloak = new FakeKeycloakAdminClient(succeeds: false);
        await using var factory = new ApiFactory(fixture, keycloak);
        var client = factory.ClientFor(subject);
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var userId = me!.UserId;

        var response = await client.DeleteAsync("/api/account", ct);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<DeleteAccountResponse>(ct);
        Assert.False(result!.KeycloakRemoved);

        var context = TestContext.For(fixture);
        var remainingUsers = await context.Collection<BsonDocument>("users")
            .Find(Builders<BsonDocument>.Filter.Eq("userId", userId))
            .CountDocumentsAsync(ct);
        Assert.Equal(0, remainingUsers);
    }

    [Fact]
    public async Task DeletingTwiceIsHarmless()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var subject = Guid.NewGuid().ToString();
        var keycloak = new FakeKeycloakAdminClient(succeeds: true);
        await using var factory = new ApiFactory(fixture, keycloak);
        var client = factory.ClientFor(subject);
        await client.GetFromJsonAsync<MeResponse>("/api/me", ct);

        await client.DeleteAsync("/api/account", ct);
        var second = await client.DeleteAsync("/api/account", ct);

        second.EnsureSuccessStatusCode();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test test/PSPad.Api.Tests --filter "FullyQualifiedName~AccountEndpointTests" -v`
Expected: FAIL to compile — `AccountEndpoints`, `UserDataWipe` do not exist, route `/api/account` is unmapped.

- [ ] **Step 3: Write minimal implementation**

Create `src/PSPad.Api/Identity/UserDataWipe.cs`:

```csharp
using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Infrastructure.Mongo;

namespace PSPad.Api.Identity;

public static class UserDataWipe
{
    public static async Task RunAsync(MongoContext context, Guid userId, CancellationToken ct)
    {
        var collectionNames = await (await context.Database.ListCollectionNamesAsync(cancellationToken: ct))
            .ToListAsync(ct);

        using var session = await context.Client.StartSessionAsync(cancellationToken: ct);
        session.StartTransaction();

        try
        {
            foreach (var name in collectionNames)
            {
                await context.Collection<BsonDocument>(name).DeleteManyAsync(
                    session, Builders<BsonDocument>.Filter.Eq("userId", userId), cancellationToken: ct);
            }

            await session.CommitTransactionAsync(ct);
        }
        catch
        {
            await session.AbortTransactionAsync(ct);
            throw;
        }
    }
}
```

Create `src/PSPad.Api/Endpoints/AccountEndpoints.cs`:

```csharp
using PSPad.Api.Identity;
using PSPad.Contracts;
using PSPad.Infrastructure.Mongo;

namespace PSPad.Api.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapDelete("account", async (
            ICurrentUser current, MongoContext context, IKeycloakAdminClient keycloak, CancellationToken ct) =>
        {
            await UserDataWipe.RunAsync(context, current.UserId, ct);
            var keycloakRemoved = await keycloak.DeleteUserAsync(current.Subject, ct);
            return Results.Ok(new DeleteAccountResponse(keycloakRemoved));
        });
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test test/PSPad.Api.Tests --filter "FullyQualifiedName~AccountEndpointTests" -v`
Expected: FAIL — `IKeycloakAdminClient` has no default registration when no fake is supplied, and the route isn't mapped yet. This is expected until Task 8 wires DI and `Program.cs`'s route mapping; re-run after Task 8. If run standalone before Task 8, the test host fails to start (`IKeycloakAdminClient` unresolved) — that failure is the expected state at the end of this task's Step 3; do not treat it as this task being broken. Proceed to Task 8 before re-verifying.

- [ ] **Step 5: Commit**

```bash
git add src/PSPad.Api/Identity/UserDataWipe.cs src/PSPad.Api/Endpoints/AccountEndpoints.cs test/PSPad.Api.Tests/ApiFactory.cs test/PSPad.Api.Tests/Identity/FakeKeycloakAdminClient.cs test/PSPad.Api.Tests/Endpoints/AccountEndpointTests.cs
git commit -m "feat: DELETE /api/account sweeps Mongo and calls Keycloak (#23)"
```

---

## Task 8: Wire DI, route mapping, and compose env vars

**Files:**
- Modify: `src/PSPad.Api/Program.cs`
- Modify: `docker/compose.yaml`

**Interfaces:**
- Consumes: `KeycloakAdminOptions`, `IKeycloakAdminClient`/`KeycloakAdminClient` (Task 6), `AccountEndpoints.MapAccountEndpoints` (Task 7).
- Produces: nothing new — this task only wires what Tasks 6–7 built into the running app.

- [ ] **Step 1: Run Task 7's test to confirm the expected failure**

Run: `dotnet test test/PSPad.Api.Tests --filter "FullyQualifiedName~AccountEndpointTests" -v`
Expected: FAIL (host fails to start — `IKeycloakAdminClient` unresolved), confirming this task is still needed.

- [ ] **Step 2: Wire it up**

In `src/PSPad.Api/Program.cs`, add the using and two registrations, and map the new endpoint group:

```csharp
using PSPad.Api.Identity;
```

After `builder.Services.AddScoped<ICurrentUser, ClaimsCurrentUser>();`, add:

```csharp
builder.Services.Configure<KeycloakAdminOptions>(builder.Configuration.GetSection(KeycloakAdminOptions.Section));
builder.Services.AddHttpClient<IKeycloakAdminClient, KeycloakAdminClient>();
```

After `api.MapMeEndpoints();`, add:

```csharp
api.MapAccountEndpoints();
```

In `docker/compose.yaml`, under the `api` service's `environment:` block, add two lines alongside the existing `Keycloak__*` entries:

```yaml
      Keycloak__AdminUser: ${KEYCLOAK_ADMIN_USER}
      Keycloak__AdminPassword: ${KEYCLOAK_ADMIN_PASSWORD}
```

`KEYCLOAK_ADMIN_USER`/`KEYCLOAK_ADMIN_PASSWORD` already exist in `docker/.env.example` with description comments (they provision Keycloak's own bootstrap admin) — no `.env.example` change needed, per `adr/0033`'s decision to reuse them rather than add a new realm client.

- [ ] **Step 3: Run tests to verify they pass**

Run: `dotnet test test/PSPad.Api.Tests --filter "FullyQualifiedName~AccountEndpointTests" -v`
Expected: all PASS

Run the full Api test suite to confirm nothing else regressed:

Run: `dotnet test test/PSPad.Api.Tests -v`
Expected: all PASS

- [ ] **Step 4: Commit**

```bash
git add src/PSPad.Api/Program.cs docker/compose.yaml
git commit -m "feat: wire KeycloakAdminClient and /api/account into the host (#23)"
```

---

## Task 9: `PSPadApiClient.DeleteAccountAsync()`

**Files:**
- Modify: `src/PSPad.App/Api/PSPadApiClient.cs`
- Test: create `test/PSPad.App.Tests/Api/PSPadApiClientTests.cs` if one does not already exist covering this client — check first; if a `PSPadApiClientTests.cs` file exists, add to it instead of creating a new one.

**Interfaces:**
- Consumes: `HttpClient` (constructor-injected, existing).
- Produces: `PSPadApiClient.DeleteAccountAsync() : Task<DeleteAccountResponse?>` — consumed by Task 11's dialog.

- [ ] **Step 0: Check for an existing client test file**

Run: `find test/PSPad.App.Tests -iname "PSPadApiClientTests.cs"`

If found, add the steps below to that file instead of creating a new one; adjust the `namespace`/`using` block to match its existing style rather than the one shown here.

- [ ] **Step 1: Write the failing test**

If no existing file, create `test/PSPad.App.Tests/Api/PSPadApiClientTests.cs`:

```csharp
using System.Net;
using PSPad.App.Api;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Api;

[UnitTest]
public class PSPadApiClientTests
{
    [Fact]
    public async Task DeleteAccountAsyncReturnsTheParsedResponseOnSuccess()
    {
        var client = ClientWith(Respond(HttpStatusCode.OK, """{"keycloakRemoved":true}"""));

        var result = await client.DeleteAccountAsync();

        Assert.NotNull(result);
        Assert.True(result!.KeycloakRemoved);
    }

    [Fact]
    public async Task DeleteAccountAsyncReturnsNullOnFailureStatus()
    {
        var client = ClientWith(Respond(HttpStatusCode.InternalServerError, ""));

        Assert.Null(await client.DeleteAccountAsync());
    }

    [Fact]
    public async Task DeleteAccountAsyncReturnsNullWhenOffline()
    {
        var client = ClientWith(new ThrowingHandler());

        Assert.Null(await client.DeleteAccountAsync());
    }

    static PSPadApiClient ClientWith(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

    static StubHandler Respond(HttpStatusCode status, string body) => new(status, body);

    sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }

    sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("offline");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/PSPad.App.Tests --filter "FullyQualifiedName~PSPadApiClientTests" -v`
Expected: FAIL to compile — `DeleteAccountAsync` does not exist.

- [ ] **Step 3: Write minimal implementation**

In `src/PSPad.App/Api/PSPadApiClient.cs`, add:

```csharp
    public async Task<DeleteAccountResponse?> DeleteAccountAsync()
    {
        try
        {
            var response = await http.DeleteAsync("api/account");

            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<DeleteAccountResponse>()
                : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test test/PSPad.App.Tests --filter "FullyQualifiedName~PSPadApiClientTests" -v`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/PSPad.App/Api/PSPadApiClient.cs test/PSPad.App.Tests/Api/PSPadApiClientTests.cs
git commit -m "feat: PSPadApiClient.DeleteAccountAsync (#23)"
```

---

## Task 10: `LocalAccountDeletion` — client-side cleanup

**Files:**
- Create: `src/PSPad.App/Auth/LocalAccountDeletion.cs`
- Modify: `src/PSPad.App/Program.cs`
- Create: `test/PSPad.App.Tests/Auth/LocalAccountDeletionTests.cs`

**Interfaces:**
- Consumes: `ILocalSessionStore`, `IReplica`, `IOutbox`, `LocalAuthenticationStateProvider` — all existing.
- Produces: `LocalAccountDeletion.ClearAsync() : Task` — consumed by Task 11's `SettingsPage` wiring.

- [ ] **Step 1: Write the failing test**

Create `test/PSPad.App.Tests/Auth/LocalAccountDeletionTests.cs`:

```csharp
using PSPad.App.Auth;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.Contracts;
using PSPad.TestInfrastructure;
using System.Text.Json;

namespace PSPad.App.Tests.Auth;

[UnitTest]
public class LocalAccountDeletionTests
{
    static readonly Guid User = Guid.NewGuid();

    [Fact]
    public async Task ClearAsyncWipesTheReplicaTheOutboxAndTheSession()
    {
        var sessions = new InMemoryLocalSessionStore(
            new LocalSession(User, "Ada", "ada@example.com", "UTC", "refresh-token", DateTimeOffset.UtcNow));
        var replica = new InMemoryReplica();
        await replica.SetOwnerAsync(User);
        var outbox = new InMemoryOutbox();
        await outbox.AppendAsync(Guid.NewGuid(), new CommandEnvelope("Test", JsonSerializer.SerializeToElement(new { })));
        var authProvider = new LocalAuthenticationStateProvider(sessions);
        var deletion = new LocalAccountDeletion(sessions, replica, outbox, authProvider);

        await deletion.ClearAsync();

        Assert.Null(sessions.Current);
        Assert.Null(await replica.OwnerAsync());
        Assert.Equal(0, await outbox.CountAsync());
    }

    [Fact]
    public async Task ClearAsyncAnnouncesSignedOut()
    {
        var sessions = new InMemoryLocalSessionStore(
            new LocalSession(User, "Ada", "ada@example.com", "UTC", "refresh-token", DateTimeOffset.UtcNow));
        var replica = new InMemoryReplica();
        var outbox = new InMemoryOutbox();
        var authProvider = new LocalAuthenticationStateProvider(sessions);
        var deletion = new LocalAccountDeletion(sessions, replica, outbox, authProvider);
        var announced = false;
        authProvider.AuthenticationStateChanged += _ => announced = true;

        await deletion.ClearAsync();

        Assert.True(announced);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test test/PSPad.App.Tests --filter "FullyQualifiedName~LocalAccountDeletionTests" -v`
Expected: FAIL to compile — `LocalAccountDeletion` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/PSPad.App/Auth/LocalAccountDeletion.cs`:

```csharp
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;

namespace PSPad.App.Auth;

public sealed class LocalAccountDeletion(
    ILocalSessionStore sessions,
    IReplica replica,
    IOutbox outbox,
    LocalAuthenticationStateProvider authenticationState)
{
    public async Task ClearAsync()
    {
        await replica.ClearAsync();
        await outbox.ClearAsync();
        await sessions.ClearAsync();
        authenticationState.SignedOut();
    }
}
```

In `src/PSPad.App/Program.cs`, next to `builder.Services.AddScoped<LocalSignOut>();`, add:

```csharp
builder.Services.AddScoped<LocalAccountDeletion>();
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test test/PSPad.App.Tests --filter "FullyQualifiedName~LocalAccountDeletionTests" -v`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add src/PSPad.App/Auth/LocalAccountDeletion.cs src/PSPad.App/Program.cs test/PSPad.App.Tests/Auth/LocalAccountDeletionTests.cs
git commit -m "feat: LocalAccountDeletion wipes replica, outbox and session (#23)"
```

---

## Task 11: Danger zone — `DeleteAccountDialog` wired into `SettingsPage`

No standalone dialog component test exists anywhere in this codebase — every dialog (`NameDialog`, `ConfirmDialog`, `AddTaskDialog`, `AreaPickerDialog`) is tested through the page that opens it, via a render fragment that puts `MudPopoverProvider` + `MudDialogProvider` + the page in one tree (see `AreaBoardTests.BuildAreaBoardWithDialogs`). This task follows that exact, proven pattern rather than rendering the dialog in isolation.

**Files:**
- Create: `src/PSPad.App/Components/DeleteAccountDialog.razor`
- Modify: `src/PSPad.App/Pages/SettingsPage.razor`
- Test: `test/PSPad.App.Tests/Pages/SettingsPageTests.cs`

**Interfaces:**
- Consumes: `PSPadApiClient.DeleteAccountAsync()` (Task 9), `LocalAccountDeletion` (Task 10), `IDialogService` (`@inject IDialogService Dialogs`, already available via `AddMudServices()` in `AppTestHost`).
- Produces: `DeleteAccountDialog` component with `[Parameter] string Email`, closing with `DialogResult.Ok(true)` on success — nothing later consumes this; it is the plan's final integration point.

- [ ] **Step 1: Write the failing tests**

Add to `test/PSPad.App.Tests/Pages/SettingsPageTests.cs`:

```csharp
    [Fact]
    public void TheDangerZoneCardOffersAccountDeletion()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render<SettingsPage>();

        Assert.NotEmpty(page.FindAll(".pspad-delete-account"));
        Assert.False(page.Find(".pspad-delete-account").HasAttribute("disabled"));
    }

    [Fact]
    public void TheDeleteAccountButtonIsDisabledWhenOffline()
    {
        Arrange(displayName: "Ada", email: "ada@example.com", online: false);

        var page = Render<SettingsPage>();

        Assert.True(page.Find(".pspad-delete-account").HasAttribute("disabled"));
    }

    [Fact]
    public void TheDeleteConfirmButtonStaysDisabledUntilTheEmailMatches()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");

        var page = Render(BuildSettingsPageWithDialogs());
        page.Find(".pspad-delete-account").Click();

        Assert.True(page.Find(".pspad-confirm-delete").HasAttribute("disabled"));

        page.Find(".pspad-confirm-email input").Input("someone-else@example.com");
        Assert.True(page.Find(".pspad-confirm-delete").HasAttribute("disabled"));

        page.Find(".pspad-confirm-email input").Input("ADA@EXAMPLE.COM");
        Assert.False(page.Find(".pspad-confirm-delete").HasAttribute("disabled"));
    }

    [Fact]
    public async Task ConfirmingDeletesTheAccountClearsLocalStateAndReturnsToWelcome()
    {
        Arrange(displayName: "Ada", email: "ada@example.com");
        var navigation = Services.GetRequiredService<BunitNavigationManager>();

        var page = Render(BuildSettingsPageWithDialogs());
        page.Find(".pspad-delete-account").Click();
        page.Find(".pspad-confirm-email input").Input("ada@example.com");
        await page.InvokeAsync(() => page.Find(".pspad-confirm-delete").Click());

        Assert.Null(_sessions!.Current);
        Assert.Null(await _replica!.OwnerAsync());
        Assert.Equal("http://localhost/welcome", navigation.Uri);
    }

    [Fact]
    public void AFailedDeleteShowsAnErrorAndKeepsTheDialogOpen()
    {
        Arrange(displayName: "Ada", email: "ada@example.com", accountDeleteFails: true);

        var page = Render(BuildSettingsPageWithDialogs());
        page.Find(".pspad-delete-account").Click();
        page.Find(".pspad-confirm-email input").Input("ada@example.com");
        page.Find(".pspad-confirm-delete").Click();

        Assert.Contains("Something went wrong", page.Markup);
        Assert.NotEmpty(page.FindAll(".pspad-confirm-delete"));
    }

    RenderFragment BuildSettingsPageWithDialogs() => builder =>
    {
        builder.OpenComponent<MudPopoverProvider>(0);
        builder.CloseComponent();
        builder.OpenComponent<MudDialogProvider>(1);
        builder.CloseComponent();
        builder.OpenComponent<SettingsPage>(2);
        builder.CloseComponent();
    };
```

Update the `Arrange` method's signature to add an `accountDeleteFails` parameter, register `LocalAccountDeletion`, and let the stub HTTP handler answer `DELETE api/account` as well as `PUT api/me/timezone`:

```csharp
    AppState Arrange(
        string displayName, string email, bool respondWithNullTimeZone = false, bool online = true,
        bool accountDeleteFails = false)
    {
        _replica = AppTestHost.Arrange(this, User, Today);
        Services.AddSingleton<IConnectivity>(new FixedConnectivity(online));
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Keycloak:Authority"] = "http://localhost:8080/realms/psplace",
                ["Keycloak:ClientId"] = "pspad-frontend"
            })
            .Build());
        _sessions = new InMemoryLocalSessionStore(
            new LocalSession(User, displayName, email, "UTC", "refresh-token", DateTimeOffset.UtcNow));
        _authProvider = new LocalAuthenticationStateProvider(_sessions);
        Services.AddSingleton<ILocalSessionStore>(_sessions);
        Services.AddSingleton(_authProvider);
        Services.AddSingleton(new LocalSignOut(_sessions, _replica, _authProvider));
        Services.AddSingleton(new LocalAccountDeletion(
            _sessions, _replica, Services.GetRequiredService<IOutbox>(), _authProvider));

        var state = new AppState
        {
            UserId = User,
            Today = Today,
            DisplayName = displayName,
            Email = email
        };
        Services.AddSingleton(state);

        Services.AddSingleton(new PSPadApiClient(
            new HttpClient(new StubApiHandler(User, displayName, email, respondWithNullTimeZone, accountDeleteFails))
        {
            BaseAddress = new Uri("http://localhost/")
        }));

        return state;
    }
```

Rename the existing `EchoTimeZoneHandler` class to `StubApiHandler` and extend it to also answer `DELETE api/account`:

```csharp
    sealed class StubApiHandler(
        Guid userId, string displayName, string email, bool respondWithNullTimeZone, bool accountDeleteFails)
        : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Delete)
            {
                return accountDeleteFails
                    ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    : new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""{"keycloakRemoved":true}""")
                    };
            }

            var body = await request.Content!.ReadFromJsonAsync<SetTimeZoneRequest>(cancellationToken);
            var timeZone = respondWithNullTimeZone ? null! : body!.TimeZone;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new MeResponse(userId, displayName, email, timeZone))
            };
        }
    }
```

Add `using PSPad.App.Components;` and `using Microsoft.AspNetCore.Components.RenderTree;` (for `RenderTreeBuilder`, used by `BuildSettingsPageWithDialogs`'s `RenderFragment`) to the top of `SettingsPageTests.cs` — `using PSPad.App.Auth;` is already present via `LocalSignOut`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test test/PSPad.App.Tests --filter "FullyQualifiedName~SettingsPageTests" -v`
Expected: FAIL to compile — `DeleteAccountDialog`, `.pspad-delete-account`, `.pspad-confirm-*`, `LocalAccountDeletion` registration and `StubApiHandler` do not exist yet.

- [ ] **Step 3: Write minimal implementation**

Create `src/PSPad.App/Components/DeleteAccountDialog.razor`:

```razor
<MudDialog>
    <DialogContent>
        <MudText Typo="Typo.body1" Class="mb-3">
            This permanently deletes your account and everything in it. Type
            <strong>@Email</strong> to confirm.
        </MudText>
        <MudTextField T="string" @bind-Value="_confirmation" Label="Email" Immediate="true"
                      AutoFocus="true" Disabled="@_deleting" Class="pspad-confirm-email" />
        @if (_error)
        {
            <MudText Typo="Typo.caption" Color="Color.Error" Class="mt-2">
                Something went wrong. Your data may already be gone — try again, or contact your administrator.
            </MudText>
        }
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@Cancel" Disabled="@_deleting">Cancel</MudButton>
        <MudButton Class="pspad-confirm-delete" Color="Color.Error" Variant="Variant.Filled"
                   OnClick="@DeleteAsync" Disabled="@(!Confirmed || _deleting)">
            @(_deleting ? "Deleting…" : "Delete account")
        </MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] IMudDialogInstance Dialog { get; set; } = null!;

    [Parameter] public string Email { get; set; } = "";

    [Inject] PSPad.App.Api.PSPadApiClient Api { get; set; } = null!;

    string _confirmation = "";
    bool _deleting;
    bool _error;

    bool Confirmed => string.Equals(_confirmation.Trim(), Email, StringComparison.OrdinalIgnoreCase);

    async Task DeleteAsync()
    {
        _deleting = true;
        _error = false;

        var response = await Api.DeleteAccountAsync();

        if (response is null)
        {
            _deleting = false;
            _error = true;
            return;
        }

        Dialog.Close(DialogResult.Ok(true));
    }

    void Cancel() => Dialog.Cancel();
}
```

Add to `SettingsPage.razor`'s usings:

```razor
@using PSPad.App.Components
```

Add a fifth card after the Sync `MudItem` from Task 3, inside the `MudGrid`:

```razor
    <MudItem xs="12" sm="6" md="4" xl="3">
        <MudPaper Outlined="true" Class="pa-4">
            <MudText Typo="Typo.subtitle2" Color="Color.Error" Class="mb-3">Danger zone</MudText>
            <MudText Typo="Typo.body2" Class="mb-3">
                Permanently deletes your account and everything in it.
            </MudText>
            <MudButton Class="pspad-delete-account" Color="Color.Error" Variant="Variant.Outlined"
                       Disabled="@(!Connectivity.IsOnline)" OnClick="@OpenDeleteAccountDialogAsync">
                Delete account
            </MudButton>
            @if (!Connectivity.IsOnline)
            {
                <MudText Typo="Typo.caption" Class="mt-2">Requires a connection.</MudText>
            }
        </MudPaper>
    </MudItem>
```

Inject the new dependencies at the top of the file, alongside the existing `@inject` lines:

```razor
@inject IDialogService Dialogs
@inject LocalAccountDeletion AccountDeletion
```

Add to `@code`:

```csharp
    async Task OpenDeleteAccountDialogAsync()
    {
        var parameters = new DialogParameters<DeleteAccountDialog> { { dialog => dialog.Email, State.Email } };
        var dialog = await Dialogs.ShowAsync<DeleteAccountDialog>("Delete account", parameters);
        var result = await dialog.Result;

        if (result is null || result.Canceled)
        {
            return;
        }

        await AccountDeletion.ClearAsync();
        Navigation.NavigateTo("welcome");
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test test/PSPad.App.Tests --filter "FullyQualifiedName~SettingsPageTests" -v`
Expected: all PASS

Run the full `PSPad.App.Tests` suite to confirm no regressions across the whole settings feature:

Run: `dotnet test test/PSPad.App.Tests -v`
Expected: all PASS

- [ ] **Step 5: Commit**

```bash
git add src/PSPad.App/Components/DeleteAccountDialog.razor src/PSPad.App/Pages/SettingsPage.razor test/PSPad.App.Tests/Pages/SettingsPageTests.cs
git commit -m "feat: Danger zone card with type-to-confirm account deletion (#23)"
```

---

## Final verification

- [ ] **Run the entire suite**

Run: `dotnet test --filter Category=Unit`
Expected: all PASS, a few seconds.

Run: `dotnet test --filter Category=Integration`
Expected: all PASS (Docker must be running — Testcontainers starts its own MongoDB).

Run: `dotnet test`
Expected: all PASS.

- [ ] **Manually verify in the running app** (optional but recommended before closing #23): use the `run` skill to launch the compose stack, sign in, and confirm — the theme list, the searchable time zone, the grid at a wide viewport, sign-out inside the Account card, and a full delete-account round trip against a real Keycloak.

- [ ] **Update AGENTS.md §8** once this branch is merged, adding `adr/0033` to the list of ADRs built on the branch, per AGENTS.md's own "any new or changed architectural decision gets an ADR, immediately" and "§5/ADR set never drift apart" rules. Not done as part of this plan's tasks because §8 describes what is already merged on `main`, not in-flight work.

- [ ] **Update `docs/src/pages/features.astro`** (per AGENTS.md "Docs ship with the change") to mention account deletion as a capability, since it is new user-facing scope. Check the file's existing structure before editing — out of scope for this plan's file list since it wasn't explored during brainstorming; do this as a small follow-up edit before or alongside the PR that merges this branch.
