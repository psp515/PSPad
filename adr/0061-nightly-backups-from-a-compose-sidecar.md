---
title: Nightly backups run in a compose sidecar, encrypted before they leave the host
tags: [deployment, persistence, security, identity]
date: 2026-10-12
status: Proposed (amends [0015](0015-one-self-host-compose-stack.md))
---

# ADR-0061: Nightly backups run in a compose sidecar, encrypted before they leave the host

## Context

Losing data in PSPad today is easy: a lost disk, a bad upgrade, or a
`down -v` that wipes `keycloak-data` (#120). `install.astro` documents a
manual `mongodump`, but nothing runs it, nothing copies its output off the
host, and nobody has ever tested a restore.

PSPad keys users by their Keycloak UUID. A Mongo dump restored without the
accounts it belongs to orphans every user's data, so a backup has to cover
both stores.

ADR-0015 kept Keycloak on `start-dev`/H2 to avoid a second database
engine. H2 can't give a consistent copy while Keycloak is running.
Production already runs Keycloak on Postgres, and #138 proposes the same
for the repository compose.

## Decision

We will ship a `backup` service in `docker/compose.yaml`, behind a `backup`
profile, built from `docker/backup/`.

- Once a day it takes a `mongodump --oplog` of the whole instance and a
  `pg_dump` of Keycloak's Postgres database.
- It keeps the result in a local directory.
- When an S3-compatible bucket or Google Drive is configured, it also
  uploads the result there through rclone, always encrypted first with an
  age public key the operator provides.
- It keeps `BACKUP_KEEP_DAYS` days at each destination and reports health
  through its container healthcheck and an optional ping URL.

Keycloak in the repository compose moves to Postgres (#138), amending
ADR-0015's rejection of that option. The backup service refuses to start
against an H2 Keycloak.

There's no .NET code for backups, and no module.

## Considered alternatives

- **A hosted service in `PSPad.Api` exporting collections through the
  driver.** Rejected:
  - It reimplements `mongodump`, without a point-in-time view across
    collections.
  - It puts dump load and remote credentials inside the API process.
  - It still can't reach Keycloak's data.
- **A host crontab and a documented script.** Rejected: nothing in the
  stack would run it, report on it or test it. The manual one-liner stays
  in the docs as the fallback.
- **Plaintext uploads with bucket-side encryption and lifecycle rules.**
  Rejected: the bucket's operator, or anyone who leaks its keys, reads
  every task. Google Drive has no equivalent.
- **Tarring the `keycloak-data` volume.** Rejected: copying a live H2 file
  isn't consistent, and stopping Keycloak nightly takes sign-in down.
- **Supporting both H2 export and `pg_dump`.** Rejected: there's no safe
  export from a running H2 instance, and two paths would double the
  restore matrix for an engine production doesn't use.

## Consequences

- A self-hoster gets nightly, off-host, encrypted copies by setting a few
  `.env` variables. The private key never needs to be on the server.
- The repository compose gains a Postgres container. That's the cost
  ADR-0015 avoided, now paid because accounts must be restorable.
  Existing H2 installs need a one-off export and import, which is
  documented in `install.astro`.
- The recovery point is the last nightly run. Up to a day of writes can
  be lost.
- Losing the age private key makes every off-host copy unreadable. The
  local copy stays plaintext, so a same-host restore still works.
- Google Drive tokens can expire, for example from an unpublished OAuth
  client. The healthcheck and ping exist so that failure is noticed.
- A new image to build, pin and keep patched: mongo tools, postgres
  client, rclone, age, supercronic.
