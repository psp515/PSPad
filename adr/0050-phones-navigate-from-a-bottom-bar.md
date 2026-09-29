---
title: Phones navigate from a bottom bar, not a drawer
tags: [ui, navigation, mobile]
date: 2026-09-29
status: Active
---

# ADR-0050: Phones navigate from a bottom bar, not a drawer

> Status: Active. Amends [ADR-0022](0022-drawer-cleanup-settings-and-app-info-in-sidebar-area-fab.md) and
> [ADR-0023](0023-sidebar-search-removed-footer-added.md) below `md` only.

## Context

Below 960px the whole navigation tree sat in a temporary drawer behind a
hamburger: two taps to reach My Day, Inbox or an area, and the drawer
covered the screen each time. PSPad is a daily driver used mostly on a
phone, and My Day is the screen it exists for.

## Decision

We will give phones (below `md`) a dense top bar (back, title, connection
status, avatar), a bottom bar with Inbox, Areas, My Day, Goals and
Statistics — My Day a raised centre `MudFab` — and a right-hand temporary
`MudDrawer` opened from the avatar holding the account badge, Settings,
App info and the sidebar footer. Areas open the area last used on the
device (`/areas`), and the area screen switches areas with a wrapping row
of chip links. Pages publish their title through `PageHeading`, which
also draws the desktop title. From `md` up the permanent sidebar is
unchanged.

## Considered alternatives

- **Keep the hamburger drawer** — two taps to anything, and nothing says
  My Day is the centre of the app.
- **Avatar opens a `MudMenu`** — enough for two links today, but it leaves
  no home for the sidebar footer and nowhere to grow.
- **A `MudChipSet` for areas** — selection semantics for what is
  navigation, and it makes the New area chip selectable.
- **A horizontally scrolling chip row** — one row at any count, but the
  current area can sit off-screen.
- **`AppShell` clears the title on location change** — a panel's query
  change fires it without re-rendering the page, blanking the title.

## Consequences

- One more navigation surface to keep in step with the sidebar: a new
  top-level screen must be added to both, or consciously left off the
  bottom bar (five slots are full).
- Every routed page must render a `PageHeading`, loading branches
  included, or the top bar shows the previous screen's title.
- The goal screen's status row moves below its heading at every width.
- MudBlazor has no bottom navigation component, so the bar is a
  `MudAppBar Bottom="true"` with a small amount of `pspad-*` CSS.
