---
title: Repeats can be yearly, and a task's lead time sets how far ahead it shows
tags: [domain, recurrence, today, ui]
date: 2026-09-30
status: Active
---

# ADR-0051: Repeats can be yearly, and a task's lead time sets how far ahead it shows

> Amends [ADR-0043](0043-repeat-interval-and-until-date.md) (repeat interval
> and until date).

## Context

Issue #100. A repeat could be daily, weekly or monthly, so "car insurance,
every 15 March" had to be written as every 12 months on day 15, which reads
wrong and cannot express 29 February. Upcoming on My Day looked a fixed
seven days ahead, so a yearly bill surfaced one week before it was due — too
late to act — while a daily chore cluttered the week with the next day's
occurrence. The issue also asked for "remind me X days or months before",
and for the task panel to separate a one-time task from a repeating one: a
repeating task's due date means "until" (ADR-0043), and showing Due and
Repeat side by side made that easy to misread.

No push channel exists yet; Reminders is a later subsystem.

## Decision

We will add `RecurrenceKind.Yearly`. It fires on `StartsOn`'s month and day,
clamped to the month's length (29 Feb fires on 28 Feb in non-leap years), in
every Nth year counted from `StartsOn`'s year. The rule gains no field.

We will give `TodoTask` an optional `LeadTime` — an amount 1–99 and a unit
(days, weeks, months) — set by `SetTaskLeadTime` / `TaskLeadTimeSet`. It
belongs to the task, not the rule, because one-time tasks use it too.

We will read the lead time as the task's **look-ahead** in `TodayRule.Plan`:
a one-time task's earliest trigger, or a repeating task's next unticked
occurrence, is shown in Tomorrow or Upcoming only from
`LeadTime.FirstShownFor(date)` onwards. With no lead time the look-ahead is
the previous seven days. The lead time never moves a task into Today or
Overdue: dates alone still drive those, and a repeating task is still never
overdue.

We will separate the two kinds in the task panel with a One-time |
Repeating toggle over the same `TodoTask` aggregate. One-time shows Due;
Repeating shows Repeat and Until. Switching clears the other side's fields.

## Considered alternatives

- **Lead time puts the task on Today early.** A task due in two weeks would
  sit on Today for two weeks, crowding what "do now" means and blurring the
  Today rule (AGENTS.md §3). Upcoming is where "coming, prepare" belongs.
- **Lead time only for repeats**, as the issue worded it. One-time tasks
  have the same problem (a passport renewal due in two months), and the
  field and the rule are identical.
- **Lead time on `RecurrenceRule`.** Would not reach one-time tasks and
  would mix "when it fires" with "when I want to see it".
- **Yearly as monthly with interval 12.** Works for most dates but reads
  "Every 12 months on day 15", loses the month in the description, and
  cannot say 29 February.
- **Repeating tasks as a separate aggregate.** New commands, events, sync
  collection and Statistics projections for the same fields, re-opening
  ADR-0007 and ADR-0043. The toggle gives the separation the user sees
  without it.
- **Store only, act later.** Saves the field for the Reminders subsystem
  but gives the user nothing now.

## Consequences

- Yearly repeats read "Yearly on 15 Mar" and handle leap days.
- Upcoming has no fixed end: its reach is per task. A task with a
  six-month lead sits in Upcoming for six months; that is what the user
  asked for.
- A short lead time also **hides** a task: a 1-day lead keeps a task due
  in three days out of Upcoming. The empty value reads "1 week before" so
  the default is visible.
- Finding a repeat's next occurrence scans day by day up to the
  look-ahead's reach — at most 99 months, a few thousand `OccursOn` calls
  per task. Acceptable for one user's tasks; revisit if it shows in
  profiles.
- The Reminders subsystem gets a stored "how early" per task to schedule
  from, without a new field.
- Old documents need no migration: `LeadTime` reads `null`, the old
  seven-day behaviour.
- The Repeat menu loses Never; the toggle is the only way to stop a task
  repeating.
