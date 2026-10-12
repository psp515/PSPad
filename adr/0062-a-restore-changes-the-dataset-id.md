---
title: A restore changes the dataset id, and a client that sees a new id re-pulls from zero
tags: [sync, persistence, offline]
date: 2026-10-12
status: Active (amends [0006](0006-sync-is-delta-by-version.md))
---

# ADR-0062: A restore changes the dataset id, and a client that sees a new id re-pulls from zero

## Context

Restoring a backup (ADR-0061) rolls `counters.events` back to the value it
had when the dump was taken. Every client that synced after that point
holds a marker above it.

Delta sync (ADR-0006) then asks for `seq > marker`, which returns nothing
until new writes overtake the old marker. The replica also keeps every
document created or changed after the backup, which the server no longer
has.

The replica is meant to be disposable and the server is meant to be truth.
Neither holds if the client can't tell that the server's dataset was
replaced. The collections fingerprint (ADR-0049) already forces a pull
from zero once, but only on a client-side change.

## Decision

We will keep a dataset id on the server, one `meta` document
`{ _id: "dataset", value: Guid }`, created on first read, and return it on
every `GET /api/sync` as `SyncResponse.DatasetId`. The restore script
deletes it, so the restored instance mints a new one.

A client stores the id in replica meta. On a mismatch it discards that
response, clears every replica collection and its marker, and keeps its
outbox and recorded owner. It records the new id, and pulls with
`since = 0`. A client with no stored id records the one it receives
without purging.

## Considered alternatives

- **The restore script bumps `counters.events` above the old maximum.**
  Rejected: clients would start pulling again, but they'd keep phantom
  documents created after the backup, and nothing on the server could
  delete them.
- **Tell users to clear site data after a restore.** Rejected: it's easy
  to miss on one device of several, and that device keeps showing data
  that no longer exists.
- **Derive the id from the data, for example a hash of the oldest event.**
  Rejected: a restore of a recent backup keeps the same oldest event, so
  it wouldn't change the id.

## Consequences

- After a restore, every client re-syncs from scratch on its next pull,
  without anyone touching a device.
- Unsent outbox commands replay against the restored server under
  last-write-wins (AD-5). Those that refer to aggregates the restore lost
  come back rejected and are surfaced, never dropped.
- A full pull per device after a restore costs as much as a first sign-in
  (see #128 for paging).
- The id only changes if the restore goes through `restore.sh`. A raw
  `mongorestore` brings the old id back, and clients won't notice. The
  docs name the script as the only supported restore.
- The sync contract gains a field. Older clients ignore it and keep
  today's behaviour.
