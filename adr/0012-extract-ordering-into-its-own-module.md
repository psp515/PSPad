---
title: Extract element ordering into its own module, out of the Tasks domain
tags: [architecture, domain, ui, future-work]
date: 2026-09-12
status: Proposed
---

# ADR-0012: Extract element ordering into its own module, out of the Tasks domain

## Context

`Area.Position`, `TaskList.Position`, `Step.Position` and `InboxItem.Position`
today store a dense integer directly on the domain aggregate that owns the
ordered element (ADR-0007's plan, task 2: `Positions.Next`/`Positions.Move`).
This was the cheapest way to ship slice 1's ordering requirement, but
position is where an element sits *on screen*, not a fact about the area,
list, step or inbox item itself — two users could reasonably want the same
set of areas ordered differently, and a future feature (e.g. a second view
of the same lists) would want its own ordering without touching the
domain aggregate at all. Storing it on the aggregate ties a UI-placement
concern to the Tasks module's data, and to AD-4/ADR-0004's WASM-purity
boundary for reasons that have nothing to do with placement.

This ADR is `Proposed`, not `Active`: it records a future direction, not a
change being made now. No code changes in this branch.

## Decision

Future work: move ordering into a separate module (working name
`PSPad.Module.Ordering`) that owns a `Placement` record keyed by
`(UserId, ElementType, ElementId) -> Position`, independent of every
aggregate whose elements it orders. Tasks-module aggregates stop carrying
`Position` fields; `Positions.Next`/`Positions.Move` move to the new
module and operate on placements, not on aggregate state directly.

No migration is planned for existing `Position` data: when this cuts over,
elements with no placement record fall back to **alphabetical order by
name**, and users re-order manually whatever ordering they cared about.
This is acceptable because ordering is a low-stakes, quickly-reset user
preference, not data whose loss causes real harm.

## Considered alternatives

- **Keep position embedded in each aggregate (status quo)** — the option
  being moved away from; rejected long-term because it couples a UI
  concern to domain aggregates that plan 03 onward (Today, habits, yearly
  goals) will keep growing, none of which need to know how their elements
  are ordered on screen.
- **Write a migration that carries existing `Position` values into the new
  module** — rejected: the values already exist as the same dense
  integers, so a migration is possible, but the explicit decision here is
  not to bother — ordering is cheap for a user to redo and not worth the
  migration code's maintenance cost.
- **A generic `IOrderable` interface each aggregate implements, resolved by
  a service in the same module** — rejected: still lives inside
  `PSPad.Module.Tasks`, so it doesn't solve the actual problem (ordering
  data mixed into domain aggregates); it would only rename the coupling.

## Consequences

Once done: Tasks-module aggregates model only task semantics, and any
future orderable list (habits, yearly goals, print-domain parts) gets
ordering for free from one module instead of re-implementing
`Positions.Next`/`Positions.Move` per aggregate. Reading an ordered list
becomes a join between the aggregate query and the placement lookup instead
of a field already on the loaded document — an extra round trip (or a
batched lookup) that today's single-document read doesn't pay. Until this
is implemented, `Position` stays on the aggregates as-is; this ADR is the
plan for when that changes, not a description of the current code.

