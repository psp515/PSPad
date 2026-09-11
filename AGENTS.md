# PSPad — Agent Guide

A self-hosted, multi-user GTD notepad. This file is the entry point for any agent
working in this repo: what we are building, what is decided, what is next.

**Status: design phase. No code exists yet.** The repo holds only a license,
a Visual Studio `.gitignore`, and empty `src/`, `test/`, `docs/`, `docker/`.

---

## 1. Vision

A daily-driver task system built around GTD practice, not a generic todo app.
One screen answers "what do I do today", pulling across every area of life, while
capture stays frictionless through a single shared Inbox. It runs on my own
hardware, works on a phone with no connection, and syncs when the network returns.

The long-term system also covers habits, yearly goals, connected accounts
(GitHub, OneDrive, Google Drive, Thingiverse), and 3D-printing material/part
lists with reference libraries. Those are **not** in the first slice — see §3.

---

## 2. Subsystem decomposition

The full idea is a platform, not one project. Each subsystem gets its own
spec → plan → implementation cycle.

| # | Subsystem | Slice |
|---|-----------|-------|
| 1 | GTD core — areas, Inbox, lists, tasks, steps, recurrence, goals, Today | **1 (now)** |
| 2 | Action history — event log + browsing screen | **1 (now)** |
| 3 | Identity — built-in login + Keycloak (OIDC) | **1 (now)** |
| 4 | Habits — streaks, daily progress | later |
| 5 | Goals & annual plans — yearly horizon, end-of-year summary | later |
| 6 | Account integrations — GitHub issues, OneDrive, Google Drive, Thingiverse | later |
| 7 | 3D-print domain — material lists, part lists, reference materials | later |
| 8 | Analytics & reminders — charts, push notifications, thought of the day | later |

Subsystems 4 and 5 are cheap follow-ons *because* slice 1 already models
recurrence as occurrences and goals as entities. Do not regress those decisions.

---

## 3. Slice 1 scope (current)

In:

- **Areas** — life, work, studies, projects; user-defined, not hardcoded.
- **Lists** — live inside an area. Plain lists only in this slice.
- **Inbox** — one shared quick-capture list outside all areas. Moving an item
  out of the Inbox into a list is the organizing act, and is a first-class command.
- **Tasks** — belong to exactly one list. Fields: name, due date, goal link,
  priority, star, steps.
- **Steps** — checkable items inside a task, each with its own due date. List
  screens show the task name plus its next unchecked step.
- **Recurrence** — a recurring task is a template plus per-day occurrences.
- **Goals** — separate entities; many tasks may point at one goal.
- **Today screen** — cross-area, see rule below.
- **Action history** — event stream plus a browsing screen with filters.
- **Offline PWA** — full offline read and write via a local replica and a
  command outbox.
- **Auth** — standard app login plus Keycloak via OIDC.

Out of slice 1: habits, annual plans, integrations, print lists, reference
materials, charts, push reminders, thought of the day, list types beyond plain.

### Today screen rule

A task appears on Today when **either**:

- its due date is today or earlier (earlier = overdue, pinned to the top), **or**
- its next unchecked step is due today or earlier.

**Recurring tasks never become overdue.** A missed occurrence stays on its own
day marked *skipped*; only today's occurrence appears on Today. "Read a book"
untouched yesterday must not show as overdue today. This rule lives in the
domain layer, in one place, shared by client and server.

---

## 4. Tech stack

| Layer | Choice | Why |
|-------|--------|-----|
| Runtime | .NET (latest LTS) | Single language across client and server |
| Store | **Marten on PostgreSQL** | JSONB documents *and* event sourcing in one engine; action history falls out of the event stream instead of a bolted-on audit table; ACID; one container |
| Messaging | **Wolverine** | Native Marten integration — a command handler and its event append share one unit of work |
| Frontend | **Blazor WebAssembly, standalone, as a PWA** | Blazor Server is disqualified: it needs a live connection and offline is a hard requirement |
| UI kit | **MudBlazor** | Mature, complete component set (tables, date pickers, dialogs); Material look accepted over shadcn, which is React-only and whose Blazor ports are immature |
| Local store | IndexedDB | Offline replica plus command outbox |
| Identity | ASP.NET Core Identity + OIDC to Keycloak | Self-hosted, multi-user |
| Packaging | Docker Compose | App, PostgreSQL, Keycloak |

---

## 5. Architecture decisions

**AD-1 — Modular monolith, vertical slices.** Features are folders (Areas,
Inbox, Tasks, Goals, Today, History), each holding its commands, handlers,
projections, and endpoints. No horizontal Services/Repositories layering. No
microservices.

**AD-2 — CQRS with an event-sourced write side.** Commands mutate aggregates and
append events; Marten projections build read models. Queries never touch
aggregates. Action history is the event stream — never write a parallel audit log.

