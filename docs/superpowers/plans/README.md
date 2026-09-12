# Slice 1 Implementation Plans

Eight plans covering the GTD core, action history and identity. All of them
implement one spec: `../specs/2026-09-12-slice-1-design.md`.

Develop on the host with the .NET 10 SDK. Docker runs the backing services and,
during integration tests, a throwaway MongoDB — so it must be running. Each
deployable gets its own `Dockerfile` beside its `.csproj`: the API's in plan 01,
the client's in plan 07.

| # | Plan | Delivers | Depends on |
|---|------|----------|------------|
| 01 | [Foundation](2026-09-12-01-foundation.md) | Solution skeleton, project references, compose stack (MongoDB `rs0`, Keycloak), category attributes, Testcontainers fixture, architecture guards, API image | — |
| 02 | [Tasks core](2026-09-12-02-tasks-core.md) | Areas, lists, goals, tasks, steps, Inbox as pure decide/apply aggregates; shared position ordering | 01 |
| 03 | [Recurrence & Today](2026-09-12-03-recurrence-and-today.md) | Recurrence rule, derived occurrences, the Today rule | 02 |
| 04 | [API & persistence](2026-09-12-04-api-persistence.md) | Mongo document store, transactional save, global sequence, idempotency, command dispatch, `/api/commands`, `/api/sync`, `/api/today` | 01, 02, 03 |
| 05 | [Identity](2026-09-12-05-identity.md) | User aggregate with time zone, Keycloak JWT bearer, first-sign-in provisioning with seeded areas, `/api/me` | 04 |
| 06 | [History](2026-09-12-06-history.md) | Event log queries, `/api/history` | 04, 05 |
| 07 | [Client PWA](2026-09-12-07-client-pwa.md) | Blazor WASM PWA with MudBlazor, OIDC sign-in, Today, Inbox, areas, lists, task detail, goals, history, nginx image | 04, 05, 06 |
| 08 | [Offline & sync](2026-09-12-08-offline-sync.md) | IndexedDB replica, ordered outbox, delta sync, connectivity handling, end-to-end round-trip proof | 07 |

## Order

Run them in numbered order. Two exceptions worth knowing:

- **02 and 03 are pure domain** — no database, no HTTP. They can be executed by
  someone with nothing running, and they are the best place to start if you want
  to understand the system before touching infrastructure.
- **05 replaces the test-only identity seam that 04 introduces.** Do not ship 04
  to anything reachable from a network without 05 behind it.

## Test conventions

Every test class carries `[UnitTest]` or `[IntegrationTest]` from
`PSPad.TestInfrastructure`. Integration classes also join
`[Collection(MongoCollection.Name)]` and get a real MongoDB from Testcontainers —
one container per run, never a compose service.

```bash
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
```

The unit suite starts no containers. If it does, something is mislabeled.

## The three rules that keep being load-bearing

1. **A recurring task never becomes overdue.** A missed day stays behind as
   skipped. Tested in the domain (03), in the API query (04) and in the client
   projection (07) — if a change breaks it, three suites go red.
2. **The Tasks module compiles to WebAssembly.** The same decide/apply runs on
   the server and in the browser, which is the only reason offline edits and
   server state agree. Plan 01's purity guard fails the build if an
   infrastructure reference sneaks in.
3. **History comes only from the event log.** Nothing writes a second record of
   what happened. A write that skips the log is a bug, not a shortcut.
