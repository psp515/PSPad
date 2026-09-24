---
title: History renamed to Statistics
tags: [ui, architecture, domain]
date: 2026-09-24
status: Active (amends [0020](0020-history-sidebar-row-and-settings-screen.md))
---

# ADR-0038: History renamed to Statistics

## Context

ADR-0011, ADR-0020 and AGENTS.md's original §2 all named the module and
screen "History": a browsable log of what happened, with a burndown chart
bolted on. ADR-0036 and ADR-0037 changed what the module actually is — a
bounded context with its own records, its own projection, tiles, four
charts and a consistency heatmap, not a log viewer. "History" undersold it
and, worse, collided with the everyday meaning of the word once the screen
stopped being primarily a log and became primarily a set of visualizations
with a record feed underneath them. The module, the route, the sidebar
label and the endpoint prefix all still said "History" and needed to agree
with what the code now does.

## Decision

`PSPad.Module.History` is renamed `PSPad.Module.Statistics`. The route
becomes `/statistics`; `/history` is kept as a client-side redirect to
`/statistics` so existing PWA bookmarks and any device with the old route
cached still land somewhere valid, rather than 404ing. The sidebar
`MudNavLink` is relabelled from History to Statistics (its position in the
nav group, between Goals and the divider, is unchanged from ADR-0020). The
HTTP surface moves from `/api/history` to `/api/statistics/records` and
`/api/statistics/overview`; `/api/history` no longer exists — there is no
server-side redirect for it, only the client route redirect above, since
nothing other than this app's own client called it.

## Considered alternatives

- **Keep the "History" name, let the screen's scope grow underneath it.**
  Rejected: "History" describes a feed, and the feed is now one section of
  four among tiles, four charts and a heatmap. A name that undersells most
  of a screen's content is worse than a rename, and the rename is cheap —
  one route, one label, one endpoint prefix, all changed together in the
  same piece of work that built the new screen.
- **Keep `/api/history` live as a server-side alias alongside
  `/api/statistics/*`.** Rejected: nothing external ever depended on it —
  it existed only for this app's own client, which moves to the new paths
  in the same deploy — so an alias would be dead code kept "just in case"
  with no case that actually needs it.

## Consequences

Amends ADR-0020: every place that ADR described as "History" (the sidebar
row, the `/history` route) now reads "Statistics" at `/statistics`; ADR-0020's
reasoning for giving the screen a permanent sidebar row (parity with My Day,
Inbox, Goals — a primary destination, not an account-menu afterthought)
still holds unchanged, only the label moved. `PSPad.Module.History`'s entry
in ADR-0010's module list is now wrong; ADR-0010's status line is updated to
point here rather than its body being edited, per this repo's own rule
against rewriting a past ADR's Decision after the fact. Any external
bookmark, script or shortcut hitting `/api/history` breaks outright — judged
acceptable since it was never a public API, only this app's own client.
