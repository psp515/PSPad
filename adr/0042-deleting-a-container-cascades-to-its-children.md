---
title: Deleting an area or list deletes what it contains
tags: [domain, persistence, offline]
date: 2026-09-27
status: Active
---

# ADR-0042: Deleting an area or list deletes what it contains

## Context

Delete used to touch one aggregate only. `DeleteArea` marked the `Area`
deleted and left its `TaskList`s pointing at a dead `AreaId`; `DeleteTaskList`
did the same to its `TodoTask`s. The confirm dialogs said so ("Its lists
stay, without an area"). In practice those children were lost, not kept:
nothing in the UI lists a list whose area is gone, and a task whose list is
gone still showed on Today while linking to a dead `/lists/{id}` — and if it
was not due, it was unreachable altogether. They also stayed open in the
Statistics Outstanding count forever. Issue #47.

Two forces shape the fix. AD-8 keeps each of `Area`, `TaskList` and
`TodoTask` its own aggregate, linked by id, so the cascade cannot be a nested
delete inside one document. And AD-3 runs the same handler offline against
the replica, so a device that was offline when a container was deleted can
still ship commands aimed at it.

## Decision

We will make deleting a container delete its live children in the same
command and the same transaction:

- `DeleteArea` deletes the area, every live `TaskList` whose `AreaId` is that
  area, and every live `TodoTask` in those lists.
- `DeleteTaskList` deletes the list and every live `TodoTask` in it.
- Each child emits its own existing event (`TaskListDeleted`, `TaskDeleted`),
  staged into the one unit of work. No new event types.
- The list-to-tasks step lives once, in `TaskListCascade`, used by both
  handlers. Children are found with `IDocumentStore<T>.LoadAllAsync(userId)`
  and filtered in memory; the aggregates stay single-document.

We will also reject commands that write into a missing or deleted
container, so a stale device cannot re-orphan into it: `CreateTask`,
`MoveTaskToList` and `OrganiseInboxItem` require a live target list;
`CreateTaskList` and `MoveTaskListToArea` require a live target area. The
check lives in the handler (it needs a second store), reuses the
container's own `Require`, and the rejection is surfaced like any other
(AD-5).

Documents orphaned before this change are left as they are.

## Considered alternatives

- **Cascade area → lists only, keep orphaned tasks.** Moves the same bug one
  level down: whole lists' worth of tasks become orphans at once.
- **Refuse to delete a non-empty area or list.** No silent loss, but the
  user has to empty a container by hand before removing it — friction for
  the common case of retiring a whole project.
- **Re-home the children** (an auto-created fallback list, or the Inbox).
  Needs a new "fallback" concept; the Inbox holds plain text, so moving a
  task there would drop its steps, dates, priority and goal link.
- **One `AreaDeleted` event with the cascade implied.** Every consumer —
  sync replica, Statistics labels and Outstanding — would need to learn the
  implication. Per-child events are already understood everywhere.

## Consequences

- One delete can stage many documents and events in one transaction. Fine
  at single-user GTD sizes; a very large area makes a correspondingly large
  commit.
- The delete handlers now load all of the user's lists and tasks. Cheap on
  the replica, a scoped query on Mongo; worth an index-backed query if a
  user's task count grows large.
- Deletion is irreversible and wider than before. The dialogs say so; there
  is no undo.
- Because the server re-runs the handler against its own truth, the cascade
  also catches children a device had not pulled yet.
- Commands aimed at a list or area that another device deleted are now
  rejected rather than accepted into a dead container. The outbox ships in
  order, so "create list, then add tasks to it" offline still succeeds.
- Tests that created tasks into an unseeded list id must now seed the list.
- Existing orphans remain; a one-off sweep would be a separate change.
