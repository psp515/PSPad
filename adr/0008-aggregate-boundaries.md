---
title: Aggregate boundaries — Area, TaskList, TodoTask, Goal, Inbox, User
tags: [architecture, domain, aggregates]
date: 2026-09-11
status: Active
---

# ADR-0008: Aggregate boundaries — Area, TaskList, TodoTask, Goal, Inbox, User

## Context

Slice 1 needed a settled answer to "what is one document, loaded and
written as a unit" before any command or handler could be written. Two
specific questions forced the issue: do steps live inside a task or as
their own collection, and does a goal hold the tasks that point at it or
the reverse.

## Decision

Aggregates are `Area`, `TaskList`, `TodoTask` (steps live inside the task,
not on their own), `Goal`, `Inbox` (one per user). `User` was added to this
list once identity became in-scope for slice 1 (during the 2026-09-12
MongoDB replanning). Cross-aggregate links are ids, never nested objects —
a task's goal link is a `Guid?`, and a goal holds no list of task ids.

## Considered alternatives

- **Steps as their own collection, referencing their task by id** —
  rejected: steps are never queried independently of their task and are
  always loaded with it, so a separate collection would only add a join for
  no benefit (also recorded in the Tasks-core plan, plan 02 task 7).
- **Goals holding a list of task ids** — rejected: goals are global and a
  task's goal link changes over the task's lifetime; keeping the list on
  the goal would mean updating two documents (goal and task) on every
  re-link instead of one, and risks the two drifting apart.

## Consequences

A task and its steps always load, decide and commit as one document — no
cross-collection transaction is needed for the single most common edit
(checking off a step). The cost is that any future feature wanting to query
steps across tasks (e.g. "all steps due this week") has to scan tasks, not
steps directly, since steps have no independent existence to index.
