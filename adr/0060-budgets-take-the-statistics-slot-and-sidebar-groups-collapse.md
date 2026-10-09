---
title: Budgets take Statistics' bottom-bar slot, and the sidebar's Budgets and Areas collapse
tags: [ui, navigation, mobile]
date: 2026-10-09
status: Active
---

# ADR-0060: Budgets take Statistics' bottom-bar slot, and the sidebar's Budgets and Areas collapse

## Context

Money (`adr/0059`) adds a top-level destination, Budgets, with several
budgets under it. The phone bottom bar (`adr/0050`) has five fixed slots:
Inbox, Areas, My Day, Goals, Statistics. On the desktop sidebar, areas are
already a long flat list, and adding every budget under it would push
Settings and App info further down. The maintainer wants budgets on the
phone bar, and asked that long sidebar lists expand on click.

## Decision

We will:

- **Phone bottom bar:** make it Inbox, Areas, My Day, Goals, Budgets.
  Statistics moves into `AccountDrawer`, as the first row above Settings and
  App info. `NavTab` maps `/budgets…` to the Budgets slot.
- **Desktop sidebar:**
  - Add a Budgets group after the main rows, as a `MudNavGroup`. Its header
    links to `/budgets`, its chevron toggles it, and its children are the
    active (unarchived) budgets in creation order.
  - Turn the existing Areas section into a collapsible group of the same
    kind.
  - Remember each group's expanded state per device in `localStorage`. Both
    groups default to expanded, and a collapsed header shows its count
    ("Areas · 9").

This amends `adr/0050` (bottom-bar slots) and `adr/0014`/`adr/0022` (sidebar
layout) for these two sections only.

## Considered alternatives

- **Budgets in the account drawer, Statistics keeps its slot.** The
  maintainer uses budgets daily and checks Statistics rarely, so the slot
  goes to the screen that is used more often.
- **A sixth bottom-bar slot.** Below about 360 px wide, labels get truncated,
  and `adr/0050` chose five equal slots on purpose.
- **Never list budgets in the sidebar, only reach them through `/budgets`.**
  That's one click more for the most common action, and it's inconsistent
  with areas.
- **Keep the group state on the server, Presentation-style.** It's a
  per-device layout preference, like the theme. Syncing it would collapse a
  roomy desktop sidebar because of a choice made on a laptop.

## Consequences

- On phones, Budgets is one tap away and Statistics is two (avatar →
  Statistics).
- The sidebar stays short however many areas or budgets exist.
- Expanded state isn't shared across devices. A new device starts expanded.
- Tests that pin the bottom-bar order and the sidebar markup change with
  this decision.
