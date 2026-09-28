---
title: A repeat carries an interval, and a repeating task's due date is its inclusive end
tags: [domain, recurrence, today, ui]
date: 2026-09-28
status: Active
---

# ADR-0043: A repeat carries an interval, and a repeating task's due date is its inclusive end

> Amends [ADR-0007](0007-recurrence-as-template-and-occurrences.md)
> (recurrence as template plus derived occurrences).

## Context

ADR-0007's rule knew three kinds — daily, weekly on listed weekdays, monthly
on a day — each firing every day, week or month. "Every 3 weeks on Monday"
or "every 2 days" could not be said. A repeating task also could not end:
`TodoTask.Decide` rejected a due date on a repeating task and a repeat on a
dated task ("A repeating task cannot also have a due date"), and the panel
greyed the due row, so a repeat ran forever or had to be removed by hand.
The Add panel had no repeat row at all. Issue #45.

## Decision

We will give `RecurrenceRule` an `Interval` (1–99, `EveryNth` rejects
anything else). Kinds are unchanged; the interval counts from `StartsOn`:
Daily fires every Nth day; Weekly fires on its weekdays in every Nth
Monday-based week, counted from `StartsOn`'s week; MonthlyOnDay fires on its
day (clamped to the month's length) in every Nth month, counted from
`StartsOn`'s month. Documents stored before the field existed read
`Interval = 0`; the rule reads it through `Every`, which treats anything
below 1 as 1.

We will treat a repeating task's `DueOn` as its inclusive end ("Until").
Both rejections are removed. `TodoTask.OccursOn(day)` is the rule's
`OccursOn` and `day <= DueOn`; Today, Upcoming, occurrence chips and
`CompleteOccurrence` all go through it, so nothing occurs or can be ticked
after the end. A repeating task is still never overdue — the due date is a
cut-off, never a trigger.

We will derive "ended" rather than store it: `TodoTask.EndedBy(today)` is
true when the task repeats and `DueOn < today`. An ended task counts as
completed on screen. No command, no event, no `CompletedAt` — like
Pending and Skipped, it follows from rule, end and today, and moving the
end forward reopens it.

We will derive the done count and streak from `completedDays`
(`RepeatTally`): total is the number of done days; the streak counts done
occurrences walking back from today, where today's still-pending occurrence
does not break it.

## Considered alternatives

- **A separate `EndsOn` on the rule** — a second date beside `DueOn` that a
  repeating task could never otherwise use, and two fields for the user to
  keep consistent. The due date already means "by when"; on a repeat that is
  "until when".
- **Keep repeat and due date mutually exclusive** (the previous behaviour) —
  leaves no way to end a repeat except removing it, which also discards its
  meaning on the task, and keeps the greyed row users read as broken.
- **An explicit end command and event** (`EndRecurrence` / `RecurrenceEnded`)
  — stores a fact that follows from the date and today, needs a scheduler or
  a first-look write to fire on the right day, and has to be undone when the
  end moves. Derivation needs none of that.
- **Store the rule as an iCalendar RRULE string** — covers far more than the
  three kinds the UI offers, needs a parser in the WASM-safe module, and
  turns a readable document into an opaque string. A single integer field
  covers "every N" with no new dependency.

## Consequences

- "Every 3 weeks on Monday", "every 2 days" and "every 6 months on day 15"
  are expressible; the editor offers them through a Custom… dialog beside
  the presets.
- Repeats can end, and the Add panel can set a repeat and an end in one go
  (the client sends `SetTaskRecurrence` before `SetTaskDueDate`, though
  either order is valid now).
- Old documents need no migration: `Interval = 0` behaves as 1.
- "Ended" has no event, so Statistics records nothing when a repeat runs
  out; the log shows only the ticks and the due date being set. A chart that
  wants "repeats finished" would have to derive it too. Statistics'
  Outstanding chart likewise never records an end for a repeating task,
  exactly as for recurring tasks before this record — a known gap.
- Every reader of a repeat must go through `TodoTask.OccursOn`, not
  `RecurrenceRule.OccursOn`, or it will show occurrences past the end. The
  rule alone no longer answers "does this task occur today".
- Interval arithmetic anchors on `StartsOn`: changing the start date shifts
  which weeks or months fire, and the Custom dialog exposes it for that
  reason.
