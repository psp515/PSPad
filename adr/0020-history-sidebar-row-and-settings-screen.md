---
title: History as a sidebar row, theme and sync status into a Settings screen
tags: [ui, identity]
date: 2026-09-16
status: Active (amends [0014](0014-one-navigation-tree-at-every-width.md); supersedes History's placement in [0017](0017-promote-goals-to-a-sidebar-row.md))
---

# ADR-0020: History as a sidebar row, theme and sync status into a Settings screen

## Context

ADR-0014 put Goals and History behind the account menu on one shared
argument: the sidebar's job is areas, and both screens are low-frequency.
ADR-0017 already reversed half of that for Goals, but kept History's half of
the reasoning intact — "History stays in the account menu; its low-frequency
reasoning still holds."

Two things changed since. First, History gained a burndown chart
(`Module.Tasks/Analytics/BurndownRule.cs`, rendered on `HistoryPage.razor`
above the event log) — a screen someone opens to look at something, not just
an occasional audit lookup, the same shift in kind ADR-0017 used to promote
Goals. Second, `specs/ui-polish-design.md` D3/D4 identified that the account
menu had become a dumping ground for anything that did not fit elsewhere:
Theme lived there as a control nested inside a menu item (unreachable by
keyboard in any obvious way), and there was no home at all for sync status or
the time zone picker `SetUserTimeZone` had exposed no caller for since slice
1 — every user silently ran on `Etc/UTC`, which is directly wrong per
AGENTS.md §3's Today rule ("today" is defined in the user's time zone).

A menu built for "Settings, History, Sign out" and a menu built for
"an unreadable `?` holding Theme, Sync status and Sign out" cannot both be
the right shape. One of them had to move.

## Decision

We will give **History** a permanent sidebar `MudNavLink`, at the same tier
as My Day, Inbox and Goals: sidebar order becomes My Day / Inbox / Goals /
History, divider, areas, **+ New area**
(`src/PSPad.App/Layout/NavSidebar.razor`).

We will add a new `/settings` screen (`src/PSPad.App/Pages/SettingsPage.razor`)
holding: Account (avatar, display name, email — read-only, the IdP owns
them), Time zone (`MudSelect` over `TimeZoneInfo.GetSystemTimeZones()`,
saved via `PUT /api/me/timezone`), Theme (System / Light / Dark, per-device
`localStorage`, as ADR-0013 and ADR-0014 already decided — only its screen
moves, not its storage), and Sync (pending outbox command count). This
amends `ui-redesign-2-design.md` D12's placement of the theme control and
D13's placement of History.

The account menu (`src/PSPad.App/Layout/AccountMenu.razor`) is rebuilt down
to navigation only: **Settings**, **History**, **Sign out** — nothing left
in it is itself a control to operate, only links to screens that hold one.

## Considered alternatives

- **Leave Theme and Sync status in the account menu, just restyle it.**
  Rejected: a menu item is transient — opened, acted on, dismissed — which is
  the wrong shape for a toggle someone sets once and rarely revisits, or a
  status someone checks *because* they are wondering whether their edits went
  through. Both want a place that stays put and is scannable at a glance, not
  a control buried one level inside a dropdown with no keyboard-obvious way
  to reach it.
- **Leave History menu-only, per ADR-0017's original call.** Rejected on the
  same evidence ADR-0017 used for Goals: History stopped being a pure log the
  moment it grew a chart. A screen with a visualization worth glancing at
  routinely is a primary destination, not a secondary account action —
  parity with My Day, Inbox and Goals, not with Sign out.
- **A separate `/theme` or `/sync` screen instead of one combined Settings
  screen.** Rejected: none of Account, Time zone, Theme or Sync status is
  substantial enough alone to earn a nav destination of its own, and splitting
  them multiplies the same "where do I find X" problem this decision exists
  to solve.

## Consequences

One more permanent sidebar row. The sidebar now carries five fixed
destinations (My Day, Inbox, Goals, History) plus the divider and the areas
list below it — one row further from the areas a user actually manages day
to day, competing for the same vertical space a long areas list wants. Judged
acceptable here: four fixed rows is still well short of the "three or four
primary destinations" ADR-0014 already treats as the budget for a phone-width
drawer, and the areas list scrolls independently of the fixed rows above it.
If a future addition needs a fifth fixed row, that budget argument is the one
to revisit, not this decision.

Settings becomes a genuine screen with its own tests (`SettingsPageTests`
covering the time zone round trip and the theme buttons) rather than
behaviour folded into `AccountMenuTests`. The account menu shrinks to pure
navigation, so `AccountMenuTests` now asserts what links it offers rather
than what controls it operates — a smaller, more stable surface to test.
Time zone changes go over `PUT /api/me/timezone`, not the offline command
path (per `specs/ui-polish-design.md` D3), so Settings is the one screen in
the app that needs a live connection — a deliberate, narrow exception to
"the app works offline," accepted because it is a rare configuration action
against server-owned state, not routine GTD work.
