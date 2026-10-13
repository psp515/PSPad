---
title: A goal holds its own task links; a task names no goal
tags: [architecture, domain, aggregates, security, sharing, statistics]
date: 2026-10-12
status: Active
---

# ADR-0065: A goal holds its own task links; a task names no goal

> Status: Active (amends [ADR-0008](0008-aggregate-boundaries.md))

## Context

Issue #129. `TodoTask.GoalId` was set by `LinkTaskToGoal` with no check that the
goal existed or belonged to the actor. Shared lists (ADR-0054) send a list's
tasks in full to every member, `goalId` included, so a member could link their
own task to the owner's private goal and read its name, and every rename, in
their statistics feed, which resolved labels by id alone. A task in a shared
list could also hold only one goal, the owner's, so members could not count a
shared task toward their own goals.

## Decision

We will store goal links on the goal: `Goal` gains `TaskIds`, the tasks the
goal's owner counts toward it, and `TodoTask` loses `GoalId`. `AddTaskToGoal`
and `RemoveTaskFromGoal` act on the actor's own goal only; adding requires the
actor to see the task (they own it or are a member of its list). Each user puts
a task in at most one of their own goals: adding it to goal B removes it from
the user's goal A in the same unit of work. The client finds a task's goal by
looking through the user's goals.

`TaskCompleted` and `OccurrenceCompleted` no longer carry a goal. Statistics
attribute a completion to a goal when the overview is read, from the reader's
own goal links; label lookups are limited to the reader's own labels and the
lists they are a member of. A one-time startup migration moves each task's
`goalId` into its owner's goal and drops links to anyone else's goal.

## Considered alternatives

- **Keep `TodoTask.GoalId` and check ownership in `LinkTaskToGoal`** — closes
  the write, but members still receive the owner's goal ids in sync, a shared
  task can still only carry the owner's goal, and statistics still trust a
  goal id that arrived on someone else's task.
- **Many goals per task per user** — no current screen needs it, and the goal
  picker, goal page and "by goal" chart all assume one goal per task.
- **Goal links as their own aggregate** — one document per link multiplies
  writes and sync rows for what is a short set on a document the user already
  owns and syncs.

## Consequences

A user's goals can only ever name tasks; nobody else's goal id travels anywhere,
and a member can count a shared task toward their own goal. The "by goal" chart
follows the current links, so moving a task to another goal moves its past
completions with it. A goal keeps the id of a task deleted later; readers skip
ids they cannot resolve. Old `TaskLinkedToGoal` events stay in the log and still
project for statistics replay. Concurrent moves between two goals from two
devices resolve last-write-wins per goal (ADR-0005), which can briefly leave a
task in two of a user's goals until the next move.
