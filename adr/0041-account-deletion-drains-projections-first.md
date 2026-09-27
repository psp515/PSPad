---
title: Account deletion drains the domain event pump before wiping
tags: [identity, persistence, events, security]
date: 2026-09-26
status: Active
---

# ADR-0041: Account deletion drains the domain event pump before wiping

> Amends [ADR-0034](0034-account-deletion-bypasses-the-command-pipeline.md)
> (the wipe) and [ADR-0036](0036-domain-events-dispatched-after-commit.md)
> (asynchronous dispatch).

## Context

ADR-0034 wipes every collection's `userId` documents in one transaction.
ADR-0036 then made Statistics projections asynchronous: a command's events
are published to an in-process channel after its transaction commits, and a
background pump writes `statistics_*` documents later. A user who deletes the
account seconds after a change can have that change's projection land
*after* the wipe, leaving Statistics documents for a user who no longer
exists. CI's `DeletingTheAccountRemovesEveryDocumentForThatUser` caught it as
an intermittent failure.

## Decision

We will make `DELETE /api/account` wait until the pump has handled every
envelope published before the request started, then wipe. The dispatcher
counts envelopes published and handled; `DrainAsync` waits until the handled
count reaches the published count it saw on entry. The pump counts an
envelope handled after all its handlers ran, whether they succeeded or threw.
The wait is capped at 10 seconds, after which the wipe runs anyway and the
timeout is logged.

## Considered alternatives

- **Projections skip events for users that no longer exist** — adds a read to
  every projected event, and the check and the write still race.
- **Wipe twice, before and after a delay** — a guess at timing, not a
  guarantee.
- **Route the wipe through the pump as a special work item** — gives the same
  ordering, but mixes a request/response operation into a fire-and-forget
  channel of domain events and complicates the pump's contract.

## Consequences

- No projection can write for the user after the wipe, for everything
  committed before deletion began.
- Deletion now waits for the pump's backlog. Normally that is milliseconds; if
  the pump is still replaying on startup, deletion waits up to the 10-second
  cap and then wipes, and a projection may still land afterwards. It is logged,
  not silent.
- A command from the same user that commits *during* the deletion request is
  not waited for. That needs the user to act in another tab while confirming
  deletion by typing their email.
