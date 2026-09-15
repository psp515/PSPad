---
title: A documentation site in Astro, published to GitHub Pages
tags: [documentation, tooling, deployment]
date: 2026-09-14
status: Active
---

# ADR-0016: A documentation site in Astro, published to GitHub Pages

## Context

PSPad is a self-hosted application with no public explanation of how to host
it, and no product surface of its own to point a stranger at. `README.md`
addresses a contributor — it assumes a clone, a `.NET` SDK, and an interest
in the domain model — not someone deciding whether to run this on their own
hardware. There was nowhere to send that second reader, and nowhere to show
the thing before asking anyone to install it.

## Decision

We will build `docs/` as a plain Astro project with three hand-built pages —
landing, features, install — published to GitHub Pages by its own GitHub
Actions workflow. The install page reads `docker/compose.yaml` and
`docker/.env.example` at build time rather than restating their contents in
prose. AGENTS.md §11 carries a rule that documentation ships with the change
that makes it wrong, so the install page and the compose stack it describes
cannot drift apart the way `README.md` and the old two-compose-file setup
already had.

## Considered alternatives

- **Starlight.** Rejected: it buys a sidebar, search and versioning that
  three pages do not need, and its splash template fights the one page that
  has to persuade rather than reference — the landing page is not
  documentation shaped.
- **A `docs` service added to the compose stack.** Rejected: the install page
  is the thing a reader consults *before* any stack exists on their machine.
  Serving it from a container that page is meant to help them bring up is
  backwards.
- **Hand-written install content.** Rejected: prose describing
  `docker/compose.yaml` drifts the day a variable is renamed there — exactly
  the failure this whole page exists to prevent, and the one ADR-0015 already
  paid for once with the abandoned production compose file.
- **A CI job that fails when `docker/**` changes without a matching
  `docs/**` change.** Rejected: noisy, and it catches the fact that a file
  changed rather than the fact that a sentence went stale. Reading the file
  at build time removes the category of drift instead of policing it.

## Consequences

A Node toolchain enters a `.NET` repository, with its own workflow and its
own lockfile; `ci.yml` is untouched and has no reason to run `npm`. The docs
build now fails if `docker/compose.yaml` or `docker/.env.example` moves or is
renamed, which is a feature — the install page cannot silently go stale on
the one section that matters most for a self-hoster's first attempt. Prose
elsewhere on the site still drifts on its own; only the generated blocks
cannot. Icons are committed rather than generated in CI, so refreshing them
is a manual step outside this workflow.
