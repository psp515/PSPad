---
title: Coming up looks one week ahead for every task, and the lead time is removed
tags: [domain, today, ui, sync]
date: 2026-11-20
status: Active
---

# ADR-0093: Coming up looks one week ahead for every task, and the lead time is removed

> Status: Active. Supersedes the lead-time part of
> [ADR-0053](0053-yearly-repeats-and-lead-time.md); its yearly repeats and
> One-time | Repeating toggle stay in force.

## Context

ADR-0053 gave each task a lead time (1–99 days, weeks or months) that set
how far ahead it showed in Coming up, and labelled its row "Remind me".
Push reminders (ADR-0092) took the name "Remind me" for a notification, and
the old row was renamed "Show ahead". In use, the maintainer found two rows
about "before the due date" in one panel confusing, and Show ahead was not
worth its place: the push reminder answers "tell me in time" for a yearly
bill, which was the case the lead time was built for.

## Decision

We will remove `LeadTime`, `LeadUnit`, `SetTaskLeadTime` and
`TaskLeadTimeSet`. `TodayRule.Plan` shows a one-time task's trigger or a
repeating task's next unticked occurrence in Coming up when it falls within
the next seven days (`TodayRule.UpcomingDays`), the same reach for every
task. Stored `leadTime` fields are left in place and ignored on load; past
`TaskLeadTimeSet` events stay in the log as text. A `SetTaskLeadTime` from an
older client is refused as an unknown command, which the client treats as
final and drops (ADR-0021).

## Considered alternatives

- **Hide the row and keep the field.** Tasks that already carry a lead time
  would keep a reach the user can no longer see or change.
- **Keep the command as a no-op for old clients.** It would accept a change
  that does nothing; an unknown-command refusal is already handled and says
  what happened.
- **A migration that unsets `leadTime`.** Extra elements are already
  ignored on load (`IgnoreExtraElementsConvention`); a migration adds risk
  for no visible change.

## Consequences

- A task with a long lead time (a yearly bill set to one month) now enters
  Coming up only a week before; a reminder set some days before is the way
  to be told earlier.
- A task with a short lead time (one day) now shows a week ahead again.
- One fewer row and command in the task panel and the shared contract.
- `leadTime` stays in old documents until the task is next saved by a full
  replace; nothing reads it.
