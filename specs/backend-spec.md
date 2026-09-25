# Backend Spec

Standing rules for the command pipeline, storage, sync, identity and HTTP
surface — everything below the UI. This is a rulebook, not a history: it
states what the system does now. For *why*, the superseded design
narratives (`slice-design.md`, `offline-first-session-design.md`) are in
git history (`git log -- specs/`); the ADRs they produced stay in `adr/`
and remain the decision record.

Where this spec and an ADR disagree, the ADR wins. Where this spec and the
code disagree, say so rather than silently following either. AGENTS.md §5
(AD-1…AD-9) is the terse architectural summary this spec expands on —
read that first for the "why one modular monolith", this for the mechanics.

---

## 1. Project layout & references

```
PSPad.slnx
src/
  PSPad.Api/                   Minimal API, endpoints, DI composition, Dockerfile
  PSPad.App/                   Blazor WASM PWA, MudBlazor, IndexedDB replica + outbox, Dockerfile (nginx)
  shared/
    PSPad.Abstractions/        ICommandHandler<T>, IDocumentStore<T>, IUnitOfWork, IClock, Aggregate
    PSPad.Contracts/            wire shapes: command envelope, sync DTOs, history DTOs
    PSPad.Infrastructure/       Mongo client, connection string, generic repository, Keycloak/JWT, DI registration
  modules/
    PSPad.Module.Tasks/         areas, lists, Inbox, tasks, steps, goals, recurrence, Today — pure, WASM-safe
    PSPad.Module.History/       queries over the event log
    PSPad.Module.Identity/      User, time zone, first-sign-in provisioning
test/
  PSPad.Module.Tasks.Tests/     unit only
  PSPad.Module.History.Tests/   unit only
  PSPad.Module.Identity.Tests/  unit only
  PSPad.Api.Tests/              integration, Testcontainers MongoDB
  PSPad.App.Tests/              unit + bUnit
  PSPad.TestInfrastructure/     Mongo fixture, category attributes, architecture guards
docker/                         compose files, Keycloak realm, nginx config
```

References run one way only:

| Project | May reference |
|---|---|
| `PSPad.Abstractions` | nothing |
| `PSPad.Contracts` | `PSPad.Abstractions` |
| `PSPad.Module.Tasks` | `PSPad.Abstractions` — and nothing else. This is the purity rule (AD-4) |
| `PSPad.Module.History` | `PSPad.Abstractions`, `PSPad.Contracts` |
| `PSPad.Module.Identity` | `PSPad.Abstractions`, `PSPad.Contracts` |
| `PSPad.Infrastructure` | `PSPad.Abstractions`, `PSPad.Contracts` — never a module |
| `PSPad.Api` | everything |
| `PSPad.App` | `PSPad.Module.Tasks`, `PSPad.Abstractions`, `PSPad.Contracts` — never `PSPad.Infrastructure` |

`PSPad.Infrastructure` stores documents generically by `T`, so it never
needs to know a module exists. `PSPad.App` references `PSPad.Module.Tasks`
because the same command handler runs in the browser against the IndexedDB
replica and on the server against MongoDB (AD-3) — the only reason offline
edits and server state agree.

Enforced by architecture guard tests, not just this document: no
`MongoDB.*`/`Microsoft.AspNetCore.*`/`System.Net.Http` inside
`PSPad.Module.Tasks` or `PSPad.Abstractions`; no module reference inside
`PSPad.Infrastructure`.

---

## 2. Command pipeline

One shape, both sides of the wire:

```csharp
public interface IAggregate { Guid Id { get; } Guid UserId { get; } int Version { get; } }
public interface ICommand { Guid CommandId { get; } Guid UserId { get; } }
public abstract record DomainEvent(Guid AggregateId, Guid UserId, DateTimeOffset At);
public interface ICommandHandler<in TCommand> where TCommand : ICommand
{
    Task<CommandResult> HandleAsync(TCommand command, CancellationToken ct);
}
public sealed record CommandResult(bool Accepted, string? Rejection = null)
{
    public static CommandResult Ok() => new(true);
    public static CommandResult Rejected(string reason) => new(false, reason);
}
```

Aggregates keep rules pure and testable with no store: `Decide(command)`
returns the events a command produces, or throws `DomainRejectedException`;
`Apply(event)` folds one event into state.

A handler does exactly four things — load, decide, apply, stage — and
never names a database type:

```csharp
public interface IDocumentStore<T> where T : Aggregate
{
    Task<T?> LoadAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<T>> LoadAllAsync(Guid userId, CancellationToken ct);
}
public interface IUnitOfWork
{
    void Stage(Aggregate aggregate, IReadOnlyList<DomainEvent> events);
    Task CommitAsync(Guid commandId, Guid userId, CancellationToken ct);
}
```

