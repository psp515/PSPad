---
title: Every Start hands back its own pull, because the shell mounts once signed out and once signed in
tags: [sync, identity, ui, offline]
date: 2026-09-23
status: Active (amends ADR-0030)
---

# ADR-0031: Every Start hands back its own pull, because the shell mounts once signed out and once signed in

## Context

ADR-0030 has `AppShell` hold the brand splash until `SyncCoordinator.Started`
completes on a device holding nothing for the signed-in user. It did not engage
in the running application: after a sign-in the app drew itself empty and the
data appeared a minute later, on the poll. An incognito window reproduced it,
so no stale service worker or cached bundle was involved.

`AuthorizeRouteView` renders `<NotAuthorized>` inside the route's layout. The
signed-out visitor's `RedirectToLogin` is therefore rendered *inside*
`AppShell`, which runs its whole boot sequence — including `Coordinator.Start()`
— before anyone has signed in. `Start` was latched on `_started`: the first call
set `Started` to that anonymous pull and every later call returned without
touching it.

So the sign-in went: anonymous shell starts the coordinator, its pull finishes
with no session behind it, the shell is torn down by the redirect to `/welcome`;
the user signs in; a new shell mounts, finds an empty replica, and awaits a
`Started` task that completed minutes ago. The splash never held, the empty app
was drawn, and the sixty-second poll — still running from the anonymous start —
delivered the data later. This also explains why a hard reload fixed it: a
reload boots with the session already stored, so the first and only `Start` is
the signed-in one.

## Decision

**`Start` is idempotent about the poll loop and not about the pull.** The
`_started` latch keeps `LoopAsync` and the `CameOnline` subscription to one
each, and `Started` is assigned a fresh `SyncNowAsync()` on every call. A caller
that awaits `Started` after calling `Start` is awaiting its own pull, not
someone else's.

## Considered alternatives

- **Keep the route off `AppShell` for anonymous visitors** — `App.razor` would
  give `<NotAuthorized>` its own layout, and the shell would never mount signed
  out. It fixes this instance and leaves the trap: any second mount of the
  shell, for any reason, still gets a stale `Started`. The latched pull is the
  defect, not where it was observed.
- **Have `AppShell` call `SyncNowAsync()` directly instead of awaiting
  `Started`** — no coordinator change at all. `Start` fires its own pull
  regardless, so the shell would run two concurrent syncs against the same
  marker on every boot.
- **Reset the coordinator on sign-in** — an explicit `Reset()` the sign-in path
  calls. It adds a lifecycle step that every future entry point must remember,
  to restore a property that should never have gone stale.
- **Skip `Start` when signed out** — the shell would only start the coordinator
  once it has a session. Sync while signed out is harmless and the outbox count
  it refreshes is wanted; making correctness depend on the shell's auth check
  ordering is how this bug happened in the first place.

## Consequences

The splash now holds for the first pull after a sign-in, which is what ADR-0030
decided and did not achieve. The user sees their data on the first screen they
are shown.

A boot runs one pull per `Start` call, so the ordinary signed-out-then-signed-in
sequence runs two: one anonymous, one authenticated. The anonymous one has no
bearer token and pulls nothing, and it refreshes the pending-outbox count the
shell displays, so it is not wasted.

`SyncCoordinator` no longer promises that `Started` refers to the *first* pull —
only to the pull its caller's `Start` began. ADR-0030's reasoning is unchanged;
this is the mechanism it needed.
