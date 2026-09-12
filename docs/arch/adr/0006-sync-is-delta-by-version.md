---
title: Sync pulls deltas by a monotonic version marker, never a clock
tags: [architecture, offline, sync]
date: 2026-09-11
status: Active
---

# ADR-0006: Sync pulls deltas by a monotonic version marker, never a clock

## Context

The offline client (ADR-0003) needs to catch up on server-side changes made
while it was disconnected, without re-downloading everything every time.
Clocks across client and server devices cannot be trusted to agree closely
enough to use as a sync boundary — clock skew would silently drop or
duplicate changes at the edges of a time window.

## Decision

The client pulls every row changed since its own last-known marker and
overwrites its replica with what comes back. The server is the source of
truth; the replica is disposable. The marker is a monotonic counter the
server assigns itself on every write (implemented later, in the MongoDB
design, as a single `counters` document incremented per write) — never a
timestamp.

## Considered alternatives

- **Timestamp-based sync** (`since=<last-seen-time>`) — rejected: clock
  skew between server and client, or between the server's own writes, can
  put two writes on either side of a boundary that a naive `>` comparison
  gets wrong, silently.
- **Full replica re-download on every reconnect** — rejected: works, but
  throws away the point of a local replica for anyone with more than a
  trivial amount of data.

## Consequences

Sync is resumable and cannot silently skip a write, because the marker only
ever moves forward by exactly one write at a time. The cost is that the
server must own and increment this counter transactionally with every write
that touches sync-relevant state — a write that forgets to bump it is
invisible to every client's next pull.
