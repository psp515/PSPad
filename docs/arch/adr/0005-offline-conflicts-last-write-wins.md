---
title: Resolve offline conflicts last-write-wins per aggregate
tags: [architecture, offline, sync]
date: 2026-09-11
status: Active
---

# ADR-0005: Resolve offline conflicts last-write-wins per aggregate

## Context

Offline edits (ADR-0003) can conflict with server-side changes made while a
device was disconnected — the same task edited on a phone with no signal
and, separately, on a desktop. PSPad is a single-owner GTD tool, not a
collaborative document editor, so genuine concurrent edits by different
people to the same aggregate essentially never happen; the realistic case
is one person's two devices.

## Decision

Offline conflicts resolve last-write-wins per aggregate. A command that
loses is rejected, and rejections are always surfaced to the user — never
dropped silently.

## Considered alternatives

- **CRDTs / operational transforms** — rejected: a merge engine capable of
  reconciling arbitrary concurrent edits costs more to build and reason
  about than the entire GTD core it would be protecting, for a conflict rate
  that is effectively zero given single ownership.
- **Reject all conflicting commands and force manual re-entry** — rejected:
  too heavy for what is usually just a stale client catching up; last-write-
  wins with a visible rejection covers the rare genuine conflict without
  punishing the common stale-replica case.

## Consequences

Sync stays simple: no merge logic, no vector clocks. The cost is that a
genuine conflict silently picks a winner from the system's point of view —
the loser's user must be told and given the chance to redo their change,
which puts real weight on "rejections are always surfaced," not treated as
an edge case to skip.
