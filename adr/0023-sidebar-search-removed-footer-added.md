---
title: Pull local search out of the sidebar for now, give the drawer a footer
tags: [ui]
date: 2026-09-17
status: Active (amends [0022](0022-drawer-cleanup-settings-and-app-info-in-sidebar-area-fab.md); temporarily supersedes `ui-redesign-2-design.md`'s local-search decision)
---

# ADR-0023: Pull local search out of the sidebar for now, give the drawer a footer

## Context

`specs/ui-redesign-2-design.md` settled local search as part of the current
client design, entered from a field pinned to the top of the sidebar
(`NavSidebar.razor`), just below the account row, navigating to `/search`.
Follow-up work on the drawer (ADR-0022) wants that top-of-sidebar space back:
with the search field gone, the account badge (now non-interactive per
ADR-0022) can take more breathing room, and the drawer gains room for a
footer.

Separately, the drawer had no footer at all — nothing anchored to its
bottom regardless of how many areas were listed above.

## Decision

We will remove the search field and its wiring from `NavSidebar.razor`
entirely: no input, no `OnSearchKey`, no `NavigationManager` injection. The
`/search` page and route are untouched — only the drawer's entry point to it
is gone. This is a deliberate, temporary gap: search is not deleted as a
feature, it is unreachable from the UI until it gets a new home (not decided
here).

`AccountBadge`'s padding grows from `py-2` to `py-4` on the full (non
mobile-app-bar) variant, now that the field below it is gone.

The drawer gains a footer, pinned to the bottom via a `flex-grow-1` spacer
above it: the current date and time (client-local, refreshed every minute
via a `PeriodicTimer`) and a `GPL v3` caption line.

## Considered alternatives

- **Move the search field somewhere else in the same change (e.g. the app
  bar) instead of just deleting it.** Rejected for now: no destination was
  chosen yet, and picking one under this change would be a UI decision made
  in passing rather than a deliberate one. Tracked as unfinished, not
  abandoned.
- **Leave the field in place and only add the footer below it.** Rejected:
  the space it frees is exactly what makes the wider account badge and a
  real footer fit without growing the drawer's fixed rows further, per
  ADR-0022's already-stated budget concerns.

## Consequences

Nobody can reach `/search` from the client UI until it is given a new
entry point — a real gap, not a rounding error, and this ADR is where that
gap is recorded so it doesn't get silently forgotten. `NavSidebarTests`
gains a test asserting no search input renders; the old field had no
dedicated test coverage to remove.
