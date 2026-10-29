# Behaviour tests — design

Status: done 2026-10-29 — unit 2479, integration 413, e2e scenarios 34.

Bugs found during the migration (each a skipped `BUG:` step, reported in PR #225):

- 01-first-sign-in: a first sign-in while another user saves a change can fail and land back on Welcome.
- 01-first-sign-in: signing in from Welcome ignores the return address.
- 01-first-sign-in: coming back from the fresh sign-in does not reopen the delete dialog.
- 01-first-sign-in: deleting the account signs out before it leaves Settings, so Welcome keeps Settings as the return address.
- 02-capture-and-triage: a deadline set on a draft step is lost when an Inbox item is converted.
- 15-search-statistics-settings: an old History address lands on an empty Statistics page.
- 19-step-rename: an edit made just before a sync pull is undone.
- 24-new-task-draft: a deadline set on a draft step is lost when the task is added.
- 25-area-board-cards: ticking a repeat on a day it skips shows it ticked until a reload, though nothing was saved.
- 27-reference-items: labels drafted on a new reference item are missing when it is opened, until a reload, though the server stored them.
- 30-reference-rows: a reference card counts its items as "open tasks".

Tests assert what the code does, never how it looks or how it is wired.
Decision record: ADR-0079. Standing rules after the migration live in
`specs/testing-spec.md`; this file describes the migration.

## 1. Problem

On 2026-10-28 the suite held about 3,000 unit test methods (2,100 in
`PSPad.App.Tests`, 980 across the module test projects), 380 API tests
(mostly integration) and 22 Playwright scenarios. Refactors that keep
behaviour break many tests, because:

- bUnit tests assert CSS classes, DOM structure and markup (about 1,670 such
  assertions in 91 files);
- module handler tests run against hand-written `FakeDocumentStore`,
  `FakeUnitOfWork` and in-memory stores, so they pin the wiring between a
  handler and its ports rather than the result a user or client sees.

Goal: remove every test bound to shape or wiring, and keep behaviour covered
at the cheapest tier that can observe it.

Not a goal: raising coverage, changing production behaviour, or replacing
pure rule tests that already assert behaviour.

## 2. The rule

A test asserts an observable result: a returned value, a decision (events or
a rejection), a stored document, an HTTP response, a sync delta, or what a
user sees and can do in the browser. It never asserts a CSS class, a DOM
shape, a MudBlazor internal, or a call recorded on a fake of one of our own
ports.

| Tier | Subject | Runs |
|---|---|---|
| Unit `[UnitTest]` | Pure rules: domain `Decide` → events or rejection; Today, recurrence, lead time, money values and readings, list view rules, positions; CSV parsing and import planning; client queries and text rules; offline outbox, replica and sync folding; logic-bearing components (§4.2); architecture, category, resource, localization and stylesheet guards | in process, no Docker |
| Integration `[IntegrationTest]` | A command or query through HTTP on `ApiFactory` with Testcontainers Mongo, asserting the response, documents, events and sync deltas: every handler, cascade, projection, reader, sharing and auth flow | Testcontainers |
| End-to-end | A user's flow in Chromium against the compose stack: pages, layout, navigation, panels, localization, offline and live updates; new steps locate by role, label or text | compose stack |

A new test starts at the tier its subject names: a rule with edge cases is a
unit test, a server effect is an integration test, anything a user sees on a
page is an end-to-end step. TDD is unchanged: the failing test comes first,
and for UI work it is an end-to-end step.

Stubs in unit tests are allowed only at external boundaries (HTTP to
Keycloak or NBP, the JS runtime, the clock, connectivity) and only to assert
the client's resulting behaviour, never the outgoing request's shape.

## 3. Backend

### 3.1 Stays unit

Aggregate decisions (`TodoTask`, `Goal`, `Area`, `Inbox`, `TaskList`,
`ReferenceItem`, `Budget`, `MoneyEntry`, `User`, access and sharing rules,
content limits), the Today and day plan rules, recurrence, lead time, list
view rules (`ListFilter`, `ListSort`, `ListCalendar`), `Positions`,
`Arranged`, money values and readings (`Money`, `Currencies`, `YearMonth`,
`MonthTotals`, `BalanceTotals`, `Insights`, `MonthPace`, `RateSuggestion`,
`UsualMonth`, `MoneySummary`), CSV reading and import plans and previews,
provisioning plans, snapshot builders that are pure functions, and the
architecture and category guards.

A test in this group that reaches its subject through a fake store is
rewritten to call the pure function or `Decide` directly; if it cannot be,
it moves to integration.

### 3.2 Moves to integration

- Handler tests: `BudgetHandler`, `CategoryHandler`, `MoneyEntryHandler`,
  `BalanceHandler`, `CategoryCascade`, `ReferenceHandler`, `DeleteCascade`,
  `ReorderListsHandler`, `PlaceListHandler`, and any other test that drives a
  handler through `FakeDocumentStore` or `FakeUnitOfWork`. They become
  `POST /api/commands` followed by assertions on the command result, the
  stored documents or `/api/sync`.
- Statistics projections, replay and readers (on `InMemoryStatisticsStore`)
  become command → `/api/statistics/*` assertions.
- Sharing snapshot marking and publishing (on in-memory stores) become
  command → public snapshot endpoint assertions.
- API plumbing unit tests (`CommandDispatcher*`, `ConflictRetry`,
  `DomainEventPump`, `ChannelDomainEventDispatcher`, `Live*`) move only where
  an HTTP or Mongo test can observe the same behaviour; the rest are deleted.

All new integration tests live in `PSPad.Api.Tests`, grouped by the
aggregate's operation folder as today, and go through `ApiFactory`.

### 3.3 Deleted

Tests whose only subject is a type's shape (`CollectionName`,
`CommandResult`, SSE framing details, `EnrichedEvent` field lists) after the
ledger (§6) confirms no behaviour is lost. The module `Fakes/` folders and
`InMemory*` stores are deleted once nothing uses them.

