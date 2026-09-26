---
title: Offer a new app version with a reload prompt, never activate it unasked
tags: [offline, pwa, ui]
date: 2026-09-26
status: Active
---

# ADR-0040: A new app version is offered with a reload prompt, never activated unasked

## Context

The client is an installed PWA on Blazor's default published service
worker. A deploy is fetched into a new `offline-cache-{version}`, but the new
worker then sits in the browser's waiting state until every tab of the app
closes. Installed PWAs are rarely closed, a reload keeps the old controller,
and nothing told the user a new version existed — so a device could run a
stale build for weeks.

## Decision

We will keep the waiting state and hand the switch to the user. The worker
skips waiting only when a page posts it `{ type: 'SKIP_WAITING' }`. The page
watches its registration (a worker already waiting at start, or a new one
reaching `installed` while a controller exists), checks for updates hourly
and whenever the tab becomes visible, and `AppShell` then shows one
persistent snackbar with a `Reload` action. Reload posts the message, waits
for `controllerchange`, and reloads.

## Considered alternatives

- **`skipWaiting()` + `clients.claim()` on install** — activates a new build
  under a running page mid-session: the old WASM keeps running against a new
  cache, and an edit in progress is lost to a forced reload. Rejected for
  consent and consistency.
- **Status quo (wait for all tabs to close)** — no work, but stale versions
  live indefinitely on an installed PWA with no sign anything is wrong.
- **A blocking dialog** — interrupts capture for something that can wait;
  a snackbar can be dismissed and acted on later.

## Consequences

- Users on the old version see the prompt within an hour, or at the next
  focus of the tab, and reach the new build in one click without closing
  anything.
- Dismissing the prompt keeps the old version until all tabs close — the
  browser default, now chosen rather than silent.
- The reload is safe for offline work: commands are already durable in the
  IndexedDB outbox. Unsaved text in an open field is still lost, which is
  why the reload waits for the click.
- Several open tabs share one waiting worker. Reloading in one activates it
  for all; the others see `controllerchange`, show the same prompt, and
  reload only when their user clicks it.
