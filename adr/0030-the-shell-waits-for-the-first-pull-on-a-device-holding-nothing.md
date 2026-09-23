---
title: The shell waits for the first pull on a device holding nothing for this user
tags: [ui, sync, offline, identity]
date: 2026-09-23
status: Active (amends ADR-0029; mechanism corrected by ADR-0031)
---

# ADR-0030: The shell waits for the first pull on a device holding nothing for this user

## Context

ADR-0029 gave replica-backed screens a way to hear that a sync had landed, so a
page rendered from an empty replica would redraw once its data arrived. That is
necessary and it was not sufficient.

The window it leaves open is the one every sign-in goes through. ADR-0028 made
sign-out clear the replica, so a sign-in starts from nothing: `AppShell` marks
itself ready, the routed page reads an empty replica, and the user is shown a
complete, confident, empty application — "Nothing due today" over a full
account. The data appears later, if the notification arrives. Anything that
stops it arriving leaves the empty screen standing: a pull that fails and is
swallowed, or a service worker still serving a build without the redraw.

Being right *eventually* is the wrong shape for this moment. The screen that
says a user has no tasks is indistinguishable from the screen that says their
data has not arrived yet, and the first one is a lie the app tells confidently.

## Decision

**A device holding nothing for this user waits for the first pull before the
app is drawn.** `ReplicaOwnership.NothingSyncedYetAsync` reports a sync marker
of zero — nothing has ever been pulled here for this user, as opposed to merely
being behind. When that is true, `AppShell` keeps the brand splash up, awaits
`SyncCoordinator.Started`, reloads, and only then sets itself ready.

**`SyncCoordinator.Start` hands back its first pull** as `Started`, so the shell
awaits a task rather than hoping for a notification. The poll loop and the
`CameOnline` retry are unchanged.

**The wait is bounded at ten seconds and swallows its own failures.** A held
splash is an unrecoverable blank screen: a pull that never answers still has to
let the app through, for the same reason `SessionBootstrapper` caps its own
boot. Offline, `SyncNowAsync` returns at once and nothing is held at all.

**A device that has pulled before never waits.** That is the whole point of the
local replica, and ADR-0027 is unchanged for it: a relaunch opens on stored
data, server or no server.

## Considered alternatives

- **Rely on ADR-0029's redraw alone** — no boot change, no network in the
  critical path. It makes correctness depend on a notification arriving, and
  shows a confidently empty app until it does. This was tried first and is what
  this ADR exists to correct.
- **Render the app and show a loading indicator over the data regions** —
  honest about the state without holding the whole shell. It needs every
  replica-backed screen to grow a "syncing" state distinct from "empty", which
  is five screens paying for a case that lasts one pull.
- **Always wait for a pull at boot** — one code path, no marker check. It puts
  a network round trip in front of every launch and breaks ADR-0027's promise
  that an installed app opens on local data with no server at all.

## Consequences

Signing in shows the splash until the data is there, then the app with data in
it. The user never sees their account rendered as empty.

A first sign-in against an unreachable server waits up to ten seconds before
showing the empty app. ADR-0028's reachability toast fires alongside it, so the
emptiness is at least explained.

`ReplicaOwnership` answers a question about sync progress as well as ownership.
The marker is the honest source for "does this device hold anything for this
user", and splitting it into another type to keep the name pure would buy
nothing.

`AppShell`'s two replica loads now share one guarded helper, so a throwing
document store cannot escape `OnInitializedAsync` from either of them.

ADR-0029's cascade stays, and stays necessary: it carries every sync after the
first, which is how a change made on another device reaches an open screen.
