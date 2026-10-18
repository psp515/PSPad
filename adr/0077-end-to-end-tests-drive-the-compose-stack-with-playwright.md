---
title: End-to-end tests drive the compose stack with Playwright
tags: [testing, architecture, ci]
date: 2026-10-18
status: Active
---

# ADR-0077: End-to-end tests drive the compose stack with Playwright

> Status: Active

## Context

Unit tests cover the domain and bUnit components; integration tests
(ADR-0009) cover handlers and HTTP against a Testcontainers MongoDB. Nothing
exercises the application as a user meets it: the WebAssembly client in a
browser, signed in through Keycloak, calling the API across origins under
the client's Content-Security-Policy, syncing an IndexedDB replica, going
offline. Issue #107 asked for end-to-end coverage of whole scenarios, kept
to two or three minutes so it runs on every pull request.

Two questions needed settling: which runner, and what the tests run against.

## Decision

We will keep a Playwright Test suite in TypeScript under `test/e2e/`, outside
the .NET solution, that runs whole-scenario journeys in Chromium against the
Docker Compose stack in `docker/compose.yaml`, built from the working tree
with `docker/.env.example`. Each test creates its own Keycloak user through
the admin API and signs in through the real login form. Server-side checks
that a screen cannot show are made from the test with the client's own bearer
token against the API. The suite is a CI job that gates deploy. Design:
`specs/e2e-testing-design.md`.

## Considered alternatives

- **Playwright for .NET inside the solution** (`[EndToEndTest]` category) —
  rejected. `dotnet test` would then need the compose stack or a filter on
  every run, breaking the "unit suite needs nothing" rule; and the .NET
  binding trails the Node runner in trace viewer, UI mode and codegen, which
  are the tools that make browser tests maintainable.
- **A separate API-only end-to-end suite with Testcontainers** — rejected as
  a tier. The integration suite already boots the API host against a real
  MongoDB; another layer would re-test the same handlers over sockets. The
  one thing it would add, the behaviour of the deployed container with
  Keycloak, the browser suite covers by running against the real stack, and
  calls the API directly where a check is about the server.
- **Reusing Playwright storage state to sign in once** — rejected. The
  client keeps its session in IndexedDB and OIDC state in `sessionStorage`;
  restoring it is possible but brittle, and a shared account would forbid
  exact assertions. One user per test costs about three seconds and buys
  isolation.
- **Running against `dotnet run` hosts instead of containers** — rejected.
  It would skip the nginx CSP, the appsettings substitution at container
  start and the image Dockerfiles, which are exactly the seams the suite is
  for.

## Consequences

- Every pull request builds both images and runs the stack in CI, adding
  several minutes to the pipeline before the suite's own two to three.
- A third test category exists with its own toolchain (Node, npm, Chromium)
  and its own Dependabot entry; `specs/testing-spec.md` names it.
- The suite depends on the application staying drivable through roles,
  labels, text and the `pspad-*` classes; a UI change that breaks a selector
  is a failing scenario to fix alongside the change, never a hidden skip.
- Scenarios share one clock rule: "today" is the UTC date, because a fresh
  Keycloak user is provisioned in `Etc/UTC`.
- If the stack's ports or realm change, the suite's configuration changes
  with them; the suite reads them from the same `.env.example`.
