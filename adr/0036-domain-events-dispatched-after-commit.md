---
title: Domain events are dispatched asynchronously after commit and replayed from a marker on startup
tags: [architecture, events, persistence]
date: 2026-09-24
status: Active
---

# ADR-0036: Domain events are dispatched asynchronously after commit and replayed from a marker on startup

## Context

Nothing consumed domain events. `events` was written by `MongoUnitOfWork.CommitAsync`
and then only ever read back as rows — the History module read the raw log and
mapped each `StoredEvent.Type` through a string dictionary. Statistics needed
to become a real consumer: something has to notice "a task was completed" and
turn it into a record, without becoming a second write path inside `Tasks`.

## Decision

`MongoUnitOfWork.CommitAsync` publishes the committed events'
`DomainEventEnvelope`s (`(Seq, DomainEvent)`, the `seq` already assigned
inside the same transaction) through `IDomainEventDispatcher` **after**
`session.CommitTransactionAsync` returns, never inside the transaction. The
default implementation, `ChannelDomainEventDispatcher`, writes to a bounded,
single-reader `Channel<DomainEventEnvelope>`. One `DomainEventPump :
BackgroundService` reads it: on startup, before draining the channel at all,
it resolves `IDomainEventReplay` in its own DI scope and calls
`CatchUpAsync`, which reads `statistics_state.lastProcessedSeq`, replays
`events` forward from there in batches through every registered
`IDomainEventHandler`, and advances the marker after each batch. Only then
does the pump start draining the live channel, resolving a fresh scope and
every `IDomainEventHandler` per batch.

A handler that throws is caught and logged by the pump
(`src/shared/PSPad.Infrastructure/Events/DomainEventPump.cs`) — one bad
handler does not take the service down. A dispatch failure at the point of
publish (the channel write itself) is likewise caught and logged inside
`MongoUnitOfWork.CommitAsync` — the transaction already committed, so a
publish failure must never turn an accepted command into a failed one.

## Considered alternatives

- **Synchronous dispatch inside the write transaction.** Rejected: it puts
  a second module (Statistics) in the Tasks write path — every task write
  now depends on Statistics being up and fast — and it cannot backfill
  events that already exist in an established database, which this feature
  needs on its very first boot.
- **A polling projector**, scanning `events` on a timer instead of reacting
  to a live feed. Rejected: it adds a staleness window that has to be
  explained on the statistics screen, and it is a second background process
  that can silently stop advancing with nothing louder than a stale number
  to notice it by.
- **Fire-and-forget with no replay.** Rejected: an existing installation's
  database would show empty statistics forever, with no path to backfill,
  and a crash between commit and handler execution would lose that record
  permanently with no way to repair it.

## Consequences

Statistics lag a command by however long the channel takes to drain —
typically milliseconds, but there is no read-your-writes guarantee: a
client that completes a task and immediately reloads the statistics screen
can see the old numbers. The marker is global (`statistics_state`, one
document), not per user, so replay after a crash re-walks every user's
events since the marker, not just the affected one. A slow handler cannot
slow down a write — the publish happens after commit, and the pump
processes the channel independently of request latency — but by the same
token nothing upstream of the pump can apply backpressure to a command
beyond the channel filling up (1024 capacity, `Wait` on full).

**The marker is advanced only by startup replay; the live drain loop never
writes it.** This is deliberate and load-bearing, not an oversight: because
the pump catches and logs a throwing handler rather than crashing, a bad
handler must not be able to make a swallowed failure permanent and silent.
If the live loop advanced `lastProcessedSeq` as it went, an event whose
handler threw would still move the marker past it, and that event would
never be retried. Leaving the marker exactly where the last replay left it
means every event dispatched live — succeeded or not — gets replayed again
from `statistics_state.lastProcessedSeq` the next time the process starts,
and only that replay pass is allowed to advance it. The cost is that replay
work grows with how long the process stays up between restarts: a
long-running instance that never restarts never checkpoints anything past
its first boot's replay, so its next boot (deploy, crash, restart) replays
everything dispatched live in between.
