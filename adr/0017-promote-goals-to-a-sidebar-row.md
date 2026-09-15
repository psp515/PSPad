---
title: Promote Goals to a permanent sidebar row
tags: [ui, domain]
date: 2026-09-15
status: Active (supersedes Goals' placement in [0014](0014-one-navigation-tree-at-every-width.md))
---

# ADR-0017: Promote Goals to a permanent sidebar row

## Context

ADR-0014 put Goals and History behind the account menu, both on the same
reasoning: they are low-frequency and the sidebar's job is areas. `GoalsPage`
was also still the pre-redesign screen — a bare list with no `TaskRow`, no
task-detail overlay, no visual kinship with `AreaBoard`, `Today` or
`ListPage` — reached only from a dropdown that otherwise holds Theme, Sync
status and Sign out.

Living with the result showed the reasoning did not fit Goals the way it
fits History. History is an append-only log a user consults occasionally.
Goals are not: AGENTS.md §10 settles them as **global**, spanning every
area — the same shape as My Day and Inbox, both already permanent rows.
A user working GTD checks and assigns goals as part of routine planning,
not as an occasional lookup. Filing a first-class GTD concept next to
"Sign out" made it easy to forget it exists at all, which is exactly the
"what do I do now" problem AGENTS.md §1 says the whole application exists to
answer.

## Decision

We will give **Goals** a permanent sidebar row, between Inbox and the
divider that precedes the user's areas — the same tier as My Day and Inbox.
**History** stays in the account menu; its low-frequency reasoning still
holds. `GoalsPage` is rebuilt to match the current visual system:
`GoalCard` (structurally parallel to `ListCard`) showing each goal's open
tasks via the existing `TaskRow`, achieved goals collapsed into their own
section, and an inline **+ New goal** field.

No command, event, aggregate or sync change — `Goal` already supports
Create/Rename/Achieve/Reopen/Delete end to end. This is presentation layer
only, same scope discipline `ui-redesign-2-design.md` set for itself.

## Considered alternatives

- **Leave Goals in the account menu, just reorder or re-icon it.** Rejected:
  the problem is the tier, not the position within it — a dropdown is the
  wrong altitude for a screen a user is meant to check regularly.
- **A count badge on the Goals row, like My Day's due count or Inbox's item
  count.** Rejected: those counts are actionable ("N things need doing
  today"); an open-goal count is not a pending-work signal in the same way,
  and no other sidebar row (areas included) carries one.
- **Move History to the sidebar too, for symmetry.** Rejected: History's
  low-frequency reasoning from ADR-0014 was never the problem — only
  applying it to Goals was.

## Consequences

One more permanent sidebar row; `NavSidebarTests` and `AccountMenuTests`
change to match (Goals asserted present in the sidebar and absent from the
account menu). `GoalsPage` goes from a functional stub to a screen that
reuses `TaskRow`, `ThingMenu`, `NameDialog` and `ConfirmDialog` the way
every other screen already does, closing a gap the original redesign left
open. `specs/ui-redesign-2-design.md` D13 is amended in place to record
this, per AGENTS.md §11 — the record ships with the change, not as a
follow-up.
