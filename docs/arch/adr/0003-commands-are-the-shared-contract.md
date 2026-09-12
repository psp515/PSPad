---
title: Commands are the shared client/server contract
tags: [architecture, offline, contracts]
date: 2026-09-11
status: Active
---

# ADR-0003: Commands are the shared client/server contract

## Context

PSPad is offline-first: the Blazor WASM client must accept a change with no
network and reconcile with the server later. If the client and server run
different code to decide whether a change is valid, the two can disagree
about state in ways that are hard to detect and harder to explain to the
user.

## Decision

Command, event and aggregate types live in a project referenced by both the
WASM client and the server. The client applies a command to its local
IndexedDB replica immediately (optimistic), records it in an outbox, and
ships it when the network returns. The server replays the same command
through the same aggregate logic. One model of behavior, not two.

## Considered alternatives

- **Separate client-side validation, server as sole authority** — rejected:
  the client would show optimistic UI that the server might reject for
  reasons the client never checked, producing a worse offline experience
  than checking twice with one set of rules.
- **A dumb client that queues raw intents for the server to interpret** —
  rejected: the client could never show correct optimistic state offline
  without knowing the same rules the server applies.

## Consequences

Client and server can never drift on what a command means, and offline
edits look correct at write time because they are checked by the same
logic that will later run for real. The cost is that the shared project
must stay pure — anything it depends on has to run in a browser sandbox,
which is what ADR-0004 exists to enforce.
