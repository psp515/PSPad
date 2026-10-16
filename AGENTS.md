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
- `PSPad.Module.Tasks` and `PSPad.Module.Money` are WASM-safe: reference
  `PSPad.Abstractions` only.
- Money totals are PLN; every amount carries its rate frozen at entry (AD-14).
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
| Budgets, expenses, balances, NBP rates | `specs/money-design.md` |
| Budget CSV import | `specs/budget-import-design.md` |
| Sharing a budget | `specs/budget-sharing-design.md` |
| Budget page views: Overview, Entries, Stats, forecast, insights | `specs/budget-overview-design.md` |
| Length and count limits on user content | `specs/content-limits-design.md` |
| Live updates (sync nudges over SSE, proxies) | `specs/live-updates-design.md` |
| Why a decision was made | `adr/README.md` index → the `Active` record |

Spec vs ADR disagree: ADR wins. Spec vs code disagree: say so, follow neither silently.

## 4. Architecture in brief (reasoning in `adr/`)

| AD | Summary | ADR |
|----|---------|-----|
| 1 | Modular monolith: Tasks, Statistics, Identity, Presentation, Sharing, Money | 0010, 0045, 0059 |
| 2 | Documents are truth, events are the log; snapshot marks are state-only | 0011, 0064 |
| 3–4 | Commands shared; Tasks compiles to WASM | 0003, 0004 |
| 5–6 | LWW conflicts offline, version-checked saves on the server, final rejections dropped; delta sync by monotonic `seq`, paged, documents only | 0005, 0006, 0061, 0063, 0067 |
| 7 | Recurrence template + occurrences | 0007, 0043, 0053 |
| 8 | Aggregates: User, Area, TaskList, TodoTask, Goal, Inbox, ReferenceItem; a goal holds its task links | 0008, 0047, 0065 |
| 9 | Integration tests own a Testcontainers MongoDB 8 replica set | 0009 |
| 10 | Events dispatch after commit, replayed from a marker | 0036, 0037 |
| 11 | Per-user views live in Presentation | 0051 |
| 12 | Lists and budgets shared by membership | 0054, 0057, 0066 |
| 13 | Public snapshots, server-only module | 0056, 0064 |
| 14 | Money: WASM-safe module, budgets as containers, rate frozen per amount | 0059 |
| 15 | Containers non-root on exact-version-tagged images; central package versions; Dependabot weekly | 0071 |
| 16 | Open clients get a data-free sync nudge over SSE, fanned out from commit | 0075 |

Stack: .NET 10, MongoDB 8 replica set `rs0`, Blazor WASM PWA + MudBlazor,
IndexedDB replica and outbox, Keycloak OIDC/JWT, Docker Compose, no messaging bus.

## 5. Layout

```
src/PSPad.Api  src/PSPad.App  src/shared/{Abstractions,Contracts,Infrastructure}
src/modules/PSPad.Module.{Tasks,Statistics,Identity,Presentation,Sharing,Money}
test/  brand/  docs/  specs/  adr/  docker/  .superpowers/sdd/ (uncommitted plans)
```

References run one way: `Tasks` and `Money` see `Abstractions` only; `Infrastructure` sees no
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