Writing is separate from reading because one command can touch two
aggregates (organising an inbox item creates a task and empties the item),
and both must land together or not at all. On the server, `CommitAsync`
persists every staged aggregate, appends their events, bumps the sync
sequence and records the command id **in one MongoDB transaction**. In the
browser, the same call writes the IndexedDB replica and appends to the
outbox.

`Aggregate` carries one settable `long Seq`, stamped with the sequence of
the last event that touched it — data about the write, never the domain,
and no `Decide` may read it.

**Idempotency.** Every command carries a client-generated `CommandId`. The
server records processed ids in `processed_commands` inside the same
transaction as the write. A replayed command (the outbox shipping twice
after a flaky reconnect) finds its id present and returns the earlier
result without writing.

---

## 3. Storage

**MongoDB 8, single-node replica set `rs0`.** Not optional — multi-document
transactions require one. Dev, prod and tests all run the same shape.

| Collection | Holds | Notable fields |
|---|---|---|
| `users` | one per person | `timeZone` (IANA), `provisionedAt` |
| `areas` | user-defined areas | `name`, `position` |
| `lists` | task lists, each inside one area | `areaId`, `name`, `position` |
| `inboxes` | one per user | `items[]` |
| `tasks` | tasks with steps inline | `listId`, `dueOn`, `goalId`, `priority`, `starred`, `steps[]`, `recurrence`, `completedDays[]`, `createdAt` |
| `goals` | global goals | `name`, `achieved`, `notAchieved`, `dueOn` |
| `events` | action history and the sync feed | `seq`, `userId`, `aggregateType`, `aggregateId`, `type`, `payload`, `at` |
| `processed_commands` | idempotency keys | `_id` = command id, `at` |
| `counters` | the global sequence | `_id: "events"`, `value` |

Every aggregate document carries `_id` (GUID), `userId`, `version`
(optimistic concurrency), `seq` (sequence of the last touching event) and
`deleted` (soft delete, so a removal travels through sync).

**The sequence.** `counters` holds one document. Each write runs
`findOneAndUpdate({_id: "events"}, {$inc: {value: 1}})` inside the
transaction; the returned value stamps both the event and the aggregate's
`seq`. Monotonic, safe as a resume marker — a clock is not.

**Indexes.**

- `events`: `{userId: 1, seq: 1}`, `{userId: 1, at: -1}` (history screen)
- every aggregate collection: `{userId: 1, seq: 1}` (delta sync)
- `tasks`: `{userId: 1, listId: 1}`, `{userId: 1, dueOn: 1}`
- `lists`: `{userId: 1, areaId: 1}`
- `processed_commands`: TTL index on `at`, 30 days

---

## 4. Domain rules

Authoritative statement lives in AGENTS.md §3 — Today rule and
never-overdue recurrence. This section only adds implementation mechanics
not stated there:

- The Today rule takes the instant and the time zone as explicit
  arguments, so it is deterministic under test — never reads a clock or
  `TimeZoneInfo.Local` internally.
- Recurrence stores a rule plus `completedDays`; Pending/Skipped are
  **derived**, never stored.
- The rule is tested in three places on purpose: the module (unit), the
  API's Today query (integration), and the client projection (bUnit). A
  change that breaks it should turn three suites red, not one.
- Ordering (lists, steps, inbox items) uses a dense `position` integer.
  Reordering rewrites the affected range rather than using fractional
  keys — ranges are short, and the rewrite touches one document.
- `TodoTask.CreatedAt` is set in `When(TaskCreated)` from the event's `At`.
  `BurndownRule` (`PSPad.Module.Tasks/Analytics/`) is the pure, WASM-safe
  engine computing the burndown chart client-side: `open(d)` counts tasks
  created on or before `d`, not yet completed by `d`, not deleted;
  `completed(d)` counts tasks and recurrence occurrences completed on `d`;
  days bucket in the **user's time zone**; recurring templates are
  excluded from the open line (they never close, so they'd sit on it as a
  permanent flat offset — same shape of argument as never-overdue).
- A goal's status is `InProgress`, `Achieved` or `NotAchieved`, set by
  `SetGoalStatus`. It is derived from two stored flags, `achieved` and
  `notAchieved`, never stored as its own field. Documents written before
  statuses existed carry only `achieved`, so they read correctly with no
  backfill. `AchieveGoal` and `ReopenGoal` still work so that commands
  already queued in an outbox are not rejected; `ReopenGoal` returns any
  closed goal to `InProgress`. `SetGoalDueDate` sets or clears an
  optional `dueOn`.

---

## 5. Sync

`GET /api/sync?since={seq}` returns every aggregate document for the caller
with `seq > since`, the events in that range, and the new marker. The
client overwrites its replica with what it receives — the server is truth,
the replica is disposable (AD-6).

