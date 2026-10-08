# PSPad — Agent Guide

Self-hosted multi-user GTD notepad. This file is an index: read the spec or ADR
it points to before working, not before every task. Slice 1 is built.

## 1. What it is

Daily-driver GTD system. One Today screen answers "what do I do now" across all
areas; one shared Inbox captures; offline PWA syncs when back online.
Later: habits, annual plans, integrations, print domain, reminders (`adr/README.md`).

## 2. Invariants (never regress)

- **Today rule:** task is on Today when due today or earlier, or its next
  unchecked step is. "Today" is in the **user's stored time zone**, never
  machine-local. **Recurring tasks are never overdue** — only today's pending
  occurrence shows. Rule lives once in the domain layer, shared client and server.
- Recurrence = template + occurrences, only Done days stored (AD-7).
- Goals are global entities; star means important only, never puts a task on Today.
- `PSPad.Module.Tasks` is WASM-safe: references `PSPad.Abstractions` only.
- Aggregate documents are truth; events are the log beside them. No audit table.
- Commands are the shared contract; same handler runs in WASM and on the server.
- Offline conflicts: last-write-wins per aggregate; rejected commands are surfaced.

## 3. Where to read

| Working on | Read |
|------------|------|
| Command pipeline, storage, domain rules, sync, identity, HTTP, containers, statistics | `specs/backend-spec.md` |
| Client components, layout, pages, theming, navigation | `specs/ui-spec.md` |
| Adding an aggregate, module boundaries | `specs/modules-spec.md` |
| Writing or running tests, fixtures, guards | `specs/testing-spec.md` |
| Sharing, invites, public snapshots | `specs/sharing-spec.md` |
| Reference lists | `specs/reference-lists-design.md` |
| List ordering | `specs/list-ordering-design.md` |
| Recurrence lead time, yearly repeats | `specs/recurrence-lead-time-design.md` |
| Phone navigation | `specs/mobile-navigation-design.md` |
| GitHub integration | `specs/github-integration-design.md` |
| My Day by day, task times (in progress) | `specs/my-day-design.md` |
| Why a decision was made | `adr/README.md` index → the `Active` record |

Spec vs ADR disagree: ADR wins. Spec vs code disagree: say so, follow neither silently.

## 4. Architecture in brief (reasoning in `adr/`)

| AD | Summary | ADR |
|----|---------|-----|
| 1 | Modular monolith: Tasks, Statistics, Identity, Presentation, Sharing | 0010, 0045 |
| 2 | Documents are truth, events are the log | 0011 |
| 3–4 | Commands shared; Tasks compiles to WASM | 0003, 0004 |
| 5–6 | LWW conflicts; delta sync by monotonic `seq` | 0005, 0006 |
| 7 | Recurrence template + occurrences | 0007, 0043, 0053 |
| 8 | Aggregates: User, Area, TaskList, TodoTask, Goal, Inbox, ReferenceItem | 0008, 0047 |
| 9 | Integration tests own a Testcontainers MongoDB 8 replica set | 0009 |
| 10 | Events dispatch after commit, replayed from a marker | 0036, 0037 |
| 11 | Per-user views live in Presentation | 0051 |
| 12 | Lists shared by membership | 0054, 0057 |
| 13 | Public snapshots, server-only module | 0056 |

Stack: .NET 10, MongoDB 8 replica set `rs0`, Blazor WASM PWA + MudBlazor,
IndexedDB replica and outbox, Keycloak OIDC/JWT, Docker Compose, no messaging bus.

## 5. Layout

```
src/PSPad.Api  src/PSPad.App  src/shared/{Abstractions,Contracts,Infrastructure}
src/modules/PSPad.Module.{Tasks,Statistics,Identity,Presentation,Sharing}
test/  brand/  docs/  specs/  adr/  docker/  .superpowers/sdd/ (uncommitted plans)
```

References run one way: `Tasks` sees `Abstractions` only; `Infrastructure` sees no
module; `App` never sees `Infrastructure`; `Api` sees all. Only module edges:
`Statistics`→`Tasks`, `Sharing`→`Tasks`. Detail: `specs/backend-spec.md` §1.

## 6. Rules for agents

- **Design before code.** Brainstorm → spec (`specs/<topic>-design.md`) → plan
  (`.superpowers/sdd/<feature>/`, uncommitted).
- **Read first:** the relevant spec; for architecture, module, persistence, sync
  or contract changes also `adr/README.md` and its `Active` records.
- **ADR immediately** for any new or changed architectural decision, using
  `adr/template.md`, plus its index row; update §4 here if an AD changes. Never
  edit an old ADR's Decision — supersede it.
- **Docs ship with the change:** self-hoster-facing change (compose, env, ports)
  → `docs/src/pages/install.astro` and `docker/.env.example` comments;
  user-facing capability → `features.astro` and the landing page.
- **TDD.** Failing test first; every test class has `[UnitTest]` or
  `[IntegrationTest]` (`specs/testing-spec.md`). Never claim green without running.
- **No comments in code.** Rename or extract instead; one line only for a
  counter-intuitive *why*. XML docs only on cross-project public API.
- **One type per file**, filename = type, grouped by operation:
  `Goals/Achieve/{AchieveGoal,GoalAchieved,AchieveGoalHandler}.cs`; aggregate
  class stays in its folder; namespace is the aggregate's, not the folder's.
- **Prefer MudBlazor components** over custom `pspad-*` markup (`specs/ui-spec.md`).
- **Be terse.** No preamble, no narration of tool calls, report failures with the
  shortest decisive error line.
- Code, comments, commits, docs in English; chat with maintainer may be Polish.
- License GPL v3; dependencies must be compatible.
