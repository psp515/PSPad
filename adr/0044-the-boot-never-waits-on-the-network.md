---
title: The boot never waits on the network, and the splash stays up until the first screen is ready
tags: [offline, pwa, ui, identity]
date: 2026-09-28
status: Active (amends ADR-0027 and ADR-0030)
---

# ADR-0044: The boot never waits on the network, and the splash stays up until the first screen is ready

## Context

The app would not open in a shop. The phone had a network that accepted
requests and never answered — a captive portal, one bar of signal — rather
than no network at all. Real airplane mode worked, because a fetch with no
route fails at once and the service worker falls back to its cache.

Reproduced against a release build in headless Chromium, the boot held on the
splash indefinitely. The cause was the service worker's rule for
`appsettings.json`: network-first with no timeout, so that a value the
container's entrypoint rewrote at start reaches clients without a new build.
Blazor cannot start without its configuration, so a request that never
answers is a boot that never finishes. A second hang sat behind it:
`AppShell` set itself ready and then awaited `/api/me` before its next render.
Only the first incomplete await of `OnInitializedAsync` renders, so a
non-answering server held the loader up for HttpClient's 100-second timeout.

The same trace, on a CPU slowed 4× to act like a phone, explained the screens
that flashed at every launch. `Program.cs` tore the splash down before the
first render. What followed was a blank page, then `AuthorizeRouteView`'s
authorizing pass drawing `AppShell` chrome — the open desktop drawer or the
mobile app bar — around a centered loader, then the app. The session was read
from IndexedDB three times (bootstrapper, authentication state, shell), and
`replica.js` opened a new IndexedDB connection for every call, 22 per launch.

## Decision

**The service worker answers `appsettings.json` from its cache and refreshes
it in the background** (stale-while-revalidate). It fetches over the network
only when nothing is cached. It still never pins the file's build-time hash.

**The boot splash stays up until a screen is ready, and that screen tears it
down.** `Components/BootSplashRelease` calls `pspadBoot.done` after its first
render. `AppShell` renders it once ready with a session; `PublicLayout` and
`AuthenticationLayout` render it always. A signed-out `AppShell` does not,
since it only hosts the redirect to `/welcome`. `index.html` releases the
splash on its own after 20 seconds, so a boot that never renders a screen
cannot hide the error banner forever.

**`AppShell` renders as soon as it is ready.** The account refresh runs
behind that render, never ahead of it.

**The bootstrapper hands the session it read to
`LocalAuthenticationStateProvider`** (`Adopt`), and `AppShell` reads it from
there (`SessionAsync`). Authentication state is known before the first
render, so there is no authorizing pass. Code that needs the latest stored
token — the account refresh, the authorization handler — still reads the
store.

**`replica.js` shares one IndexedDB connection** and drops it on
`versionchange` or `close`.

## Considered alternatives

- **Network-first with a short timeout** — every online launch still pays a
  round trip before Blazor starts, and a flaky network still pays the full
  timeout on every launch. Rejected for a value that changes only when
  someone redeploys.
- **Keep tearing the splash down in `Program.cs`, but render a skeleton
  instead of the shell** — still a second visual state between splash and
  app. The splash is already the loading screen.
- **Cache the session inside `LocalSessionStore`** — every reader would then
  see a refresh token that another tab may already have rotated, and Keycloak
  rejects a reused one as `invalid_grant`. Keeping the cache in the
  authentication provider limits it to identity, which only a sign-in or
  sign-out changes, and both already go through the provider.

## Consequences

A launch no longer touches the network before it draws, whether the network
is up, down or hanging. A synced device in full lie-fi reaches its content in
about 1.5 s. Each launch shows one transition, splash → app.

A changed environment value (API base address, Keycloak authority) now takes
effect from the **second** launch after a redeploy, not the first. That
launch talks to the old address. A deployment that moves its API or realm
should expect one launch of failed requests, which the app already treats as
offline. A new client build is unaffected, because the worker installs its
settings fresh.

The raw boot time on a slow phone is roughly unchanged, at about 5.5 s on a
4×-throttled CPU with everything cached. What remains is CPU spent starting
the runtime and rendering MudBlazor on the interpreter, not waiting. Cutting
it is a separate trade-off: ahead-of-time compilation, with a larger download
and slower builds.

If a screen is added under a layout that does not render `BootSplashRelease`,
the splash covers it for 20 seconds. New layouts must render it.
