---
title: Aggregate documents are truth; events are the log beside them
tags: [architecture, persistence, event-sourcing]
date: 2026-09-12
status: Active
---

# ADR-0011: Aggregate documents are truth; events are the log beside them

## Context

Moving off PostgreSQL/Marten (ADR-0002) to MongoDB removed the event store
that state used to be rebuilt from by replay — MongoDB has no equivalent.
Action history still had to come only from events (that requirement did not
change), but something now had to be the source of truth for the current
state of an aggregate, since nothing replays it anymore.

## Decision

Supersedes ADR-0002. Aggregate documents are the source of truth; events
are an append-only log written beside them in the same transaction — never
rebuilt by replay. A command does exactly four things: load the aggregate
document, decide the events, apply them to get the new state, and write the
aggregate, its events and the command id in one MongoDB transaction. Action
history is still exactly this event log — nothing writes a second, parallel
record of what happened.

## Considered alternatives

- **Build a Marten-equivalent event store on MongoDB** — rejected: MongoDB
  has no built-in event-sourcing or projection engine; rebuilding one is a
  bigger project than the GTD core it would support, and the team already
  rejected an equally-sized custom mechanism for CRDTs in ADR-0005 for the
  same reason.
- **Drop the event log, derive history from document diffs** — rejected:
  a diff between two document snapshots cannot reliably reconstruct *why* a
  change happened (which command, which user action) — exactly what the
  history screen needs to show.

## Consequences

Reading current state is now a single document load, not a replay of every
event since creation — cheaper, and it does not get slower as history
grows. The cost is paid knowingly: state is written twice per command (the
document and its events) instead of derived from one source, so a bug that
writes the document but skips the event log produces a document with no
history behind it, and nothing catches that automatically — it has to be
caught by discipline in `IUnitOfWork.CommitAsync` writing both in the same
transaction, always.
