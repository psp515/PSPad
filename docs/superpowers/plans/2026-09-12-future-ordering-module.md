# Future work: extract ordering into its own module

> **Status: not scheduled.** This is a stub for later planning, not a plan
> ready for `superpowers:subagent-driven-development` or
> `superpowers:executing-plans`. Do not execute this without first writing
> the full task-by-task plan the way `2026-09-12-02-tasks-core.md` does.

**Decision record:** `docs/arch/adr/0012-extract-ordering-into-its-own-module.md`
(status: Proposed).

## Why this exists

Position is currently a field on the domain aggregate it orders (`Area`,
`TaskList`, `Step`, `InboxItem`). That was the cheapest path for slice 1.
Ordering is a UI-placement concern, not a domain fact, and it should live
in its own module once more than one feature needs it — the Tasks module
should describe *what* an area/list/task is, not *where it sits on screen*.

## Shape of the future work (sketch, not a committed design)

1. New module `PSPad.Module.Ordering` (or fold into `PSPad.Module.Tasks`'s
   client-facing layer if a full module is overkill by then — decide at
   design time, not now).
2. A `Placement` aggregate/record: `(UserId, ElementType, ElementId) ->
   Position`. `ElementType` is a string or enum discriminator ("Area",
   "TaskList", "Step", "InboxItem", ...) so one placement store serves every
   orderable kind without the store knowing what a `Goal` or `Step` is.
3. `Positions.Next`/`Positions.Move` (currently
   `src/modules/PSPad.Module.Tasks/Ordering/Positions.cs`) move to the new
   module and operate on `Placement` rows, not on aggregate fields.
4. Remove `Position` from `Area`, `TaskList`, `Step`, `InboxItem` and their
   `Created`/creation commands. Removing a field a client may still send is
   a breaking wire change — coordinate with whatever's consuming
   `PSPad.Contracts` by the time this runs (plan 07 client PWA, plan 08
   sync) rather than assuming it's still just this backend branch.
5. **No migration.** An element with no `Placement` row sorts alphabetically
   by name. Ship the fallback first, then let ordering be re-set by hand —
   never write a backfill script for this.
6. Every query that currently reads `.Position` off an aggregate (today
   there are none yet outside tests, since plan 04's queries don't exist
   yet) needs to become a join against the placement store instead.

## Open questions for whoever picks this up

- Does `PSPad.Module.Ordering` need its own aggregate/event log (consistent
  with AD-2/ADR-0011), or is a single document per `(UserId, ElementType)`
  holding an ordered list of ids simpler and sufficient? The latter avoids
  a fan-out of one document per element but makes concurrent reorders by
  two devices resolve differently than per-element documents would.
- Does this module need to exist before plan 04 (API & persistence) wires
  real queries, or can `Position` stay on the aggregates through slice 1
  and only be extracted once a second orderable feature (habits, yearly
  goals) actually needs it? Cutting it now is speculative; cutting it when
  a second consumer shows up is YAGNI-safe and is the default unless
  product timing says otherwise.

## Definitely out of scope for this stub

Any code change. This file exists so the decision has a place to grow into
a real plan, not to authorize starting one.
