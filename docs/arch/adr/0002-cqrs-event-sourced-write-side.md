---
title: Use CQRS with an event-sourced write side on Marten/PostgreSQL
tags: [architecture, persistence, event-sourcing]
date: 2026-09-11
status: Superseded by ADR-0011
---

# ADR-0002: Use CQRS with an event-sourced write side on Marten/PostgreSQL

## Context

Action history is a first-slice requirement, and slice 1 needed a
persistence model that gives that history for free rather than as a
bolted-on audit table. PostgreSQL plus Marten was the assumed store at this
point in the project.

## Decision

We will use CQRS with an event-sourced write side: commands mutate
aggregates and append events to a Marten event stream; Marten projections
build the read models. Queries never touch aggregates directly. Action
history is the event stream itself — never a parallel audit log.

## Considered alternatives

- **State-stored aggregates with a separate audit table** — rejected at the
  time: a parallel audit log can drift from the actual writes, and Marten's
  projections give history and read models from the same source of truth.
- **No history feature at all** (defer to a later slice) — rejected: GTD
  action history is called out as slice-1 scope, not later scope.

## Consequences

History and read models came from one mechanism, and Marten did the
projection wiring. But it made PostgreSQL — specifically Marten — load
bearing for the entire write path, and state was never directly what was on
disk: it had to be rebuilt by replaying the stream. When the store moved to
MongoDB (no Marten there), that replay mechanism had nothing to run on, and
the project had to decide what "truth" meant without it. See ADR-0011.
