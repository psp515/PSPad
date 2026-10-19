---
title: Open clients get a data-free sync nudge over Server-Sent Events, fanned out from commit
tags: [architecture, sync, http, sharing, deployment]
date: 2026-10-14
status: Active
---

# ADR-0076: Open clients get a data-free sync nudge over Server-Sent Events, fanned out from commit

## Context

An open client learns about a change made elsewhere only on its next sync:
the 60-second poll, app start, reconnect, or a command of its own
(ADR-0032). With shared lists (ADR-0054), shared budgets (ADR-0066) and
public snapshot marks (ADR-0064) the gap is visible: a visitor's tick on a
snapshot reaches the owner's open app up to a minute later. Issue #105.

Sync is the one data path (ADR-0006, ADR-0061) and the replica is
disposable. Snapshot marks are unlogged events, so the post-commit event
pump (ADR-0036) never sees them. The API runs as one container with no
message bus. The access token travels in an `Authorization` header that the
browser's `EventSource` cannot set. The design is in
`specs/live-updates-design.md`.

## Decision

We will push a nudge that carries no data to every open client whose data a
commit touched, and the client will answer it with its existing guarded
`SyncNowAsync()`.

- **Transport:** Server-Sent Events on `GET /api/live`, written by our own
  code on the server and read by our own code on the client through
  `HttpClient` browser streaming, so the bearer token stays in the header.
  No SignalR, no WebSocket, no new package.
- **Source:** `MongoUnitOfWork.CommitAsync` calls `ICommitObserver` after
  the transaction commits, for logged and unlogged commits alike. A failing
  observer is logged and never fails the command.
- **Audience:** the actor, the owner, and the members of the list or budget
  the aggregate is or was in, plus members a commit removed. It mirrors
  `SyncReader`'s visibility rules.
- **Delivery:** an in-memory per-user map of streams, each with a
  capacity-1 drop-write channel, so bursts collapse into one nudge.
- **Lifetime:** a heartbeat every 25 s; the server ends a stream at the
  token's expiry or after 15 minutes; the client reconnects with
  exponential backoff and jitter, pauses while offline or hidden, and syncs
  once on every connect.
- **The poll stays.** A missed nudge falls back to the 60-second poll.

## Considered alternatives

- **Shorter poll interval** — no new moving part, but it costs battery and
  server load on every client all the time, and still lags.
- **Push data instead of a nudge** — a second data path beside sync, with its
  own authorization and ordering. Rejected up front (AD-6).
- **SignalR** — reconnects and transport fallback for free, but a client
  package and a protocol for a message with no data. Over WebSocket the token
  goes into the query string, and the CSP needs a `wss:` source.
- **Raw WebSocket** — the same token-in-URL problem, plus all the plumbing we
  would write for SSE anyway.
- **`EventSource` with a token in the query string or a cookie** — puts a
  bearer token in proxy logs, or adds a cookie auth path the API does not
  have.
- **Fan out from the domain event pump** — already asynchronous and replayed,
  but it never sees unlogged events, so snapshot marks, the first consumer,
  would not nudge anyone.
- **MongoDB change streams** — cover every write including account deletion
  and work across instances, but every change document needs a lookup to find
  its audience, and the stack runs one API instance. Kept as the path to
  scale-out.
- **Nudge every connected user on every commit** — no audience logic, but it
  leaks activity timing across users and costs every client a pull per write
  anywhere.

## Consequences

Shared lists, shared budgets, snapshot marks and a user's other devices
update within a round trip instead of up to a minute. Sync, the replica and
the outbox are unchanged; a client with live updates broken behaves exactly
as before.

Stock nginx settings were tested and need no change; Cloudflare and Caddy are
expected to need none from their documented SSE behaviour but were not tested.
The response sends `Cache-Control: no-cache, no-transform` and
`X-Accel-Buffering: no`, and the heartbeat stays under Cloudflare's 100 s idle
limit. A proxy with a read timeout under 30 s, ignored accel headers,
caching on the API, gzip for `text/event-stream`, or HTTP/1.1-only TLS needs
the change listed in the spec and `install.astro`.

Each visible tab holds one long-lived request (hidden tabs drop their stream).
On an HTTP/1.1-only proxy that is one of the browser's six connections per
host.

There is no global cap on open streams. The exposure is authenticated users ×
`Live:MaxStreamsPerUser`, which is acceptable for a self-hosted install.

The audience is a second statement of who can read what, beside `SyncReader`.
The audience tests are hand-written expectations, not a derivation from
`SyncReader`; a visibility change must update `LiveAudience` and its tests
together with `SyncReader`.

Former members after a stop-sharing (the event carries no member list),
account deletion, and backfills nudge nobody; those clients catch up on the
poll.

Fan-out is in memory. A second API instance needs a backplane, such as
change streams, before nudges cross instances. The poll keeps that case
working, only slower.

The device that sent a command receives its own echo and runs one extra pull.
