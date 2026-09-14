# Plan 16 — `/api/me` returns a GUID where a name belongs

**Goal.** The account menu shows the user's name, not their Keycloak subject.

**Evidence.** Against the running stack on 2026-09-13, with the demo user signed
in:

```
/api/me 200 {"userId":"0a5e1e4d-842e-547b-375a-c610d8f4e27c","displayName":"d2c79782-1d5c-4617-9ca0-22766d35c77a","timeZone":"Europe/Warsaw"}
```

`displayName` is the Keycloak `sub`. The cause is one line —
`src/PSPad.Api/Endpoints/MeEndpoints.cs:14` passes `current.Subject` twice, once
as the subject and once as the display name:

```csharp
var user = await provisioner.EnsureAsync(
    current.Subject, current.Subject, current.TimeZoneHint, ct);
```

**Architecture.** `ICurrentUser` reads claims; `UserProvisioner.EnsureAsync`
takes the display name and stores it on the `User` aggregate at provisioning
time. The aggregate is written once — `EnsureAsync` returns early when
`ProvisionedAt is not null` — so a name read from claims lands only for users
provisioned after this change. That is a consequence to state, not to design
around: renaming an existing user is a `User` command that does not exist and
is out of this plan's scope.

**Constraints.** Owns `src/PSPad.Api/Identity/CurrentUser.cs`,
`src/PSPad.Api/Endpoints/MeEndpoints.cs`, and
`test/PSPad.Api.Tests/Identity/`. Touches no `PSPad.App` file and no module, so
it cannot conflict with plans 12, 13, 14 or 15. No aggregate change, no new
command, no contract change — `MeResponse` already carries `DisplayName`.

**Interfaces consumed**

```csharp
ICurrentUser { string Subject; Guid UserId; string TimeZoneHint; }
UserProvisioner.EnsureAsync(string subject, string displayName, string timeZone, CancellationToken) -> Task<User>
MeResponse(Guid UserId, string DisplayName, string TimeZone)
```

**Interfaces produced**

```csharp
ICurrentUser { string DisplayName; }   // added
```

**Integration tests, not unit.** This is host and claim wiring, so it runs
through `ApiFactory` and needs Mongo (AGENTS.md §7: any hosted test does).

```bash
dotnet run --project test/PSPad.Api.Tests -- -trait "Category=Integration"
```

---

## Task 1 — A failing test that pins the current behaviour as wrong

**Files**
- create `test/PSPad.Api.Tests/Identity/MeEndpointTests.cs`
- modify `test/PSPad.Api.Tests/TestAuthentication.cs`

The test handler emits only `NameIdentifier` and `zoneinfo`, so it cannot express
a token that carries a name. Add the claims Keycloak actually sends.

- [ ] In `TestAuthenticationHandler.HandleAuthenticateAsync`, after the
      `zoneinfo` block:

```csharp
        if (Request.Headers.TryGetValue("X-Test-Name", out var name))
        {
            claims.Add(new Claim("name", name!));
        }

        if (Request.Headers.TryGetValue("X-Test-Preferred-Username", out var preferred))
        {
            claims.Add(new Claim("preferred_username", preferred!));
        }
```

- [ ] Write `MeEndpointTests.cs`, following `AuthenticationTests`'s arrangement
      of `ApiFactory` and `MongoCollection`:

