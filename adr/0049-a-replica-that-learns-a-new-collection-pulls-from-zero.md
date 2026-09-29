---
title: A replica that learns a new collection pulls from zero, once
tags: [sync, offline]
date: 2026-09-28
status: Active
---

# ADR-0049: A replica that learns a new collection pulls from zero, once

> Status values: `Proposed` (not yet acted on) · `Active` (in force) ·
> `Superseded by ADR-XXXX` (replaced — link the ADR that replaces it, and add
> `Supersedes ADR-XXXX` to the new one's status line if it fully replaces an
> older decision).

## Context

`SyncService.PullAsync` walks `response.Documents` by collection name and
looks each one up in a static `Collections` dictionary
(`areas`, `tasklists`, `todotasks`, `goals`, `inboxes`, `referenceitems`).
An unknown key is skipped with `continue` — the loop still reads every
other collection, advances `replica.SetMarkerAsync(response.Marker)`
unconditionally, and returns.

That `continue` was written for forward compatibility: a server ahead of an
old client sends a collection the old build's `Collections` map doesn't
know, and the old build shouldn't crash. It works for *that* case. It
breaks a second case it wasn't written for: this branch added
`referenceitems` client-side too. A PWA instance already installed before
this release has an old `Collections` map without `"referenceitems"` baked
into its compiled `SyncService`. Its next pull, made before the browser
picks up the new service-worker build, silently skips every
`referenceitems` row in that response and still moves its marker past
them (AD-6: marker is the server's own monotonic `seq`, not a clock, and
nothing re-plays what already passed it). Once the PWA does update and
gains a `Collections` entry for `referenceitems`, its next pull asks
`since={marker}` — a marker already past every reference item that existed
at upgrade time. Those documents never arrive. They would only show up if
edited again, which re-emits them past the marker.

This is not the ordinary skew AD-6 already tolerates (server ahead of an
old client, replica disposable, next full pull fixes divergence) — it's a
false "the client thinks it has already seen this" caused by a marker that
advanced past data the client's own code couldn't parse yet. A disposable
replica does not save it, because the *marker* itself is now wrong, not
just the copied data.

## Decision

We will fingerprint the sync collection set the client compiles against —
`SyncService.Collections.Keys`, sorted, joined into one string
(`CollectionsFingerprint`) — and store it in replica meta alongside the
marker. Before every pull, the stored fingerprint is compared to the
current one:

- Missing or different → pull with `since = 0` once, ignoring the stored
  marker for this pull only.
- Matching → pull with the stored marker, as before.

Every pull response, including the fallback one, writes both the new
marker and the current fingerprint to replica meta. A device holding
nothing yet has no stored fingerprint either, so its very first pull
already asks `since = 0` for the ordinary "new device" reason (`adr/0030`);
the fingerprint check adds no second request in that case; it only adds
one in the "existing device, new collection shipped" case, and only once,
because the stored fingerprint is corrected on that same pull.

## Considered alternatives

- **Bump a schema version and wipe the replica on mismatch** — works, but
  throws away the outbox and every other collection's local copy too, for
  a mismatch that only ever concerns the newly added collection. A full
  `ClearAsync` is the tool for a signed-in-user change (`adr/0018`), not
  for the server shipping one more collection.
- **Never advance the marker past a `continue`d collection** — would need
  `PullAsync` to track "did I skip anything" and cap the stored marker at
  the lowest seq among skipped documents, which the current response shape
  (one flat `Marker` for the whole batch) doesn't expose per collection.
  More invasive than a one-time refetch, for a case that only ever happens
  once per client build.
- **Do nothing, rely on the user editing every stale item to force a
  resync** — the failure mode named in the finding: correct but invisible,
  and it means data a user can see on another device silently doesn't
  reach this one.

## Consequences

Adding a sync collection in the future costs nothing extra: any client
whose compiled `Collections` map doesn't match what it last recorded
re-pulls from zero exactly once, picks up everything including the new
collection, and then resumes normal delta pulls. The cost is one full pull
instead of a delta pull, borne once per client per newly-shipped
collection — cheap next to a `referenceitems` collection or its successors
staying invisible until touched again. It does not fix unrelated marker
skew (a genuine server/replica mismatch from other causes is still AD-6's
problem, solved by AD-6's own "replica is disposable" answer); it only
targets the specific gap where the client's own parsing capability, not
the server's data, was what was behind.
