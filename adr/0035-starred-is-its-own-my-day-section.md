---
title: Starred tasks get their own My Day section, not the Today rule
tags: [domain, today, ui]
date: 2026-09-25
status: Active
---

# ADR-0035: Starred tasks get their own My Day section, not the Today rule

## Context

Slice 1 settled that a star means *important only*: it sorts a task up but
never places it on Today, and dates alone drive Today (AGENTS.md §10). In
daily use the maintainer stars a task to say "keep this in front of me",
and with My Day split into Overdue, Today, Tomorrow and Upcoming, a starred
task due next week or with no date at all was either buried in Upcoming or
missing from My Day entirely.

## Decision

We will give My Day a **Starred** section between Today and Tomorrow,
derived in the domain by `TodayRule.Plan`, not by the page. It holds every
open, starred, one-off task that is not overdue or due today — undated
first, then by date. Such a task is left out of Tomorrow and Upcoming so it
shows once. A starred task due today or earlier stays in Today or Overdue,
where the star already sorts it up. Recurring tasks are unaffected.
`TodayRule.Select` — the Today rule, `/api/today` and the sidebar count —
stays date-only.

## Considered alternatives

- **Let the star put a task into the Today rule itself** — tried first on
  the same branch. It made Today mean "due or flagged", inflated the
  sidebar count and `/api/today`, and reversed a settled rule for what is
  a presentation need.
- **Show starred tasks in both Starred and their date section** — duplicates
  rows, and ticking one would visibly drop two.

## Consequences

The Today rule and its three test sites are unchanged. My Day gains a
seventh section to reason about; a starred task due tomorrow now appears
under Starred, not Tomorrow, which is the one place a date no longer picks
the section. Unstarring puts it back.
