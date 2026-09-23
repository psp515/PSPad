---
title: Every accepted command triggers a sync, and overlapping runs coalesce
tags: [sync, offline, ui, architecture]
date: 2026-09-23
status: Active (refines ADR-0031's concurrency contract)
---

# ADR-0032: Every accepted command triggers a sync, and overlapping runs coalesce

## Context

A command accepted by the client is written to the replica and queued in the
outbox, and nothing then asks the server to take it. `SyncCoordinator` ran on
three triggers only: the sixty-second poll, `CameOnline`, and `Start`. So a task
ticked off on the phone reached the server somewhere between instantly and a
minute later, depending on where in the poll interval the tick landed, and the
same delay applied in reverse to the other device that wanted to see it. Issue
#37. For a system whose whole promise is that the same account looks the same
everywhere, a minute of silence after a deliberate action reads as a lost edit.

The obvious fix — call `SyncNowAsync()` after every accepted command — was not
safe on the entry point as it stood. `SyncNowAsync` had no re-entrancy guard,
and `SyncService.PushAsync` peeks the outbox and leaves the entries there until
the server's response lands. Two runs overlapping by even a tick would both peek
the same batch and push it twice. With one trigger a minute that never happened;
with a trigger per command, and a poll tick or a `CameOnline` retry able to land
in the middle of a burst, it happens on ordinary use.

The second hazard was quieter. ADR-0031 decided that `Started` is a fresh
`SyncNowAsync()` per `Start` call, so a caller awaiting it is awaiting its own
pull and not one that finished before it was signed in. Any guard that hands a
late caller an already-running task risks handing back exactly the stale task
that ADR-0031 exists to prevent.

## Decision

**Every locally accepted command triggers a sync, and `SyncNowAsync` is the one
guarded entry point through which every trigger goes.** `CommandSender` calls
`ISyncTrigger.SyncNowAsync()` once `result.Accepted` is true — fire and forget,
because the local write is already durable and the caller must not wait on the
network — and a rejected command triggers nothing.

**A caller that arrives while a run is in flight shares that run's task and is
guaranteed one more pass after the current one.** `SyncNowAsync` returns
`_inFlight` and sets `_rerunRequested`; `RunAsync` loops while that flag is set,
clearing it before each pass. Two `SyncService.SyncAsync` calls never run in
parallel, and no caller's own effects are dropped.

**That shape preserves ADR-0031's guarantee, which is about effects and not
about task identity.** A caller's command is in the outbox before it observes
`Accepted`, and the shared task cannot complete until a pass that *began after*
that caller arrived has finished — the rerun flag forces it. "Awaiting your own
pull" therefore still holds in effect; only the literal sentence in ADR-0031's
Decision, that `Started` is a fresh task per call, is now true of `Start` rather
than of every caller of `SyncNowAsync`.

## Considered alternatives

- **Debounce the trigger by a few hundred milliseconds** — one sync per burst
  instead of one per command, at the cost of a timer. Declined when the feature
  was scoped: the delay is the thing being removed, and the coalescing guard
  already collapses a burst without adding latency to the first command in it.
- **Run overlapping syncs in parallel, unguarded** — no coordinator change at
  all, just the call in `CommandSender`. `PushAsync` peeks without removing, so
  the second run resends the batch the first has not yet had acknowledged. The
  server is idempotent by command id and would survive it; the client would be
  spending a round trip per command to prove it.
- **Queue commands to a channel a single sync worker drains** — a proper
  serialization primitive instead of two fields. It is a background loop, a
  lifetime and a shutdown path, to serialize an operation that already has
  exactly one entry point in a single-threaded host.
- **Inject `SyncCoordinator` into `CommandSender` directly** — no new type.
  `CommandSender`'s unit tests would then have to build a coordinator, a
  `SyncService`, an `ISyncApi` and a snackbar to assert that a command fires a
  sync. `ISyncTrigger` is one method and states exactly what the dispatcher
  needs from sync.
- **Await the sync inside `SendAsync` before returning the result** — the caller
  would know its command reached the server. It puts the network in front of
  every UI interaction and contradicts ADR-0005: the local write is the accepted
  one, and the outbox exists precisely so the trip can happen later.

## Consequences

A command reaches the server as soon as the device can carry it, and the other
device sees it on its next pull rather than up to a minute after that. The
Today screen stops being a minute stale on the device that just changed it.

The poll, `CameOnline` and `Start` all call the same guarded entry point, so
they inherit the guard without changing: the poll tick that lands mid-command no
longer double-pushes, which was a latent bug before this branch gave it a way to
happen often.

The guard is correct only because Blazor WebAssembly runs single-threaded.
`_inFlight` and `_rerunRequested` are plain fields with no lock and no
`Interlocked` — check-then-assign is atomic here only because nothing can
interleave between the two. Running `SyncCoordinator` on a multi-threaded host
would require revisiting this, and the comment in the file says so.

A burst of N rapid commands still costs more than one pass: the run in flight
finishes, then one follow-up covers everyone who arrived during it, and anyone
arriving during *that* pass earns another. It is bounded by the number of
overlapping arrivals, not by N, and never by parallelism.

ADR-0031's Decision sentence about `Started` being a fresh task per call now
describes `Start` alone. The property it was protecting — a caller never awaits
a pull that finished before it existed — is preserved by the rerun flag, and
ADR-0031 stays Active.
