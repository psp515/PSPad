---
title: A Presentation module holds per-user views of shared data
tags: [architecture, modularity, domain, ui, sync]
date: 2026-09-30
status: Active — Supersedes ADR-0012
---

# ADR-0051: A Presentation module holds per-user views of shared data

## Context

Issue #96 asks for lists to be ordered inside an area. `TaskList.Position`
already existed but had no command to change it, and ADR-0012 had proposed
pulling ordering out of the Tasks aggregates into its own module. Two further
forces shaped the decision: position is where an element sits on *one
user's* screen, so it must stay per-user even once lists are shared; and the
maintainer expects more per-user presentation later (per-list theming,
colour, icon), which a module named only for ordering would not fit.

Each list is its own aggregate, so reordering by rewriting `Position` on
every sibling means one command staging several aggregates, and a position
field on a shared list could only ever hold one user's order.

## Decision

We will add `PSPad.Module.Presentation`, which owns how a user sees their
data, never what the data is. It references `PSPad.Abstractions` only and
compiles to WASM, so client and server run its handlers (AD-3, AD-4). No
other module references it.

Its first and only aggregate is `AreaView`, one per `(user, area)`, with a
deterministic id derived from both. It holds `Order`, the list ids in the
order the user chose. `ReorderLists` carries the order the client displays,
so the module never reads Tasks aggregates. Display order is
`Arranged.Sort`: ids in `Order` first, then every other list by
`CreatedAt`, then id; stale ids are ignored, so deleting or moving a list
needs no cascade.

`TaskList.Position` is removed and replaced by `TaskList.CreatedAt`
(backfilled from `TaskListCreated`). Existing positions are not migrated;
creation order is the fallback, and it matches the order `Position` was
handed out in, so boards look the same until a user reorders.

`ListView` (per-user list colour, icon, step and item order) is the named
next extension point, recorded in `specs/modules-spec.md`, not built.

## Considered alternatives

- **Renumber `TaskList.Position` across siblings in one command** — works
  today via multi-aggregate staging, but writes N documents per move and
  keeps a single order per list, which breaks once lists are shared.
- **`Area.ListOrder`** — one write, but splits a list's placement across two
  aggregates that last-write-wins can leave disagreeing, and it is still not
  per-user.
- **Fractional `Position` (double between neighbours)** — one write per move,
  but changes the field type, eventually needs renumbering, and is still on
  the aggregate and not per-user.
- **ADR-0012's per-element `Placement` records and alphabetical fallback** —
  per-element records turn a move into many writes; alphabetical fallback
  would reshuffle every existing board on release and move a list on rename.
- **A module named `Ordering` or `Personalization`** — `Ordering` is too
  narrow for theming; `Personalization` invites user preferences that already
  live in Identity.

## Consequences

Presentation data is per-user and separate from domain data, so sharing and
theming land without touching Tasks aggregates. Reading a board now joins
lists with one `AreaView` — cheap, both are in the replica. A new sync
collection, `areaviews`, pulls from zero once on existing replicas
(ADR-0049), and account deletion must sweep it. Orphaned views of deleted
areas linger until account deletion; nothing reads them. `Area`, `Step`,
`InboxItem`, `ReferenceField` and `ReferenceItem` still carry `Position`, so
for now ordering lives in two places; `AreaView` reorders with its own few
lines instead of referencing Tasks' `Positions`. Two offline devices
reordering the same area resolve by last write wins over the whole order.
