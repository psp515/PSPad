---
title: Model recurrence as a template plus derived occurrences, never a rolling date
tags: [architecture, domain, recurrence]
date: 2026-09-11
status: Active
---

# ADR-0007: Model recurrence as a template plus derived occurrences, never a rolling date

## Context

Slice 1 requires that a recurring task never appears overdue: a missed day
should stay behind as skipped, not roll forward and pile up. Slice 1 also
needs to stay cheap to extend into habits and yearly goals later (AGENTS.md
§2), which need a per-day completion record to build streaks and summaries
from.

## Decision

Recurrence is a template plus derived occurrences: only completed days are
stored (`completedDays`); Pending and Skipped states for any other day are
computed from the rule and "today" at read time, never persisted.

## Considered alternatives

- **A single task whose due date rolls forward on completion** — rejected:
  this is the shape that produces the exact "overdue" bug the Today rule is
  built to avoid, since a missed day and a completed day are
  indistinguishable once the date has moved.
- **Persist every future occurrence as its own row (Pending included)** —
  rejected: unbounded storage growth for a value ("this day has not
  happened yet") that is always cheap to derive from the rule.

## Consequences

The never-overdue rule becomes a pure function of the rule and today's date,
testable with no database. Storing only completions also gives habit
streaks (a later subsystem) their data for free — no migration needed when
that slice starts. The cost is that every reader of a recurring task's
status (the Today query, the client projection) must apply the same
derivation logic, so it has to live in one shared place, not be
re-implemented per reader.
