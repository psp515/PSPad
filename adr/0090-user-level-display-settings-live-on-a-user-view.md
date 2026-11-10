---
title: User-level display settings live on a synced UserView in Presentation
tags: [architecture, modularity, ui, sync]
date: 2026-11-10
status: Active
---

# ADR-0090: User-level display settings live on a synced UserView in Presentation

## Context

A user asked that finished tasks stay on the My Day schedule, marked as
done, instead of disappearing, as a setting. The maintainer wants it to follow
the user across devices. Until now display settings were either per device
(theme, accent and language in `localStorage`) or a module's own preference
document (`MoneyPreferences`, ADR-0059). Presentation (ADR-0051) already
holds per-user views of shared data (`AreaView`, `ListView`) and names "a
user-level view" as an extension point.

## Decision

We will add `UserView` to Presentation: one per user, id derived from the
user id with a fixed salt, collection `userviews`, synced like the other
views. Its first field is `ShowFinishedOnSchedule` (default `false`), set by
the `ShowFinishedOnSchedule` command. Later user-level display settings that
must follow the user across devices are added as fields here.

## Considered alternatives

- **Per device in `localStorage`, like the theme** — the smallest change, but
  the setting would have to be set again on every device, which the
  maintainer rejected.
- **A field on Identity's `User`, beside the time zone** — no new aggregate,
  but `User` commands need a connection, and Identity would start holding
  how screens look.
- **A `TaskPreferences` document in Tasks** — keeps it near `TodayRule`, but
  the setting changes only what the schedule draws, not which tasks are on
  Today; Tasks would hold a display concern.

## Consequences

The switch works offline and syncs through the existing outbox and delta
sync. A missing `UserView` means every default, so no migration. It adds one
aggregate, one collection to sync, index and purge on account deletion. If
display settings later need to differ per device, `UserView` is the wrong
place for those and they stay in `localStorage`.
