---
title: Drop structurally rejected outbox commands instead of retrying forever
tags: [architecture, offline, sync, contracts]
date: 2026-09-16
status: Active
---

# ADR-0021: Drop structurally rejected outbox commands instead of retrying forever

## Context

The client outbox pushes queued commands to the server on every sync cycle
and only advances past a command once the server accepts it (ADR-0006). This
is correct for a genuine domain rejection — ADR-0005's last-write-wins
territory, where the same command might succeed later against a different
server state, so it stays queued and is retried.

It is wrong for a command the server rejects for reasons that can never
change on retry: an unknown command type (a newer client talking to an older
server, or vice versa after a version skew), a payload that fails to
deserialize, a command carrying a different user's id than the authenticated
caller, or a command type with no registered handler. These are verdicts
about the envelope itself, decided in `CommandDispatcher` before any handler
runs. Resending the identical envelope produces the identical rejection every
time. Under the old behaviour this wedged the outbox: the rejected command
sat at the head of the queue forever, blocking every command queued behind
it, and re-surfacing the same rejection snackbar on every sync cycle. The bug
that exposed this was a stale client sending a command stamped with a
`Guid.Empty` user id (the account had failed to load — see the `AppShell`
account-readiness fix on this branch) — "That command is for a different
user." — but the fix generalizes to all four structural cases, not just that
one.

## Decision

`CommandResponse` gains a fourth field, `Unrecoverable`, defaulting to
`false`. This is backward compatible in both rolling-deploy directions: an
old client deserializing a response from a new server ignores the unknown
JSON property and keeps its old retry-forever behaviour; a new client
deserializing a response from an old server (which never sends the property)
gets the default `false` from `System.Text.Json` and also keeps the old
behaviour. No crash, no migration, either way.

`CommandDispatcher`'s four structural early-return rejections — unknown
command, malformed payload, wrong user, no handler — are marked
`Unrecoverable: true`. Each is a verdict about the envelope's own
well-formedness, made before any handler runs, and cannot succeed on an
identical retry. The final response, produced by the command's own handler
(a genuine domain rejection), is untouched and keeps `Unrecoverable` at its
default `false` — domain rejections keep their exact prior behaviour:
surfaced via the sync snackbar, kept queued, retried on every sync cycle,
per ADR-0005.

`SyncService.PushAsync` drops an unrecoverable rejection's outbox entry
(via `RemoveThroughAsync`) after surfacing it once, instead of leaving it at
the head of the queue.

Two things are worth stating explicitly rather than leaving as implicit
behaviour someone has to rediscover by testing:

- Dropping an unrecoverable command's outbox entry does **not** reconcile
  the local replica, which already applied the command optimistically before
  it was queued. For the wrong-user case this is invisible in practice — the
  malformed local write is stamped with a user id nothing ever queries for
  again. For the version-skew case (a stale cached PWA client emitting a
  command type a newer server no longer recognizes) the local write is
  visible to the user and silently diverges from the server forever once the
  entry is dropped. This is strictly better than the old retry-forever
  behaviour, which at least kept re-surfacing the problem via the snackbar
  every cycle, but it is not a complete fix. Accepted as a known limitation,
  not silently.
- An empty-object payload (`{}`) deserializes successfully into an
  all-default command rather than failing, so the "malformed payload" guard
  only fires on a literal JSON `null` or a structurally incompatible
  payload — not on a same-shape-but-all-defaults object. A field-less
  payload instead falls through to the "wrong user" check, since an
  all-default `Guid` never matches a real authenticated user id, and is
  still correctly rejected as `Unrecoverable: true` — just via a different
  branch than "malformed payload" might suggest to someone reading the
  rejection reason.

This refines a corner of ADR-0005 that decision left unspecified (it commits
to rejections always being surfaced, but says nothing about retry versus
drop); it does not supersede it, and ADR-0005 keeps its `Active` status
unchanged.

## Considered alternatives

- **Bounded retry count instead of immediate drop** — rejected: a structural
  rejection is deterministic given the same envelope. A bounded retry only
  delays the identical outcome by a fixed number of cycles while still
  blocking the queue behind it for that long; it buys nothing over dropping
  immediately once the response is known to be unrecoverable.
- **A separate dead-letter queue for dropped commands, reviewable/retriable
  by the user** — rejected: more machinery than this app's single-user-scale
  problem warrants. The one-time snackbar surfacing already satisfies
  ADR-0005's "rejections are always surfaced, never dropped silent"
  requirement; a persisted review queue is a feature nobody has asked for
  yet, for a case (version skew, id corruption) that is rare in practice.

## Consequences

The outbox no longer wedges forever on one bad command. This is a general
fix, not one specific to the wrong-user case that motivated it — it equally
covers a stale client's unknown-command and malformed-payload cases after a
server upgrade drops or renames a command type.

The cost is the unreconciled-replica-write limitation above: a dropped
command's optimistic local write is not undone, so a version-skew client can
end up with a local replica that silently and permanently diverges from the
server after the one-time snackbar is dismissed. There is also currently no
record of what got dropped beyond that single snackbar — no history entry,
no log — so a dropped command is not diagnosable after the fact. If this
turns out to matter in practice, the dead-letter alternative above becomes
worth revisiting.
