---
title: Offline is confirmed by the server, not by the browser's flag alone
tags: [sync, ui]
date: 2026-10-08
status: Active (amends [0044](0044-the-boot-never-waits-on-the-network.md))
---

# ADR-0058: Offline is confirmed by the server, not by the browser's flag alone

## Context

`BrowserConnectivity` trusted `navigator.onLine` and the `online`/`offline`
events. That flag reports "offline" for setups where the API is reachable —
a LAN-only self-host, a VPN or adapter switch, a sleeping interface — and the
app then stayed offline until a later `online` event that might never come:
the poll skipped syncing, the sync buttons stayed disabled and the Offline
indicator never cleared, with the API up.

## Decision

We will treat the browser's offline signal as a prompt to check, not as a
fact. On an `offline` event, or at start when `navigator.onLine` is false, the
app probes `{Api:BaseAddress}/health` (`IServerProbe`); only a failed probe
sets `IsOnline` to false. While offline it probes again every 10 seconds and
raises `CameOnline` the moment the server answers. The `online` event still
sets online at once. The indicator is an icon with a tooltip, not a chip
with text.

## Considered alternatives

- **Trust the browser flag (status quo)** — wrong in exactly the setups a
  self-hosted app targets.
- **Probe on every poll while online** — an extra request a minute for a state
  the sync request already reveals; failures surface as `LastSyncFailed`.
- **Always attempt sync regardless of the flag** — contradicts the established
  rule that offline skips sync and makes every offline poll a failing request.

## Consequences

An offline device now makes one cheap failing request every 10 seconds until it
recovers, and `/health` must stay anonymous and CORS-agnostic (the probe is a
`no-cors` fetch, so any answer counts). A false "Offline" corrects itself within
10 seconds, with no reload. A truly offline device behaves as before.
