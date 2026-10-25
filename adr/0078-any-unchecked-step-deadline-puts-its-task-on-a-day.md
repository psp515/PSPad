---
title: Any unchecked step's deadline puts its task on a day
tags: [domain, today, steps, ui]
date: 2026-10-24
status: Active
---

# ADR-0078: Any unchecked step's deadline puts its task on a day

> Changes the Today rule invariant (AGENTS.md §2). No earlier ADR recorded
> the "next unchecked step" clause; it lived in `specs/backend-spec.md` §4
> and `specs/recurrence-lead-time-design.md` §2.3, which this ADR replaces.

## Context

Issue #202 asks for a deadline on a step, and for the task to show on the
day that deadline falls, with the due step marked on its card.

The domain already stores a step date (`Step.DueOn`, `SetStepDueDate`), but
the Today rule reads only the **next** unchecked step's date: a task's
trigger is min(due date, `NextUncheckedStep.DueOn`). Steps are an ordered
checklist, and a user dating step 3 ("hand over the keys, 31 Oct") while
steps 1 and 2 are undated gets nothing: the date is ignored until the
earlier steps are checked. No screen sets a step date yet, so no user has
relied on the narrower rule.

## Decision

We will make a task's trigger the earlier of its own due date and the
earliest deadline among **all** its unchecked steps
(`TodoTask.EarliestDueStep`; a tie goes to the lower position). Checked
steps never count. Repeating tasks keep ignoring step dates and stay never
overdue.

The rule stays in one place, `TodayRule`, whose trigger is exposed as
`TodayRule.TriggerOf(task)` so other screens (list sort, filter and calendar,
#198) read the same date. Everything downstream (Overdue, Coming up, lead
time, Starred) is unchanged and follows the new trigger.

Storage, commands, events and the API contract do not change.

## Considered alternatives

- **Keep the next unchecked step only.** No rule change, but a deadline on
  any step after the first undated one does nothing, with no sign why. The
  issue's own example would fail.
- **The task's due date wins whenever it is set.** Simpler to explain, but a
  step due before the task (order boxes before moving day) would not
  surface, which is the point of a step deadline.
- **Derive the task's due date from its steps and store it.** A second
  source of truth that must be kept in step with every step command, and
  last-write-wins offline (ADR-0005) could leave the two disagreeing.

## Consequences

- A dated step anywhere in the checklist puts the task on that day, makes it
  overdue when missed, and drives Coming up and lead time.
- A task can now be overdue because of a step while its own due date is
  weeks away. The card shows the step mark in red so the reason is visible.
- The Today rule is tested in three suites (module, API, client); all three
  change together.
- `NextUncheckedStep` loses its only reader and is removed.
- If users want sequential checklists where later dates should wait, this
  rule shows those tasks early; a per-task "in order" switch would be the
  fix, and nobody has asked for it.
