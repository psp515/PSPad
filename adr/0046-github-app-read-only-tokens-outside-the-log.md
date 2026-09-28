---
title: Connect GitHub through a read-only GitHub App; tokens live outside the event log, encrypted
tags: [integrations, security, identity, persistence]
date: 2026-09-28
status: Proposed
---

# ADR-0046: Connect GitHub through a read-only GitHub App; tokens live outside the event log, encrypted

## Context

Several users per instance each connect their own GitHub account. PSPad only
reads issues and pull requests. The instance is self-hosted, usually not
reachable from the internet. Tokens stored server-side are a liability if
the database leaks.

## Decision

We will use one GitHub App per instance, registered by the self-hoster, with
read-only Issues, Pull requests and Metadata permissions. Users connect with
the app's installation-plus-authorization flow; a one-time `state` bound to
the user identifies the callback. Access (8 h) and refresh (6 months) tokens
are stored AES-GCM encrypted with `GITHUB_TOKEN_ENCRYPTION_KEY`, in
`github_connections` — not an aggregate, no events, never synced to the
client. Sync polls on an interval (`GITHUB_SYNC_INTERVAL_MINUTES`, default
60) with ETags; no webhooks.

## Considered alternatives

- **OAuth App** — one click, non-expiring token, but the `repo` scope grants
  write to every private repository; a leak hands out write access to code.
- **Per-user fine-grained PAT** — read-only and no admin setup, but every
  user must generate and paste tokens by hand; poor for non-technical users.
- **Keycloak identity brokering** — reuses the realm, but the realm is shared
  by other apps (ADR-0026), GitHub would appear as a login option, and the
  scope problem of an OAuth App remains.
- **Webhooks** — need GitHub to reach the instance, which a LAN install
  cannot offer.

## Consequences

A leaked database yields no write access and no plaintext tokens without the
key. Self-hosters register an app and set five variables. The server must
refresh tokens and handle `NeedsReconnect` after six idle months or a
revocation. Changes on GitHub appear up to one interval late unless the user
refreshes manually.
