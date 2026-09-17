---
title: Purge local data on any signed-in/recorded-owner mismatch, including no recorded owner yet
tags: [offline, sync, identity]
date: 2026-09-17
status: Active
---

# ADR-0025: Purge local data on any signed-in/recorded-owner mismatch, including no recorded owner yet

## Context

ADR-0018 records which user a browser's local replica belongs to and wipes
`documents`, `meta` and `outbox` when the signed-in user differs from that
recorded owner. Its implementation, `ReplicaOwnership.EnsureCurrentUserAsync`,
read that as `owner is not null && owner != userId` — an explicit exemption
for a device whose `meta` store has never recorded an owner at all. The
reasoning at the time (never written down, only implied by the code and its
test `FirstSignInOnADeviceRecordsTheOwnerWithoutClearing`) was that a device
with no recorded owner is a brand-new device with nothing to purge.

That's true only if nothing can reach the outbox before an owner is ever
recorded. ADR-0024 found a path where that assumption breaks: `AppShell`
could mount on the OIDC login-callback route before authentication finished,
render as anonymous, and let a command queue under `Guid.Empty` — all before
`ReplicaOwnership.EnsureCurrentUserAsync` (called only from the authenticated
branch of `AppShell.LoadAccountAsync`) ever ran once. ADR-0024 closes that
specific race, but the exemption itself is the more general hazard: *any*
future path that manages to queue a command before ownership is first
recorded — a bug, a browser extension replaying a request, a future feature
that sends something during onboarding — leaves data on the device that
belongs to no verified owner, and the null-owner exemption would wave it
through into whichever user signs in first, unrejected only in the sense that
the server rejects it anyway ("different user"), forever, exactly as ADR-0018
was written to prevent.

## Decision

`ReplicaOwnership.EnsureCurrentUserAsync` now compares the signed-in user
against the recorded owner directly (`Guid? != Guid`), with no null
exemption: any device whose data isn't provably this user's gets purged
before use, first sign-in included. A genuinely fresh device pays a no-op
(there is nothing to clear); a device carrying data queued before an owner
was ever recorded now loses that data too, per ADR-0018's own logic applied
without a gap.

This refines ADR-0018's implementation to match what its Decision already
said ("when the signed-in user differs from the recorded owner, we wipe") —
it does not change that Decision.

## Considered alternatives

- **Keep the null exemption, close only the specific AppShell race
  (ADR-0024).** Rejected: ADR-0024 is necessary regardless (the race must not
  happen), but relying on it alone to keep the null-owner case safe makes
  every future contributor re-prove that nothing else can queue a command
  before first ownership is recorded — a property that is easy to break by
  accident and expensive to notice when it does (the failure mode is a
  silent, permanent command rejection weeks later, not a build error).
- **Scope the outbox by user id instead of purging.** Rejected for the same
  reason ADR-0018 rejected it: a previous, unattributed session's edits would
  silently vanish rather than being surfaced, which is worse than the clean
  slate a purge gives the next user.

## Consequences

No behavior change for the common case (a device that has always recorded an
owner). The cost, as in ADR-0018, is data loss for whatever unsynced local
state existed under no recorded owner — acceptable because that state was
already unattributable and, per the scenario this closes, already doomed to
permanent server-side rejection. Test fixtures that pre-seed local replica
documents without also recording an owner for that data now have it wiped
out from under them; `AppTestHost.Arrange` was updated to call
`SetOwnerAsync` for its seeded user, since seeded fixture data represents an
existing local replica for that user, not an ownerless device.
