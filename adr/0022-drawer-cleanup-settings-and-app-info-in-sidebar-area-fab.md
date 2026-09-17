---
title: Kill the dead account-menu arrow — Settings and App info become sidebar rows, area actions move to a FAB on the area page
tags: [ui, identity, domain]
date: 2026-09-17
status: Active (amends [0020](0020-history-sidebar-row-and-settings-screen.md); supersedes [ui-redesign-2-design.md](../specs/ui-redesign-2-design.md) D6/D7 for area actions only)
---

# ADR-0022: Kill the dead account-menu arrow — Settings and App info become sidebar rows, area actions move to a FAB on the area page

## Context

ADR-0020 rebuilt the account menu down to "Settings, History, Sign out —
nothing left in it is itself a control to operate, only links to screens
that hold one." In practice the menu's activator (the chevron next to the
avatar and name) stopped opening anything reliable — a dead click target
sitting at the top of every screen's sidebar, reported directly: "there is
arrow down for something to appear but after clicking there is nothing to
appear."

Separately, `specs/ui-redesign-2-design.md` D7 put per-item rename/delete in
a `⋯` menu "on the thing" — for areas, that means a three-dot icon at the
end of every sidebar row. With six-plus areas that is a row of identical
dots repeated down the sidebar, working against the sidebar's one job
(scannable areas, D-something already settled): each row should read as a
plain, tappable destination.

Both problems point the same way: the account menu earns its keep only if
it does something a click can't already do elsewhere, and a sidebar row
earns decluttering if the control it drops has somewhere better to live.

## Decision

We will delete the account-menu dropdown entirely. `AccountMenu.razor`
becomes `AccountBadge.razor`: avatar and name, no chevron, no `MudMenu`, not
clickable. It is now a label, not a control — matching what it actually did
after ADR-0020 (nothing initiated a navigation from it that the sidebar
doesn't already offer).

Settings and a new App info screen (`/app-info` — version, GPL v3 notice,
links to the docs site and GitHub repo) become permanent sidebar rows,
placed **below** the areas list and its own divider — deliberately kept out
of the top tier (My Day / Inbox / Goals / History) so that tier's four-row
budget (ADR-0014, reaffirmed in ADR-0020) is untouched. They are
low-frequency, exactly the same footing Settings already had; the sidebar
now reads as: top tier, areas + **+ New area** (moved inside that same
section, no longer split off by its own divider), then Settings / App info.

Sign out, no longer housed anywhere once the account menu is gone, moves
into `SettingsPage.razor` as a button.

Area rename/delete moves off the sidebar row entirely, onto a `MudFab` on
the area's own page (`AreaBoard.razor`) — the same "New list" FAB already
uses, opposite corner. This is a deliberate, narrow reversal of D6 ("the FAB
is deleted... two routes to the same act, one of which is always in the
wrong corner") and D7 (`⋯` on the thing) for areas specifically: D6's
argument was against a FAB *competing* with an inline creation affordance
on the same screen for the same act — that pressure doesn't apply here,
because area rename/delete isn't a creation action and has no inline
alternative on the sidebar row worth keeping. Lists keep their existing `⋯`
menu (`ThingMenu`, card header) unchanged; this decision is scoped to areas
only.

## Considered alternatives

- **Fix the account menu instead of deleting it.** Rejected: even working,
  it would still be a menu whose only remaining, non-navigational job was
  Sign out — one item does not justify a dropdown, and ADR-0020 already
  established that pattern is wrong for things people set-and-forget or
  check-in-on (Settings, History) versus a menu (Sign out) that stayed
  because nothing else forced the question until now.
- **Put Settings and App info at the top tier, alongside My Day/Inbox/Goals/
  History.** Rejected: would grow the fixed-row budget from four to six,
  which ADR-0020 flagged as exactly the tradeoff to revisit before doing.
  Placing them below the areas divider avoids that renegotiation — they are
  reachable, not competing for the same visual priority as daily-use rows.
- **Keep the `⋯` menu on the area's sidebar row, per D7, and just accept the
  clutter.** Rejected on the reporter's own terms: the row exists to be
  scanned, and a menu trigger on every row defeats that at any area count
  worth having a sidebar for.
- **Give areas their own `⋯` menu on the area page's title, matching lists'
  pattern exactly (no FAB).** This was the technically consistent option and
  was offered; overridden in favor of the FAB by deliberate choice, not by
  default — areas get a different, less consistent affordance than lists on
  their own page.

## Consequences

One less interactive element in every sidebar row and at the top of the
sidebar — closer to the "scannable list of destinations" the sidebar's
purpose. The area page introduces a FAB pattern nothing else in the app
uses for management actions (only for creation, per D6), so a future reader
of `AreaBoard.razor` finds one item that doesn't follow D6/D7's logic; this
ADR is that reader's answer for why. If a `⋯`-menu-on-header pattern is
ever wanted for areas after all (parity with lists), it costs a re-litigate
of this ADR, not a silent divergence.

`AccountMenuTests` is replaced by `AccountBadgeTests`, asserting the badge
carries no menu and no dropdown icon rather than asserting what links it
offers — a smaller, static surface. `NavSidebarTests` drops every test that
exercised `OnRenameArea`/`OnDeleteArea` through the sidebar; that coverage
moves to `AreaBoardTests`, alongside a new assertion that no `ThingMenu`
renders in the sidebar at all.
