---
title: Sync pulls documents in bounded seq pages and never ships the event log
tags: [architecture, offline, sync, security]
date: 2026-10-12
status: Active
---

# ADR-0061: Sync pulls documents in bounded seq pages and never ships the event log

> Status: Active (amends [ADR-0006](0006-sync-is-delta-by-version.md))

## Context

`GET /api/sync` answered with every document and every event newer than
`since`, inside one snapshot transaction (#128). A new device, or a stale
collections fingerprint, asks from `0`: the API loads the user's whole event
history with full payloads into memory, and the transaction can outlive
MongoDB's 60 s snapshot lifetime. Every retry asks for the same window, so
sync for that user fails forever.

The events in the response were never read: the client builds its replica
from documents alone (ADR-0011), and history and statistics are served by
their own endpoints from the server-side log. Events also grow with every
change ever made, documents only with live data.

## Decision

Sync returns documents only. Each response is one page: every source
collection is read in `seq` order up to `limit` rows (default and maximum
1000). If any source fills its limit, the page ends at the lowest last
`seq` among the full sources, every source is cut at that `seq`,
`hasMore` is `true` and the marker is that `seq`. Otherwise `hasMore` is
`false` and the marker is computed as before. Rows asked for through
`full=` come back whole on every page, outside the limit.

The client keeps pulling from each page's marker until `hasMore` is
`false`, saving each page and its marker before asking for the next.
Membership reconciliation runs once, after the last page.

## Considered alternatives

- **Page events and documents together** — rejected: keeps sending the
  largest, fastest-growing payload to a client that discards it.
- **Raise the transaction lifetime or stream one huge response** — rejected:
  memory and time still grow with the user's whole history.
- **Page by count across a merged stream of all collections** — rejected:
  needs one sorted cursor over 13 collections; cutting every source at the
  lowest full boundary gives the same no-gap guarantee with plain indexed
  queries.

## Consequences

A pull from `0` costs at most `limit` rows per collection per request, each
in its own short transaction, and an interrupted pull resumes from the last
saved page instead of starting over. The replica can be briefly
inconsistent between pages (a task before its list); it settles when the
last page lands, and LWW (ADR-0005) is unchanged because each document
still arrives in its newest version. A client that wants events back needs
a new endpoint; the sync contract no longer has them. Older clients ignore
`hasMore` and simply finish the remaining pages on their next sync.
