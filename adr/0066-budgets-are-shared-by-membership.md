---
title: Share budgets by membership on the Budget, with invite rules in Abstractions
tags: [architecture, sharing, money, sync, security]
date: 2026-10-12
status: Active
---

# ADR-0066: Share budgets by membership on the Budget, with invite rules in Abstractions

## Context

A household wants one budget that every partner writes to (#121). Lists are
already shared by membership on the aggregate (ADR-0054), joined with a link
plus a code (ADR-0057), with every membership change online-only
(ADR-0063). The Money module was built for the same model: `UserId` is the
owner on every document and one function, `BudgetAccess.To`, gates every
command (ADR-0059).

Two things stand in the way of copying lists as they are. The invite rules
(`InviteCodes`, `InviteCheck`, the 30-minute life, the five-wrong-codes
lockout, the constant-time compare) live in `PSPad.Module.Tasks`, and Money
may reference `PSPad.Abstractions` only. And money is more sensitive than a
task list, so a member must not see the invite secrets and an archived
budget must not stay joinable.

## Decision

We will share a budget by membership on the `Budget` aggregate, the way
ADR-0054 shares a list: `Members[]`, `OwnerName` and the invite fields on
`Budget`; `UserId` stays the owner on the budget and on every entry and
snapshot a member creates; `ActorId` on events records the member.

We will gate entries, categories and balance months with
`BudgetAccess.To` (owner or member), and rename, archive, restore and the
sharing commands with a new `BudgetAccess.Owned` (owner only). `ShareBudget`,
`StopSharingBudget`, `RemoveBudgetMember`, `LeaveBudget` and `JoinBudget` are
`IServerOnlyCommand`s behind `/api/budgets/...` endpoints, as ADR-0063 does
for lists. Archiving a budget ends its live invite, and an archived budget
cannot be shared or joined.

We will widen sync to budgets the caller is a member of, and to their
entries and snapshots, return `memberBudgetIds` beside `memberListIds`, and
let `full=` backfill budgets as well as lists. Members never receive the
invite fields.

We will move the invite rules, unchanged, to `PSPad.Abstractions/Invites`,
so `TaskList` and `Budget` share one implementation. Joins of both kinds
share one per-user rate limit.

We will not record or show who entered an entry or holding; the event log's
`ActorId` is the only trace.

## Considered alternatives

- **A mirrored budget per member** — rejected for lists in ADR-0054 for the
  same reasons: two documents to keep in step, and conflicts across them that
  last-write-wins per aggregate cannot settle.
- **A `Sharing`-owned membership record behind an access port** — rejected
  in ADR-0054; Money would need a port into a server-only module, and the
  WASM handlers could not check access offline.
- **Copy the invite rules into Money** — two copies of a security rule
  drift; one fix to the lockout or the compare would have to land twice.
- **Let Money reference Tasks** — breaks ADR-0059's edge rule and drags the
  Tasks module into every Money build.
- **Store "added by" on entries and holdings** — considered for the issue's
  "who entered what" and dropped by the maintainer as not needed now; it
  would add fields to two documents and their editors.
- **Separate members' holdings within one budget** — separate budgets
  already cover personal money.

## Consequences

- A member's budget works offline like their own; only joining and
  membership changes need a connection.
- Sync reads one more membership query per pull and three more indexes.
- `InviteLinkCard` and the people list become kind-neutral components.
- Abstractions grows a small invites folder that both domain modules use.
- The owner remains a single point of failure: deleting their account
  deletes the budget for every member. Transferring ownership is a separate
  decision if it is ever needed.
- A future third shareable aggregate follows the same recipe, and the cost
  of each copy (sync filter, sweep, endpoints) argues for extracting a shared
  helper then, not now.
