---
title: Pulled documents never replace a newer local copy
tags: [architecture, offline, sync]
date: 2026-10-25
status: Active (amends ADR-0005)
---

# ADR-0078: Pulled documents never replace a newer local copy

> Status: Active (amends ADR-0005)

## Context

`SyncService` pushes the outbox, then pulls documents from the marker and
saves every row straight into the replica. Since ADR-0076 every own command
echoes a sync nudge, so a pull is often in flight at the moment the user
makes the next edit. A pull that began before a local commit carries the
server's copy from before that commit; saving it overwrote the fresh local
document, and the edit vanished from the screen until the queued command was
pushed and echoed back. Changing a due date twice in a row showed the first
date again for a second or two, which read as "the change did not take".

ADR-0005 resolves conflicts last-write-wins per aggregate, but the client had
no rule for which copy is the later one.

## Decision

We will keep the local document when its `Version` is higher than the pulled
copy's, and take the pulled copy otherwise. `Version` counts applied events
and advances identically on both sides, so a local copy ahead of the server's
is ahead only by commands still queued in the outbox; the next push replays
them, the server's version catches up, and later pulls replace the copy as
before.

## Considered alternatives

- **Skip pulls while the outbox is not empty** — rejected: holds back every
  remote change for as long as any command waits, and an unpushable command
  would freeze the replica.
- **Compare timestamps** — rejected: ADR-0006 keeps clocks out of sync
  decisions; device clocks drift.
- **Re-apply queued commands over the pulled copy** — rejected: the outbox
  stores opaque envelopes with no aggregate id, and replaying handlers
  against a pulled document duplicates work the server does anyway.

## Consequences

- A document the user just changed stays changed on screen while its command
  is in flight, whatever pull completes in between.
- A command the server finally drops (ADR-0021, ADR-0067) leaves the local
  copy ahead by its events until the server's next change to that document
  passes that version. Nothing re-pulls an unchanged document, so this is the
  divergence that already existed, not a new one.
- Equal versions take the server's copy, so a second device's edit that
  lands at the same version still shows until the local command is replayed.