## 4. Frontend

### 4.1 Client logic stays unit

`State/` queries and text rules, `State/Dispatch`, `State/Outbox`,
`State/Replica`, `State/Search`, `Sync/`, `Live/` stream reading, `Auth/`
session and token handling, `Localization/` plural rules and guards,
`Theme/` preference and stylesheet guards, `Updates/`, `Markdown/`
rendering. Each test is kept when it asserts a result; a test that asserts an
outgoing request's URL, header or body shape is deleted, unless that request
is the contract with an external system (Keycloak end-session, NBP).

### 4.2 Components: logic only

bUnit stays only for components with their own decision logic: parsing,
date or time math, state transitions, keyboard handling. Starting list:
`RecurrenceEditor`, `DueDateMenu`, `DueDatePicker`, `DueDateRow`, `TimeRow`,
`LeadTimeRow`, `ListMonthView`, `WeekStrip`, `MonthStrip`, `DayTimeline`,
`StepList`, `MarkdownField`, `ImportEntriesTable`, `ReferenceFieldList`,
`ReferenceFieldValue`, `SnapshotPublisher`, `AddRow`. Kept tests are
rewritten to parameters in, callbacks and visible text, roles, labels and
`aria-*` out. Pure helpers next to components (`AreaSummary`, `ListIcon`,
`GoalStatusText`, `OpenTaskCount`) are plain unit tests and stay.

Tests of presentational components (`BrandMark`, `BrandLoader`,
`EmptyState`, `SectionHeading`, `PageHeading`, skeletons, `MasonryGrid`,
`ThingCard`, `MoneyHero`, `PanelSection`, `RowGroup`, `QrCode`, charts) are
deleted; what matters about them is covered by the end-to-end step that
shows the screen they sit on.

### 4.3 Pages and layout go to end-to-end

Every bUnit test under `Pages/` and `Layout/` is deleted. The behaviour each
one asserts is mapped in the ledger to an end-to-end step: an existing
scenario's step, a step added to an existing scenario, or a new scenario.
Existing scenarios are extended first; a new scenario is added only for a
journey none covers. A behaviour that is shape only is dropped.

