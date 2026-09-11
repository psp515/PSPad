# Slice 1 Implementation Plans

Seven plans covering the GTD core, action history and identity. All of them
implement one spec: `../specs/2026-09-11-gtd-core-design.md`.

Everything runs in Docker, including the .NET SDK. Plan 01 builds that
environment; every later plan assumes you are working inside the dev container it
creates.

| # | Plan | Delivers | Depends on |
|---|------|----------|------------|
| 01 | [Dev environment](2026-09-11-01-dev-environment.md) | Compose stack (Postgres, Keycloak), dev container with the SDK and Docker socket, solution skeleton, category attributes, Testcontainers fixture, purity guard, production image | — |
| 02 | [Domain core](2026-09-11-02-domain-core.md) | Areas, lists, goals, tasks, steps, Inbox as pure decide/apply functions; shared sort key | 01 |
| 03 | [Recurrence & Today](2026-09-11-03-recurrence-and-today.md) | Recurrence rule, occurrences with derived skipped days, the Today rule, occurrence history | 02 |
| 04 | [Server & CQRS](2026-09-11-04-server-cqrs.md) | Marten event store, command routing and idempotent processing, all projections, Today query, action history, HTTP endpoints | 01, 02, 03 |
| 05 | [Identity](2026-09-11-05-identity.md) | User aggregate with time zone, first-sign-in provisioning with seeded areas, JWT bearer, optional Keycloak, removal of the header identity seam | 04 |
| 06 | [Client PWA](2026-09-11-06-client-pwa.md) | Blazor WASM PWA with MudBlazor: Today, Inbox, areas, lists, task detail, goals, history | 04, 05 |
| 07 | [Offline & sync](2026-09-11-07-offline-sync.md) | IndexedDB replica, ordered outbox, delta sync, connectivity handling, end-to-end round-trip proof | 06 |

## Order

Run them in numbered order. Two exceptions worth knowing:

- **02 and 03 are pure domain** — no database, no HTTP. They can be executed by
  someone with nothing running, and they are the best place to start if you want
  to understand the system before touching infrastructure.
- **05 modifies code that 04 wrote** (it deletes the `X-User-Id` seam). Do not
  ship 04 to anything reachable from a network without 05 behind it.

## Test conventions

Every test class carries `[UnitTest]` or `[IntegrationTest]` from
`PSPad.TestInfrastructure`. Integration classes also join
`[Collection(PostgresCollection.Name)]` and get a real Postgres from
Testcontainers — one container per run, never a compose service.

```bash
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
```

Unit suite starts no containers. If it does, something is mislabeled.

## The two rules that keep being load-bearing

1. **A recurring task never becomes overdue.** A missed day stays behind as
   skipped. This is tested in the domain (03), in the server query (04) and in
   the client projection (06) — if a change breaks it, three suites go red.
2. **The domain compiles to WebAssembly.** The same `Decide`/`Apply` runs on the
   server and in the browser, which is the only reason offline edits and server
   state agree. Plan 01's purity guard fails the build if an infrastructure
   reference sneaks in.
