---
title: Lists are shared by membership on TaskList
tags: [architecture, domain, sync, identity]
date: 2026-10-01
status: Active — Extends ADR-0051, amends ADR-0034 and ADR-0042
---

# ADR-0053: Lists are shared by membership on TaskList

## Context

`specs/sharing-spec.md` (#103, #104) asks for a list the owner shares with
other signed-in people: they see it, edit its content offline, and sync it
like their own. Two things stood in the way.

First, every aggregate in slice 1 is keyed on one `UserId`, and both sync
(`userId == caller`) and every handler's access check (`TaskList.Require`)
assume that `UserId` is both owner and only actor. Letting a second person
write to someone else's list means either growing a second notion of
"whose data is this" everywhere, or finding one place to put it.

Second, the event log (AD-2) is the record of who did what. If a member's
command is stamped with the member's own id as `UserId`, the aggregate
changes owner mid-document — or the list has to fork into one document per
member, which breaks offline sync (AD-6), Today (cross-area, one task list)
and Statistics (`adr/0037`, keyed on `userId`) all at once.

## Decision

A list is shared by membership, not by a second aggregate.

`TaskList` gains `InviteToken` (`string?`, null = not shared), `OwnerName`,
and `Members` (`ListMember(UserId, DisplayName, JoinedAt)`); `UserId` stays
the owner, always. Five commands manage it — `ShareTaskList` (set/rotate
token, owner-only), `StopSharingTaskList` (clear token, owner-only, members
stay), `RemoveListMember` (owner-only), `LeaveTaskList` (member-only,
rejects the owner), `JoinTaskList` (server-only: resolves a token to
membership, idempotent for an existing member or the owner).

`ListAccess.To(list, actorId)` replaces the bare owner check for every
content command — tasks, steps, recurrence, descriptions, stars,
priorities, due dates, completion, occurrences, reference items and their
fields, plus `CreateTask`, `CreateReferenceItem`, `OrganiseInboxItem`.
`TaskList.Require` (owner only) stays for list-level commands —
`RenameTaskList`, `DeleteTaskList`, `MoveTaskListToArea`, the five sharing
commands, and `LinkTaskToGoal` (goals are the owner's). A handler that
previously loaded nothing but the task now loads its parent list to resolve
access (`ListAccessLoading.AccessAsync`).

`DomainEvent` gains `ActorId` (`Guid?`, defaults to `UserId` through the
`Actor` property). `UserId` on every event stays the **aggregate's owner**;
`ActorId` is who actually issued the command. `MongoUnitOfWork.CommitAsync`
stamps it, not the aggregate or the handler: it compares the staged event's
`UserId` against the authenticated `userId` on the way to the transaction
and sets `ActorId` only when they differ, so every `Decide`/`When` in the
Tasks module keeps writing plain `UserId` and stays ignorant of sharing
entirely — the aggregate's creation events (`TaskCreated`,
`ReferenceItemCreated`) take their owner from the target list, so a
member's `CreateTask` produces a task owned by the list's owner.

`IServerOnlyCommand` (a marker on `ICommand`) keeps a command out of
`/api/commands`: `CommandDispatcher.DispatchAsync` refuses it with
`Unrecoverable: true` before it reaches a handler, the same path
`JoinTaskList` uses today (`POST /api/lists/join` calls
`CommandDispatcher.RunAsync` directly, bypassing the client-facing check).
The next plan's `MarkTaskFromSnapshot` and `MarkReferenceItemFromSnapshot`
(`specs/sharing-spec.md` §2.6) reuse the same marker.

`MoveTaskToList` and `MoveReferenceItemToList` reject a target list whose
owner differs from the source list's — a task or item never crosses
ownership by moving. `OrganiseInboxItem` into a list the actor is a member
of is allowed: the Inbox item becomes a task owned by the list's owner, the
Inbox itself stays the actor's own aggregate, untouched by sharing.

Sync's widened filters and the `memberListIds` reconciliation that let a
member's replica actually hold the list (`specs/sharing-spec.md` §3) are
the next plan's work, not this one's — this decision only establishes that
membership, not a parallel aggregate, is what sync and every future reader
will key off.

## Considered alternatives

- **A `ListShare` aggregate in a new Sharing module, behind an
  `IListAccess` port.** Keeps `TaskList` itself untouched and gives a clean
  module seam. Rejected: it is a second piece of truth a client must agree
  with before any task command can be authorized — every write now reads
  two documents instead of one, and offline (AD-6, last-write-wins per
  aggregate) gains a second aggregate that can go stale independently of
  the list it gates. The membership state is small and change-infrequent;
  putting it on `TaskList` keeps authorization and the thing being
  authorized as one document under one version.
- **A reader list on every child document** (`TodoTask.ReaderIds`,
  `ReferenceItem.ReaderIds`, copied from the list at creation). Avoids the
  list lookup `ListAccess` now does on every handler. Rejected: joining a
  list adds a reader to every existing task and item it already contains,
  not just future ones; a list move, a cascade delete, and `RemoveListMember`
  would all need to keep hundreds of child documents in step with one
  membership change instead of one list document.

## Consequences

Every task and reference-item handler that only loaded its own aggregate
now also loads the parent `TaskList` to resolve `ListAccess` — one more
round trip per command, acceptable next to a MongoDB transaction that
already does several. Members see the invite token when they look at a
shared list's panel and may forward it; that is the deliberate
"anyone with the link" model (`specs/sharing-spec.md` §2.1), not a leak.
Cross-owner moves are rejected outright rather than silently re-owning a
task, so a member who wants a task in their own list must recreate it
there. Account deletion's cross-collection sweep (`adr/0034`) now also
pulls the deleted user out of every other owner's `tasklists.Members` —
a membership that outlives its member is nothing a reader can clean up on
its own, and plan 2 will need the owner's replica to see it. The owner/actor
split is paid on every event forever, even for the overwhelming majority
that have no sharing involved — `ActorId` is null there and costs one
nullable field.