`POST /api/commands` takes a batch of command envelopes from the outbox, in
order, and returns one result per envelope. Rejections surface to the user,
never dropped silently (AD-5). Conflicts resolve last-write-wins per
aggregate; the loser gets a rejection carrying a reason. The outbox ships
in order and stops on the first rejection, so a failed command can't be
overtaken by one that depended on it.

**Every accepted command triggers an immediate sync**, not just the
60-second poll / reconnect / app-start triggers. `CommandSender` calls
`ISyncTrigger.SyncNowAsync()` once a command is accepted — fire-and-forget,
since the local write is already durable. `SyncNowAsync` is the single
guarded entry point every trigger goes through: overlapping calls coalesce
into one in-flight run plus exactly one guaranteed follow-up pass, so a
poll tick landing mid-burst never double-pushes the outbox. `Start()`
always hands back its own fresh pull rather than a possibly-stale
in-flight one, because `AuthorizeRouteView` mounts the shell once
signed-out and once signed-in and both must each see their own data land.

A sync revision cascades from `AppShell` so every replica-backed screen
redraws when new data lands; the shell waits for the first pull to
complete before rendering content on a device holding nothing yet for this
user, rather than flashing an empty state.

---

## 6. Identity & session

Keycloak is the only sign-in path. The API validates JWT bearer tokens
against the realm; the client uses authorization code with PKCE. No header
identity seam anywhere — the `sub` claim maps to a `User`, everything else
keys off `userId`. `Program.cs` sets `MapInboundClaims = false` so claim
names read as Keycloak sends them (`name`, not `ClaimTypes.Name`).

On first sign-in the API provisions the user: a `User` with the time zone
the client reports (browser IANA zone, editable later), one Inbox, and a
small seed of areas. Provisioning is idempotent, runs in a transaction.
`SetUserDisplayName` heals an already-provisioned user's display name when
the token's name differs from the stored one (`MeEndpoints` issues it on
every `/api/me` call), so a Keycloak profile change picks up on next
sign-in with no migration.

**A durable local session, not the live access token, gates whether the
app opens.** Two different questions were previously conflated: "who is
this device's user" (answerable offline) and "may I call the API right
now" (only answerable online, and only matters at call time).

| | `LocalSession` | Access token |
|---|---|---|
| Answers | Who is this device's user? | May I call the API right now? |
| Storage | IndexedDB | `sessionStorage`, library-managed |
| Lifetime | Until logout or the trust window lapses | Minutes |
| Readable offline | Yes | Irrelevant offline |
| Gates | Opening the app | API calls only |

`LocalSession` holds the user id, display name, email, time zone, the OIDC
refresh token, and `LastServerContactUtc`. A custom
`LocalAuthenticationStateProvider` builds its principal from this record,
not from the library's token store.

**Boot sequence:**

```
index.html paints the brand-mark splash
  ↓
WASM boots — splash held, no chrome rendered
  ↓
read LocalSession from IndexedDB
  ├── none ─────────────────────────► login
  ├── stale (now − LastServerContactUtc > 7 days)
  │     └── clear replica + LocalSession, keep outbox ──► login
  └── fresh
        ├── tear down splash, render app from replica immediately
        └── background, non-blocking: refresh token
              ├── success → store rotated token, update LastServerContactUtc, /api/me, start sync
              ├── transport failure/timeout/5xx → offline indicator, retry on reconnect
              └── 400 invalid_grant → clear LocalSession → login
```

The offline trust window is **7 days**: any successful refresh or sync
resets it, so normal use never expires; a lost device stops serving
readable local data after a week of no contact. On expiry the replica is
cleared (server-derived, disposable) but the **outbox survives** (locally
authored intent that exists nowhere else). A `400 invalid_grant` from the
token endpoint is authoritative revocation and clears the session; any
transport failure is not — it keeps the session and just surfaces offline
state, because conflating the two would sign a user out every time they
open the app in a tunnel.

Token renewal is a direct `POST` to
`{Authority}/protocol/openid-connect/token` with `grant_type=refresh_token`,
on an `HttpClient` with no authorization handler attached — no iframe, no
cross-origin cookie dependency (which mobile browsers block in standalone
PWA mode). The library's own automatic-silent-renew mechanism is never
armed: its `IRemoteAuthenticationService`/`IAccessTokenProvider` is never
resolved outside the interactive `/authentication/*` routes, so two
renewal mechanisms never race against one rotating refresh token. Own
token attachment goes through a `DelegatingHandler`, not the library's
`AuthorizationMessageHandler`.

`/api/me` refreshes identity opportunistically when online — it never
gates readiness. Its failure sets an offline indicator and nothing else.

