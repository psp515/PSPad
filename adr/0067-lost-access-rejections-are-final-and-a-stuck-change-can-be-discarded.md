---
title: Rejections for a gone or foreign target are final, and a stuck change can be discarded
tags: [offline, sync, contracts, sharing]
date: 2026-10-13
status: Active
---

# ADR-0067: Rejections for a gone or foreign target are final, and a stuck change can be discarded

## Context

ADR-0021 dropped only the four structural rejections from the outbox and
kept every domain rejection queued, retried on each sync, on the reasoning
that a later server state might accept it. The outbox ships in order and
stops on the first rejection, so a domain rejection that can never succeed
blocks every change queued behind it, forever (issue #159).

Sharing made this common. A member removed from a shared list or budget
while they still had changes queued for it gets "That list belongs to
somebody else." or "That budget does not exist." on every sync. The next
pull purges the list or budget from their device, so the change they are
stuck behind is not even visible any more. The same holds for a change to a
task, reference item or money entry someone else deleted. Settings → Sync
showed the reason but offered no way out.

## Decision

We will:

- **Mark final rejections.** `DomainRejectedException` and `CommandResult`
  gain `Unrecoverable` (default `false`); `CommandResult.Rejected(exception)`
  carries it and `CommandDispatcher` copies it onto `CommandResponse`. It is
  set where the target is gone or is not the caller's and cannot become so
  by retrying: `ListAccess.To` and `TaskList.Require` (list missing, deleted,
  or not owned or joined), `BudgetAccess.To` and `BudgetAccess.Owned`, and a
  missing, deleted or foreign task, reference item or money entry. The
  client already drops an unrecoverable rejection after surfacing it once
  (ADR-0021), so the queue moves on.
- **Keep the rest retryable.** Every other domain rejection keeps
  ADR-0021's behaviour: an archived budget can be restored and a version
  conflict ("Someone else changed this at the same time.") succeeds on retry.
- **Let the user discard a stuck change.** When the head of the outbox was
  rejected on the last sync and is still queued, `ISyncStatus.HasStuckChange`
  is true and Settings → Sync offers "Discard this change". Discarding
  removes that one entry, clears the Rejected belt and messages, resets the
  replica marker to 0 so the next pull restores the server's copy of what the
  change had touched locally, and syncs.

This amends ADR-0021: a domain rejection is no longer always retried.

## Considered alternatives

- **Drop every domain rejection after surfacing it** — a version conflict
  would lose an edit that would have gone through on the next sync.
- **Skip a rejected command and keep pushing the ones behind it** — the
  outbox's order is what keeps a command from overtaking one it depends on;
  a later command could then apply against state the user never saw.
- **Only the Discard button** — every removed member would have to find
  Settings and clear the queue by hand, one stuck change at a time.

## Consequences

A removed member's queued changes for the lost list or budget are surfaced
once and dropped, and the rest of their changes sync. A change dropped as
final is not rolled back locally by itself; for lost access the pull purges
the list or budget anyway, and for a deleted target the pull removes it.
Discard costs one full pull. Neither path can recover the dropped edit: if
the member is invited back, they make the change again. A rejection newly
marked final in some later handler has to be chosen with the same care: a
wrong `Unrecoverable` loses a user's edit silently after one message.
