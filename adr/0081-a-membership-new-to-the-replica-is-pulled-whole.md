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

We will treat a list or budget as owed a whole pull when a pull page names it
in the membership set and the replica does not hold it. The owed set is
written to the replica's meta (`owedWhole`) **before** that page is saved, and
an id leaves it only once its whole pull (`full=` at the pull's marker) has
been saved, or once it is no longer in the membership set. Every pull that
reaches reconciliation asks for everything still owed, so a pull interrupted
by a failed later page, a whole pull that times out or fails to parse, or a
reload or suspended PWA between the delta and the whole pull, leaves the debt
for the next pull rather than losing the container's history. A failed whole
pull never purges anything and never stalls the marker. A pull from zero
clears the set, since it carries every document already.

We will also remember each join (`SyncService.AdoptJoinAsync`, used by the
list and budget join paths). A pull that began before a join landed does not
purge the joined container: its membership set cannot speak for it. The first
pull begun after the join reconciles it normally, so a member removed right
after joining still loses the container. Adopting a join, and the pull's
purge and stranded-row pass, run under one lock (`SemaphoreSlim`), so a join
can never land between the reads those passes make; the whole pull runs
outside it. Adopting a join owes the container a whole pull until every
document of the join response is saved, so a reload partway through the
adopt is over-owed rather than left holding a list without its children.

## Considered alternatives

- **Route the join through `SyncCoordinator`, waiting for the in-flight pull
  first** — a pull started by the poll loop or a live-update nudge while the
  join request is out still reads the membership set before the join commits,
  so the race shrinks but stays.
- **Keep newness in memory only (what the replica held when the pull began)**
  — the first version of this decision. The container's row is saved by the
  delta before its whole pull, so any interruption in between (a later page
  failing, a timeout, a reload) made the next pull see it as held and the
  history was lost for good.
- **Have the server send a joined container's children by delta (bump their
  `seq`)** — a join would rewrite every task of a large list and wake every
  other member's client.

## Consequences

- A newly joined list or budget always reaches the member with its existing
  documents, whichever device or path joined it, even across failures and
  reloads.
- Another device of the member, learning of the join by delta, pulls the
  container whole once.
- One more replica meta key, cleared with the replica.
- The fresh-join memory lives in the running app only. A reload between the
  join and the next pull loses it, which is harmless for purging: the
  reload's pull begins after the join, so its membership set includes it.
- A join waits for a running purge pass to finish before its documents are
  written; the pass is local and short.

## Related

ADR-0006 (delta sync by `seq`), ADR-0061 (paged pulls), ADR-0079 (pulled rows
never replace a newer local copy).
