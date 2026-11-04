---
title: Budgets moves to the fourth slot and Portfolio takes the fifth in the phone bottom bar
tags: [ui, navigation, investments]
date: 2026-10-30
status: Active
---

# ADR-0086: Budgets moves to the fourth slot and Portfolio takes the fifth in the phone bottom bar

> Status: Active (amends [0060](0060-budgets-take-the-statistics-slot-and-sidebar-groups-collapse.md))

## Context

The phone bottom bar has five slots: Inbox, Areas, My Day (raised), Goals,
Budgets (`adr/0060`). Investments needs a top-level place on phones. The
maintainer uses Budgets and Investments more often than Goals on the phone.
"Investments" does not fit a slot at 390 px.

## Decision

We will make the bar Inbox, Areas, My Day, Budgets, Portfolio. The fifth
slot opens `/investments` and is labelled "Portfolio"; the page title stays
"Investments". Goals moves into `AccountDrawer` as the first row above
Statistics. On desktop, the sidebar gets an Investments group after
Budgets, built like the Budgets group.

## Considered alternatives

- **Six slots.** Labels truncate at 390 px and targets shrink below 48 px.
- **Investments only in the drawer.** Two taps for a screen the maintainer
  checks daily.
- **Investments as a tab inside Budgets.** Mixes two modules on one page and
  hides private accounts inside shared budgets.

## Consequences

- Goals is two taps away on phones.
- The slot label and the page title differ.