A page holding a rule worth edge-case testing (a computation, not markup)
may have that rule extracted into `State/` as a pure function and unit
tested there. That is the only production change the migration allows.

### 4.4 End-to-end budget

The suite target moves from three minutes to **five minutes per shard** in CI
with four workers; CI runs two shards (`specs/e2e-testing-design.md` §5). A scenario that would push
past it splits. Locators stay role, label or text; no CSS-class locators in
new steps.

## 5. Enforcement

A guard test in `PSPad.App.Tests` (`BehaviourGuardTests`) scans the test
sources and fails on `ClassList`, `ClassName`, `.Markup`, `MarkupMatches`
and CSS-class selectors in `Find`/`FindAll`/`QuerySelector`, so shape
assertions cannot return. A guard in each module test project fails on a
`Fakes/FakeDocumentStore` or `FakeUnitOfWork` reference. Both land in the
last slice, once the sources are clean.

## 6. Migration

Incremental by slice, one pull request per slice, stacked on the previous
slice's branch. Every slice:

1. Writes a ledger `.superpowers/sdd/behaviour-tests/ledger-<slice>.md`
   (uncommitted) listing each test method it touches as **kept** (rewritten
   or untouched, still unit), **moved** (naming the integration test or
   end-to-end step that now covers it) or **dropped** (shape only, with a
   one-line reason).
2. Writes the new integration tests or end-to-end steps and runs them green.
3. Deletes or rewrites the old tests.
4. Runs `dotnet test --filter Category=Unit`, the touched integration
   project, and, for frontend slices, the end-to-end suite.

Slices, in order:

| # | Slice | Scope |
|---|---|---|
| 0 | Rules | ADR-0079, `testing-spec.md`, `e2e-testing-design.md` §5 and §9, `AGENTS.md` index row |
| 1 | Money backend | Money handler and cascade tests → integration; Money fakes removed |
| 2 | Tasks and Presentation backend | Tasks handler, cascade and fake-store tests; Presentation handlers → integration; fakes removed |
| 3 | Statistics and Sharing backend | Projections, replay, readers, snapshot publishing → integration; in-memory stores removed |
| 4 | API plumbing | API unit tests moved, kept or deleted per §3.2 |
| 5 | Client logic | `State/`, `Sync/`, `Live/`, `Auth/`, `Api/`, `Localization/`, `Theme/`, `Updates/`, `Hosting/` audited per §4.1 |
| 6 | Components | §4.2 |
| 7 | My Day and Inbox pages | `Today*`, `Inbox*`, `Welcome`, `SyncRefresh` page tests → end-to-end |
| 8 | Areas, lists, references, goals | `Area*`, `List*`, `Goal*`, reference panels → end-to-end |
| 9 | Money pages | `Budget*`, `Balances`, `MoneyEntries`, import, entry and holding panels → end-to-end |
| 10 | Sharing pages | `Join`, `Snapshot*`, members panels → end-to-end |
| 11 | Shell | `Layout/` shell, navigation, account, settings, search, statistics, app info, authentication, localization page tests → end-to-end |
| 12 | Guards | §5 guards; docs sweep |

## 7. Risks

- **Lost coverage between deletion and replacement.** The ledger and the
  "new tests green before deletion" order guard against it; reviewers read
  the ledger summary in the pull request body.
- **End-to-end runtime and flakiness grow.** Extending scenarios before
  adding new ones and the five-minute budget cap it; flaky steps follow the
  `toPass` retry pattern already used for panels.
- **Edge cases that only bUnit reached** (offline banners, error states).
  They move to a client-logic unit test when the rule can be extracted
  (§4.3), otherwise to an end-to-end step using the existing offline
  harness, otherwise the ledger records them as dropped with the reason.
- **Local end-to-end runs on a two-core box are slow.** CI's `e2e` job is
  the authority for green.
