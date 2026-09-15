---
title: One navigation tree at every width, revealed rather than rebuilt
tags: [ui, offline, identity]
date: 2026-09-12
status: Active (supersedes [0013](0013-responsive-shell-and-per-device-theme.md); Goals' placement superseded by [0017](0017-promote-goals-to-a-sidebar-row.md))
---

# ADR-0014: One navigation tree at every width

## Context

ADR-0013 put app sections and user areas on separate navigation surfaces at
every width: a sidebar plus an area chip row at `md`+, a bottom bar plus an
area bottom sheet below it. Its reasoning was that areas are unbounded user
data while sections are a fixed set of five, and that a phone offers room for
three or four primary destinations — so any surface carrying both would cap
how many areas a user may usefully have, silently.

Living with the result showed the cost. Two navigation trees exist and are
maintained separately; a new section must be added to both. The phone layout
is not the desktop layout at a different size, it is a different information
architecture, so a change to one is a judgement call about the other every
time. Nothing shows an area's lists together, because the chip row carries
names and nothing else. And the split FAB, the only way to create anything,
puts the control for "add a task to this list" in the opposite corner from the
list.

The premise about slot limits was correct, but it was a premise about *bottom
bars*, and it was applied to navigation in general.

## Decision

We will render one navigation tree at every width: account, search, My Day,
Inbox, the user's areas, and **+ New area**. At `md` and above it is a
permanent sidebar; below `md` it is the same component as a temporary drawer
behind a hamburger in the app bar.

An area opens its own screen of list cards at `/areas/{areaId}` rather than
expanding inside the sidebar.

The theme preference remains per-device in `localStorage`, exactly as ADR-0013
decided, and moves from the app bar into the account menu. That part of 0013
is restated here unchanged, not reversed.

## Considered alternatives

- **Keeping ADR-0013's split.** Rejected on evidence rather than on argument:
  the decision cost two trees and delivered a phone layout that could not
  mirror the desktop one, and the slot-limit problem it solved does not exist
  in a drawer, which scrolls and has no fixed number of positions.
- **A single merged sidebar that expands areas into their lists**, as the
  ADR-0013 record notes Microsoft To Do does. Rejected here for a different
  reason than 0013 rejected it: it is now mirrorable, but two levels of tree
  inside a drawer that is itself temporary on a phone reads badly, and an
  area's lists have a better home in a screen that can also show their work.
- **A permanent third pane for task detail.** Rejected. It reflows every
  screen at every width in exchange for keeping visible a list the user has
  just clicked away from. An overlay addressed by `?task={id}` gives the back
  button and a linkable task for less.
- **Keeping the FAB alongside inline creation.** Rejected: two routes to the
  same act, one of which is always in the wrong corner.

## Consequences

One tree to maintain. A new section is added once. The phone and the desktop
are the same information architecture at two sizes, so a layout change no
longer requires deciding what its counterpart should be.

Areas are now unbounded in the navigation itself, not only in the domain — a
user with thirty areas scrolls the drawer, which is what a drawer is for.

My Day and Inbox cost two touches on a phone instead of one, because the
drawer must be opened first. That is the accepted price, and it is the one
thing here that could turn out wrong: if opening the drawer for the hottest
path becomes the daily irritation, this decision has to be superseded and a
bottom bar reintroduced for those two destinations alone — not for areas,
whose count was never the real problem.

Both navigation branches still render into the DOM at all times and are
separated only by CSS, so the switch is flash-free and bUnit still cannot
assert which one a user sees. Breakpoint behaviour stays verified by hand, as
ADR-0013 established.

No command, event, aggregate or sync path changes to deliver this, so AD-3
and AD-4 are untouched: `PSPad.Module.Tasks` still compiles to WebAssembly
with no infrastructure reference. The one behavioural addition, search, filters
the local IndexedDB replica and adds no endpoint and no Mongo index.
