---
title: Tests assert behaviour, not shape or wiring
tags: [testing]
date: 2026-10-28
status: Active
---

# ADR-0080: Tests assert behaviour, not shape or wiring

> Status: Active

## Context

By October 2026 the suite held about 3,000 unit tests, two thirds of them
bUnit tests of pages, layout and components. About 1,670 assertions checked
CSS classes, DOM structure or markup, and module handler tests ran against
hand-written fake stores. Refactors that kept behaviour broke many tests, so
the suite resisted change instead of protecting it. ADR-0077 had already
added Playwright scenarios that drive the real application, and ADR-0009
gives integration tests a real MongoDB.

## Decision

We will test behaviour at the cheapest tier that can observe it, and never
shape or wiring. Pure rules (domain decisions, Today, recurrence, money,
list views, CSV parsing, client queries, offline sync folding, pure client
rules extracted from components) stay unit tests. Every command handler,
cascade, projection and reader is tested through HTTP on `ApiFactory` with
Testcontainers MongoDB. Pages, layout and components are tested only by
Playwright end-to-end scenarios. No test asserts a CSS class, a DOM shape or
a call on a fake of our own port; guard tests enforce it.
Migration plan: `specs/behaviour-tests-design.md`.

## Considered alternatives

- **Keep bUnit, rewritten to user-facing queries** — rejected: the tier
  drifts back to markup assertions and still duplicates what end-to-end
  scenarios see. A thin bUnit tier for logic-bearing components was kept in
  the first round and removed in the second (2026-10-29): its tests asserted
  through component internals what a scenario sees, and the rules worth an
  edge-case table now live in `State/`.
- **Move domain rules to HTTP tests as well** — rejected: edge-case tables
  (time zones, leap years, currency rates) become slow and awkward through
  HTTP with no gain in confidence.
- **Delete first, backfill later** — rejected: loses the record of what was
  covered and leaves a window for regressions.
- **Apply the rule only to tests a change touches** — rejected: the
  brittleness stays for months and two styles coexist.

## Consequences

- Refactors of markup, styling and handler wiring stop breaking tests.
- The end-to-end suite grows; its CI budget rises from three to five
  minutes, and UI TDD starts with a slower end-to-end step.
- CI runs the end-to-end suite as two shards, each with its own compose stack,
  to stay under five minutes per shard with four workers.
- The integration suite grows and takes longer on every PR.
- Edge cases that only bUnit could reach are either extracted into pure
  client rules or recorded as dropped.
- A thin bUnit tier for logic-bearing components is not a fallback: flakiness is
  fixed in the scenario or the helper, and a rule a scenario cannot reach
  well is extracted into `State/` and unit tested.
- With CSS-text tests gone, a CSS fix is guarded by a browser layout
  assertion (bounding boxes in a scenario), not a pixel snapshot.
