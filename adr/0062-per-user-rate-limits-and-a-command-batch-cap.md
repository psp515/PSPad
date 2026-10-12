---
title: Limit each user's API traffic with token buckets and cap a command batch at 50
tags: [http, security, sync]
date: 2026-10-12
status: Active
---

# ADR-0062: Limit each user's API traffic with token buckets and cap a command batch at 50

## Context

`POST /api/commands` looped over a batch of any length, and each envelope
costs a processed-command check, a MongoDB transaction and a sequence
allocation. Kestrel's 1 MB body cap still let one request carry several
thousand small envelopes. Only the anonymous `public` routes and the list
`join` endpoint were rate-limited, so one signed-in user could post such
batches, or call `/api/sync` in a tight loop, and starve MongoDB for every
user on the instance (OWASP A04, issue #127). A wrongly typed field in one
envelope also threw out of `CommandDispatcher`, failing the whole batch with
a 500 and losing the responses of commands already committed, so the client
resent the same batch forever.

## Decision

We will:

- **Cap a batch** at `CommandBatch.MaxEnvelopes` (50) in `PSPad.Contracts`.
  The server answers a longer batch with `400` before dispatching anything;
  the client's outbox peeks the same constant, so the two cannot drift.
- **Rate-limit per user**, partitioned by the JWT subject, with token buckets
  (`RateLimits` configuration section, code defaults):
  - a global limiter on every authenticated request (`Api`: 120 burst, 12/s);
  - named policies on top of it for `/api/commands` (`Commands`: 30 burst,
    3/s), `/api/sync` (`Sync`: 30 burst, 3/s) and both `/api/statistics/*`
    routes (`Statistics`: 60 burst, 6/s — read-only, so it gets more room).
  Anonymous requests skip these buckets; the per-IP `public` policy covers
  them. A refusal is `429` with `Retry-After` in whole seconds.
- **Back off on the client.** `PSPadApiClient` turns a `429` from
  `/api/commands` or `/api/sync` into `ServerBusyException` carrying
  `Retry-After` (5 s when absent). `SyncCoordinator` treats it as a reached
  server, not a failed sync, and retries once after the delay, capped at the
  60-second poll interval.
- **Reject malformed payloads per envelope.** `CommandDispatcher` answers a
  payload that fails to deserialize (`JsonException`) or is missing with the
  existing `Unrecoverable: true` malformed-payload response, so the rest of
  the batch keeps its results.

## Considered alternatives

- **Fixed or sliding windows** — a window lets a user spend the whole quota
  in a burst at the window edge, twice over; a token bucket absorbs the
  client's bursty sync-after-every-command pattern and refills smoothly.
- **Per-IP limits on authenticated routes** — every member of a household
  behind one NAT would share a bucket, and behind a reverse proxy without
  forwarded headers everyone would.
- **Documented self-hoster settings** — the limits stay code defaults, like
  `Sharing:PublicRequestsPerMinute`; a single-household instance has no
  reason to tune them, and tests override them through configuration.
- **A batch cap of 100** — the client never sends more than 50, so a higher
  cap only widens what a hostile caller can do per request.

## Consequences

One user can no longer monopolise MongoDB through the authenticated API,
and a malformed envelope costs only itself. A very long offline queue now
drains at about 50 commands per request and 3 requests a second, roughly
150 commands a second, which is far above what a person produces. A first pull longer than 30
pages (`adr/0061`) backs off and resumes from the marker saved after each page. Clients
built before this change send at most 50 commands already, but they treat a
`429` as a failed sync and show it as such until their next successful
poll. Limiter state is in memory, per API instance; scaling out to several
instances would multiply each user's allowance.
