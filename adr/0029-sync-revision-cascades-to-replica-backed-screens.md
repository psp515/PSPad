---
title: A sync revision cascades from AppShell so replica-backed screens redraw when data lands
tags: [ui, sync, offline]
date: 2026-09-23
status: Active (amended by ADR-0030)
---

# ADR-0029: A sync revision cascades from AppShell so replica-backed screens redraw when data lands

## Context

Every screen that shows user data reads the IndexedDB replica once, in its own
lifecycle method, and keeps what it got. `AppShell` subscribed to
`SyncCoordinator.Changed`, but its handler only refreshed the sidebar's areas
and counts — the routed page underneath was never told anything. Re-rendering a
layout does not re-run a child's `OnInitializedAsync`; the component instance
survives, so the page kept rendering the snapshot it took on mount.

With a warm replica this was invisible: the page rendered the previous session's
data immediately, and a delta arriving seconds later changed little. Two things
made it visible.

ADR-0028 made sign-out genuinely clear the replica. Every sign-in now starts
from an empty one, so the first render of every screen has nothing in it and the
data arrives moments later on the first sync — with nothing to redraw the page.
Signing in appeared to lose all the user's data until they navigated or
reloaded.

The same gap swallowed every cross-device change. The sixty-second poll wrote
new documents into the replica that no open screen ever showed.

## Decision

**`SyncCoordinator` counts pulls.** `Revision` increments when a sync actually
brought documents down, and stands still when a poll found nothing or the device
was offline — a screen must not re-read the replica once a minute for nothing.

**`AppShell` cascades that number around `@Body`** as a named cascading value,
`SyncRevision`. A replica-backed page declares
`[CascadingParameter(Name = "SyncRevision")]` and reloads when the value it last
rendered is not the current one. Pages with route parameters
(`AreaBoard`, `ListPage`) already reload on every parameter set and need only
the declaration.

**Every routable page is classified.** A guard test lists the replica-backed
pages and the rest, asserts the first group declares the parameter, and fails
when a new routable page appears in neither list. A screen that silently
inherits stale data is the failure this ADR exists to prevent, and it is
invisible until someone signs out.

## Considered alternatives

- **Each page subscribes to `SyncCoordinator.Changed` directly** — no cascade,
  no `AppShell` involvement. It puts an event subscription, an `IDisposable` and
  an `InvokeAsync` in every page, and a page that forgets to unsubscribe leaks.
  The cascade is one line per page and Blazor already owns the lifetime.
- **Reload on every parameter set, with no revision check** — simpler pages, no
  `_rendered` field. `AppShell` re-renders on a theme change, a drawer toggle
  and every sync tick, and `@Body` hands the routed page its parameters again
  each time; the page would make an IndexedDB round trip behind all of them.
- **Raise the revision on every sync tick rather than on a pull** — removes the
  `Pulled > 0` test. It reloads every open screen once a minute forever, to
  redraw identical data.
- **Have `AppShell` force the router to remount the page** — no page changes at
  all. It throws away scroll position, expansion state and any half-typed inline
  edit every time a sync lands.

## Consequences

Data that arrives after a screen has rendered now appears on it: the first sync
after a sign-in, and every change made on another device.

Replica-backed pages carry a cascading parameter and a `_rendered` field they
would not otherwise need. The guard test makes that cost explicit rather than
letting a new page quietly skip it.

`SyncCoordinator.SyncNowAsync` is public, so one sync can be run and awaited
without starting the poll loop. `Start()` uses it unchanged.

The revision says data changed, not what changed, so a landed sync reloads the
whole visible screen. For the five screens involved that is one or two
`LoadAllAsync` calls against IndexedDB, which is cheaper than tracking which
aggregates each screen depends on.

`HistoryPage` reads the server's event log and `SearchPage` renders what the
typed query last matched, so neither takes the cascade. Search results can
therefore go stale behind a landed sync until the query is re-run.