**AD-3 — Commands are the shared contract.** Command types live in a project
referenced by *both* the WASM client and the server. The client applies a command
to its local IndexedDB replica immediately (optimistic), records it in an outbox,
and ships it when the network is back. The server replays the same command
through the real aggregate. One model of behavior, not two.

**AD-4 — Domain layer must compile to WASM.** Aggregates and domain rules carry
no dependency on Marten, HTTP, or any infrastructure. This is what makes AD-3
possible, and it is the discipline a domain system wants anyway.

**AD-5 — Offline conflicts resolve last-write-wins per aggregate,** with rejected
commands surfaced to the user rather than dropped silently. Data is
single-owner, so genuine conflicts are rare. CRDTs were considered and
rejected: a merge engine costs more than the whole GTD core.

**AD-6 — Sync is delta-by-version.** The client pulls changes since its last
known version marker and overwrites its replica with server state. The server is
the source of truth; the replica is disposable.

**AD-7 — Recurrence is template plus occurrences,** never a single task with a
rolling date. Occurrence records are what later give habits their streaks and
charts for free.

**AD-8 — Aggregate boundaries:** `Area`, `List`, `Task` (steps live inside the
task aggregate, not on their own), `Goal`, `Inbox` (one per user). A task's goal
link is an id reference across aggregates, not a nested object.

---

## 6. Repo layout (planned)

```
src/
  PSPad.Domain/          aggregates, events, rules — no infrastructure, WASM-safe
  PSPad.Contracts/       commands, DTOs — shared by client and server
  PSPad.Server/          Wolverine handlers, Marten projections, endpoints, vertical slices
  PSPad.Client/          Blazor WASM PWA, MudBlazor, IndexedDB replica + outbox
test/
  PSPad.Domain.Tests/    rule-level tests, no I/O
  PSPad.Server.Tests/    handler + projection tests against a real Postgres
docs/
  superpowers/specs/     design specs, one per subsystem
docker/                  compose: app, postgres, keycloak
```

---

## 7. Current step

Slice 1 is specified and planned. Nothing is implemented yet.

- Spec: `docs/superpowers/specs/2026-09-11-gtd-core-design.md`
- Plans: `docs/superpowers/plans/` — seven of them, see `README.md` there for the
  order and what each delivers.

Execution starts with plan 01 (dev environment) and proceeds in numbered order;
02 and 03 are pure domain and can be worked in parallel with nothing else. Each
plan ends with a green test suite, so a plan is either done or not — there is no
half-landed state to reason about.

Do not skip ahead, and do not add features that no plan covers without taking
them through brainstorming first.

---

## 8. Future steps

Rough order after slice 1 ships:

1. **Habits** — reuse the occurrence model; add streaks and daily progress.
2. **Annual plans** — give `Goal` a yearly horizon and a year-end summary.
3. **Analytics** — charts over completed/in-progress counts and habit progress.
4. **Reminders** — server scheduler plus web push (VAPID); thought of the day.
5. **GitHub integration** — issue-backed lists; the first external sync, so it
   sets the pattern for the rest.
6. **Cloud storage links** — OneDrive and Google Drive paths; reference-material
   lists (no tasks, just linked locations, e.g. finished 3D-print projects).
7. **Print domain** — material lists, part lists, Thingiverse links.
8. **Checklists** — reusable templates convertible to a one-shot quick list.

---

## 9. Settled during design

- **Goals are global**, not scoped to an area — "new eating habit" spans life and
  studies at once.
- **Priority** is a fixed four-level set: none, low, medium, high.
- **The star means "important" only.** It sorts a task up; it never puts a task
  on Today. Today is driven by dates alone.
- **Recurrence entered slice 1** after all: a daily task like "read a book" must
  not show as overdue when yesterday was missed, and that rule cannot be bolted
  on later without changing the model.
- **Everything runs in Docker, including the SDK** — a dev container, not just
  containerized infrastructure.
- **The app is an offline-first PWA**, which ruled out Blazor Server and made
  Blazor WebAssembly the only viable option.

Still open:

- Retention for occurrence records and events — unbounded, or archived per year?

---

## 10. Conventions for agents

- **Design before code.** New features go through brainstorming → spec → plan.
  A spec lives in `docs/superpowers/specs/YYYY-MM-DD-<topic>-design.md`.
- **TDD.** Domain rules get a failing test first. The Today rule and the
  recurrence rule are the two places where bugs will hurt most.
- **Keep the domain pure.** If a change adds an infrastructure dependency inside
  `PSPad.Domain`, it breaks AD-3 and AD-4 — find another way.
- **Never add an audit table.** History comes from events (AD-2).
- **Language:** code, comments, commits, and docs in English. Conversation with
  the maintainer may be in Polish.
- **License:** GPL v3. Keep dependencies compatible.
