---
title: Invites need a link and a code, and expire after 30 minutes
tags: [security, domain, sync]
date: 2026-10-05
status: Active — Amends ADR-0054
---

# ADR-0057: Invites need a link and a code, and expire after 30 minutes

## Context

ADR-0054 gave a shared list a reusable invite link. A leaked or forwarded link
let anyone with an account join forever. The token is 144 bits, so guessing
the link is not the threat; a forwarded link is.

## Decision

Every invite carries the link token and a 6-character code from a 31-symbol
alphabet (no 0, O, 1, I, L), both chosen client-side — the sharing command
stays offline-capable, like the token — and validated in the aggregate. The QR
code embeds both (`#code=` fragment, so the code never reaches a server log);
a copied link does not.

The invite lives 30 minutes from `TaskListShared`. Five wrong codes close it:
`InviteCodeRejected` is committed rather than rejected so the count survives,
and the fifth clears token, code and expiry. The join endpoint answers one
identical 404 for every bad token or code and a 410 only when both match but
time is up; each user gets 10 join attempts per 30 minutes. Token and code are
compared in constant time. Members never receive token, code, expiry or the
failure count through sync. A list shared before this change has no code or
expiry and can never be joined: it answers the uniform 404, not 410.

## Considered alternatives

- **Owner approval of join requests** — more state and a pending-request UI;
  deferred.
- **Link-only with a rate limit** — a forwarded link still works.
- **Longer numeric codes** — worse to read aloud and type.

## Consequences

The owner has to pass the code separately from the link. Anyone holding the
link can burn the invite with five guesses, and the owner makes a new one. A
QR scan that goes through the login redirect may lose the fragment and fall
back to typing the code. Old `TaskListShared` events deserialise with an empty
code and therefore never open.