```csharp
using System.Net.Http.Json;
using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Identity;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class MeEndpointTests(MongoFixture fixture)
{
    [Fact]
    public async Task ItReportsTheNameClaimAsTheDisplayName()
    {
        using var factory = new ApiFactory(fixture);
        using var client = factory.CreateClient();
        var subject = Guid.NewGuid().ToString();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Add("X-Test-Subject", subject);
        request.Headers.Add("X-Test-Name", "Demo User");

        var response = await client.SendAsync(request);
        var me = await response.Content.ReadFromJsonAsync<MeResponse>();

        Assert.Equal("Demo User", me!.DisplayName);
    }

    [Fact]
    public async Task WithoutANameClaimItFallsBackToPreferredUsername()
    {
        using var factory = new ApiFactory(fixture);
        using var client = factory.CreateClient();
        var subject = Guid.NewGuid().ToString();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Add("X-Test-Subject", subject);
        request.Headers.Add("X-Test-Preferred-Username", "demo");

        var response = await client.SendAsync(request);
        var me = await response.Content.ReadFromJsonAsync<MeResponse>();

        Assert.Equal("demo", me!.DisplayName);
    }

    [Fact]
    public async Task WithNeitherClaimItFallsBackToTheSubject()
    {
        using var factory = new ApiFactory(fixture);
        using var client = factory.CreateClient();
        var subject = Guid.NewGuid().ToString();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Add("X-Test-Subject", subject);

        var response = await client.SendAsync(request);
        var me = await response.Content.ReadFromJsonAsync<MeResponse>();

        Assert.Equal(subject, me!.DisplayName);
    }
}
```

Each test uses its own subject, which is the suite's isolation rule (§7: one
`UserId` per test, no collection drops).

- [ ] Run — the first two must fail with the subject GUID in the message, the
      third must pass:

```bash
dotnet run --project test/PSPad.Api.Tests -- -trait "Category=Integration"
```

- [ ] Commit: `test(api): pin the display name /api/me should return`

---

## Task 2 — Read the name off the token

**Files**
- modify `src/PSPad.Api/Identity/CurrentUser.cs`
- modify `src/PSPad.Api/Endpoints/MeEndpoints.cs`

- [ ] Add to `ICurrentUser`:

```csharp
    string DisplayName { get; }
```

- [ ] Implement it on `ClaimsCurrentUser`, next to `TimeZoneHint`:

```csharp
    public string DisplayName =>
        accessor.HttpContext?.User.FindFirstValue("name")
        ?? accessor.HttpContext?.User.FindFirstValue("preferred_username")
        ?? Subject;
```

`ClaimTypes.Name` is not used: ASP.NET maps it from whichever claim the token's
`name` mapping produced, and Keycloak sends `name` and `preferred_username`
verbatim. Reading them by their own names keeps the fallback order explicit.

- [ ] In `MeEndpoints.cs`:

```csharp
            var user = await provisioner.EnsureAsync(
                current.Subject, current.DisplayName, current.TimeZoneHint, ct);
```

- [ ] Run — all three green:

```bash
dotnet run --project test/PSPad.Api.Tests -- -trait "Category=Integration"
```

- [ ] Commit: `fix(api): /api/me reports the name from the token`

---

## Task 3 — The demo user proves it end to end

**Files**
- none

`ICurrentUser` has one other implementation risk: nothing in the App changes, so
the account menu picks the value up for free. Verify against the real stack
rather than trusting that.

- [ ] Bring the stack up and rebuild the API image:

```bash
docker compose -f docker/compose.yaml up -d --build api
```

- [ ] The Keycloak realm's demo user carries `firstName` / `lastName`, so its
      token's `name` claim is `Demo User`. Fetch a token and call `/api/me`.
      Direct access grants are off in the committed realm; enable them on the
      running container only, exactly as on 2026-09-13, and note that it resets
      on `--force-recreate`:

```bash
sh scripts/check-me.sh
```

Write that script in the worktree's scratch area, not the repo — it is a probe,
not a deliverable. It must print `displayName` and assert it is `Demo User`.

- [ ] Record the actual response in the commit body. If `displayName` is still a
      GUID, the token lacks a `name` claim and the realm's mapper is the next
      suspect, not this code.

- [ ] Commit: `test(api): verified /api/me against the demo realm` (docs only, or
      no commit if nothing changed — say which in the report)

---

## Done when

- `/api/me` returns `Demo User` for the demo realm user.
- Three integration facts cover name, preferred_username and subject fallback.
- `dotnet run --project test/PSPad.Api.Tests -- -trait "Category=Integration"`
  green.

## Known consequence, not fixed here

`EnsureAsync` returns early for an already-provisioned user, so users
provisioned before this change keep the GUID as their stored display name. There
is no `RenameUser` command to correct it, and adding one is an aggregate change
outside this plan. For the dev stack, `docker compose down -v` clears it. Raise
a separate plan if real accounts ever need it.
