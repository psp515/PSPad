---
title: A star puts a one-off task on Today
tags: [domain, today]
date: 2026-09-25
status: Active
---

# ADR-0035: A star puts a one-off task on Today

> Status: `Active`. Reverses the slice-1 settlement in AGENTS.md §10 that a
> star "never puts task on Today"; that line had no ADR of its own.

## Context

Slice 1 settled that the star means *important only*: it sorts a task up
but never places it on Today, so dates alone drove Today. In daily use the
maintainer stars a task precisely to say "I want this in front of me now",
and having to also set today's due date to get it onto My Day is friction
the star was meant to remove. My Day's new sections (Tomorrow, Upcoming)
made the gap more visible: a starred task due next week sat in Upcoming.

## Decision

We will put every open, starred, one-off task on Today, in
`TodayRule.Select`, regardless of its due date — undated or due later. It
keeps its own due date for display and is never overdue unless its date has
passed, in which case it stays in Overdue. A starred task on Today is left
out of Tomorrow and Upcoming. Recurring tasks are unaffected: they still
show only on days their rule falls, so the never-overdue rule is untouched.
Because the change lives in the domain rule, the server's `/api/today`,
the sidebar count and My Day all agree.

## Considered alternatives

- **A separate "Important" section on My Day** — keeps dates as the only
  driver of Today, but splits "what do I do now" over two places, which is
  what the single Today screen exists to avoid.
- **Star a recurring task onto every day** — would contradict the
  occurrence model (AD-7) and make a skipped day look pending.

## Consequences

Today now answers "what is due or what did I flag". A user who stars many
tasks gets a long Today; unstarring is the remedy. Star still sorts first
within a section. The rule remains in one place, tested in the module, the
client projection and (through `Select`) the API query.
