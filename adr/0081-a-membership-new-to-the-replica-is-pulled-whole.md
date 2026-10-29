---
title: A membership new to the replica is pulled whole, and a pull older than a join cannot purge it
tags: [architecture, offline, sync, sharing]
date: 2026-10-29
status: Active (amends ADR-0054, ADR-0066)
---

# ADR-0081: A membership new to the replica is pulled whole, and a pull older than a join cannot purge it

> Status: Active (amends ADR-0054, ADR-0066)

## Context

`SyncService` reconciled membership after saving a pull: a list or budget in
`memberListIds` / `memberBudgetIds` that the replica did not hold was "missing"
and fetched again with `full=`. Joining bumps the container's own `seq`, so the
container itself always arrives in the next delta. By the time reconciliation
ran it was already held, never counted as missing, and its older tasks,
reference items, entries and snapshots (all below the marker) never arrived.
Joining through the API on one device, or on another device of the same user,
left the member with an empty list.

Joining from the join page saves the container and its children from the join
response, outside `SyncCoordinator`. A pull already in flight carries a
membership set the server read before the join, so its reconciliation purged
what the join had just saved, and the next delta again brought only the
container. A typed join lost the list's tasks in 3 of 5 runs during the
behaviour-test migration (PR #225).

## Decision

We will count a container as new by what the replica held **before** the pull
saved anything: every id in the membership set that the replica did not hold
when the pull began is pulled whole with `full=` at the pull's marker. A pull
from zero skips this, since it already carries every document. When that
whole pull fails, the containers it was fetching are removed from the replica
again, so the next sync still sees them as new and retries.

We will also remember each join (`SyncService.AdoptJoinAsync`, used by the
list and budget join paths). A pull that began before a join landed does not
purge the joined container: its membership set cannot speak for it. The first
pull begun after the join reconciles it normally, so a member removed right
after joining still loses the container.

## Considered alternatives

- **Route the join through `SyncCoordinator`, waiting for the in-flight pull
  first** — a pull started by the poll loop or a live-update nudge while the
  join request is out still reads the membership set before the join commits,
  so the race shrinks but stays.
- **Store the last membership set in the replica and diff against it** — a
  new IndexedDB meta key and migration for the same answer the replica's
  own rows already give.
- **Have the server send a joined container's children by delta (bump their
  `seq`)** — a join would rewrite every task of a large list and wake every
  other member's client.

## Consequences

- A newly joined list or budget always reaches the member with its existing
  documents, whichever device or path joined it.
- An owner's device learning of a list shared from another device pulls that
  list whole once: a few extra rows, saved under ADR-0079's version rule.
- The fresh-join memory lives in the running app only. A reload between the
  join and the next pull loses it, which is harmless: the reload's pull begins
  after the join.
- A failed whole pull removes a just-arrived container until the retry
  succeeds, so it can blink out of view on a flaky connection.