**Sign-out** ends the Keycloak session directly (not just local state).
Signed-out visitors land on the public `/welcome` screen, never a bare
redirect. The Keycloak realm sets `ssoSessionIdleTimeout` (30 days) and
`ssoSessionMaxLifespan` (90 days) explicitly — the idle timeout must exceed
the 7-day local trust window, or the server invalidates the refresh token
before the client-side window becomes the effective bound.

**Account deletion** (`DELETE /api/account`, see `adr/0034`) is the one
operation in the system that is not an `ICommand`. It can't run offline or
through the outbox — there is nothing left to sync a queued "delete
everything" against — and no aggregate owns "all of a user's data," so it
does not fit the load/decide/apply/stage shape §2 describes. It is handled
directly in `PSPad.Api`:

1. In one MongoDB transaction, enumerate every collection in the database
   (`Database.ListCollectionNames()`) and run
   `DeleteMany({ userId: callerId })` against each — including `events` and
   `processed_commands`. No collection name is hardcoded, so a new aggregate
   added later (a habit, a yearly goal) is covered with no code change here.
2. Only once that transaction commits, call Keycloak's Admin REST API
   (`DELETE /admin/realms/{realm}/users/{sub}`), authenticating with the
   existing bootstrap master-realm admin credentials
   (`KEYCLOAK_ADMIN_USER`/`KEYCLOAK_ADMIN_PASSWORD`) — no new realm client
   or service account. Mongo data is deleted first: if the Keycloak call
   then fails (retried a couple of times inline), the failure mode is an
   orphaned, empty Keycloak login an admin can clean up manually, never
   surviving personal data with nowhere left to request its own deletion.
   The response is `200 { keycloakRemoved: true }` on full success, or still
   `200 { keycloakRemoved: false }` after the retries are exhausted — the
   data is already irreversibly gone either way, so the client's job is to
   report which parts finished, not to fail the request. A failure before
   the Mongo transaction commits is the only case that returns a non-2xx,
   and it means nothing was deleted.
3. There is no local password to check (no local password store exists at
   all — Keycloak is the only sign-in path, §6 above), so the client-side
   confirmation is a typed-email match, not a password prompt. That is a UX
   safeguard against misclicks, not the authorization boundary — the caller's
   own validated JWT `sub` is, and the handler only ever deletes that id.
4. The caller is authenticated but the operation still requires
   connectivity; it does not go through the offline session model in the
   table above.

---

## 7. HTTP surface

| Method | Path | Purpose |
|---|---|---|
| `POST` | `/api/commands` | Execute a batch of commands. The only write endpoint |
| `GET` | `/api/sync?since=` | Delta pull |
| `GET` | `/api/today` | Server-side Today, for a cold client |
| `GET` | `/api/history?before=&limit=` | Action history, newest first |
| `GET` | `/api/me` | Current user; provisions on first call, heals display name |
| `PUT` | `/api/me/timezone` | Set the user's IANA time zone (not through the offline command path — rare, server-owned, online-only) |
| `DELETE` | `/api/account` | Delete the caller's account: every Mongo document scoped to their `userId`, then their Keycloak user. Not a command — see §6 |
| `GET` | `/health` | Liveness, unauthenticated |

Writes go through one endpoint because every write is a command and the
outbox ships them in batches — splitting per feature would buy nothing and
make ordering harder to honour.

---

## 8. Containers

Two images, each with a `Dockerfile` beside its `.csproj`, built with the
repo root as context.

- `src/PSPad.Api/Dockerfile` — ASP.NET runtime, port 8080.
- `src/PSPad.App/Dockerfile` — publishes the WASM output into
  `nginx:alpine`; nginx serves pre-compressed assets, sets the
  `application/wasm` MIME type, falls back to `/index.html` for client
  routes.

The client is static, so its API base URL and Keycloak settings can't be
baked in — the image ships `wwwroot/appsettings.json` as a template plus an
entrypoint substituting environment variables at container start.

`docker/compose.yaml` runs MongoDB and Keycloak for development.
Integration tests never touch it — they start their own MongoDB via
Testcontainers. `docker/compose.prod.yaml` runs api, app, mongo and
keycloak behind a reverse proxy terminating TLS.

---

## 9. Out of scope

Habits, annual plans, integrations, the print domain, reference materials,
push reminders, thought of the day, list types beyond plain — unchanged
from AGENTS.md §3. Server-side analytics endpoints; the burndown chart is
computed client-side only (§4). Mid-session token renewal beyond the
on-demand refresh described in §6. Offline sign-in for a device that has
never signed in — impossible, the first token exchange requires Keycloak.

## 10. Open

Retention for the event log and recurrence occurrences — unbounded, or
archived per year. Deferred until there is enough data to measure.
