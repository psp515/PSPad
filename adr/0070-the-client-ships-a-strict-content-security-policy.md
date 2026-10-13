---
title: The client ships a strict content security policy, and the API allows named origins only
tags: [architecture, security, containers, identity]
date: 2026-10-13
status: Active
---

# ADR-0070: The client ships a strict content security policy, and the API allows named origins only

> Status: Active

## Context

Issue #140. The client image's nginx sent only `Cache-Control`. The refresh
token lives in IndexedDB where any script on the origin can read it, markdown
renders through `MarkupString`, and `index.html` pulled Roboto from Google
Fonts. One XSS would take the refresh token; with no `frame-ancestors` the
app, with sign-out and account deletion in Settings, could be framed.

The page has five inline scripts: four static boot scripts (splash teardown,
theme stamp, viewport tracking, service-worker registration — the splash
teardown must stay inline so no module load can fail before it) and the
`<script type="importmap">` that `dotnet publish` fills with fingerprinted
names, so its content changes on every build. The API base address and the
Keycloak realm are runtime settings, substituted at container start.

Sign-in through `AuthenticationService.js` tries `signinSilent` first: a
hidden iframe goes to Keycloak and is redirected back to
`/authentication/login-callback` *inside the iframe*. The service worker
re-issues the page's cross-origin requests (API, Keycloak) from its own
context, under the policy its own script was served with.

The API already had a CORS policy built from `Cors:AllowedOrigins`, read once
in `Program.cs`, so a test could not override it, and with exact origins only.

## Decision

We will serve every client response with a content security policy and
`X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer` and a
`Permissions-Policy` that denies camera, microphone and geolocation. The policy
is `default-src 'self'`, `script-src 'self' 'wasm-unsafe-eval'` plus one
`sha256` hash per inline script, `style-src 'self' 'unsafe-inline'`,
`connect-src 'self'` plus the API and Keycloak origins, `img-src 'self' data:`,
`font-src 'self'`, `object-src 'none'`, `frame-src 'self'` plus Keycloak,
`frame-ancestors 'self'`, `base-uri 'self'`, `form-action 'self'` plus Keycloak.

The image computes it at container start: `19-pspad-csp.envsh`, sourced by the
nginx entrypoint, derives the two origins from `API_BASE_ADDRESS` and
`KEYCLOAK_AUTHORITY` and hashes every inline script of the served
`index.html`; nginx's template step writes it into an include that the server
block and every location with its own `add_header` pull in. Roboto is bundled
under `wwwroot/fonts/roboto` (SIL OFL 1.1).

`frame-ancestors` is `'self'`, not `'none'`: the silent sign-in callback is
the app framed by itself, and `'none'` would stall every sign-in for the
iframe's 10-second timeout before falling back to the redirect.

HSTS stays with whatever terminates TLS; the image serves plain HTTP.

The API's CORS policy reads `Cors:AllowedOrigins` through options, so it is
resolved from final configuration, and accepts wildcard subdomains.
`appsettings.json` allows `https://*.psplace.dev`; `appsettings.Development.json`
(also what hosted tests run under) allows `http://localhost:5001`; compose
replaces the first entry with `APP_BASE_ADDRESS`.

## Considered alternatives

- **Hashes computed at build time** — the importmap is written during
  `dotnet publish`, so the hash would have to come from an extra build step
  that the test suite cannot see; computing at start covers whatever page the
  image actually serves and keeps the origins runtime-configurable.
- **Moving the boot scripts to files** — removes four hashes but not the
  importmap, and the splash teardown is inline on purpose.
- **`'unsafe-inline'` for scripts** — would let an injected script run, which
  is the attack the policy exists to stop.
- **`frame-ancestors 'none'`** — breaks the silent sign-in iframe, see above.
- **`add_header_inherit merge`** — needs nginx 1.29.3+; the include works on
  any nginx the image may be rebuilt on.

## Consequences

- An injected inline script, a foreign script, a fetch to any origin but the
  API and Keycloak, and framing by another site are all refused by the browser.
- A new inline script in `index.html` needs no policy change; a new external
  origin (an image host, a second API) does, in `19-pspad-csp.envsh`.
- `style-src 'unsafe-inline'` stays, because MudBlazor sets inline styles.
- `img-src 'self' data:` is strict on purpose: no image is loaded from another
  origin. The maintainer decided on issue #136 that markdown renders no images
  and makes no HTTP requests, and that branch removes image rendering from
  `MarkdownRenderer`. Until #136 merges, a remote image in markdown is blocked
  by the policy rather than loaded.
- Behind a proxy that adds its own `Content-Security-Policy`, the browser
  enforces both; a proxy that rewrites or strips headers can undo this.
- An origin outside `*.psplace.dev` must be listed in `Cors:AllowedOrigins`
  (compose does it from `APP_BASE_ADDRESS`), or the browser blocks the API.
