---
title: Nightly backups run as two compose stacks, one for MongoDB and one for Keycloak, encrypted before they leave the host
tags: [deployment, persistence, security, identity]
date: 2026-10-12
status: Proposed (amends [0015](0015-one-self-host-compose-stack.md))
---

# ADR-0061: Nightly backups run as two compose stacks, one for MongoDB and one for Keycloak

## Context

Losing data in PSPad today is easy: a lost disk, a bad upgrade, or a
`down -v` that wipes `keycloak-data` (#120). `install.astro` documents a
manual `mongodump`, but nothing runs it, nothing copies its output off the
host, and nobody has ever tested a restore.

PSPad keys users by their Keycloak UUID (ADR-0026). A Mongo dump restored
without the accounts it belongs to orphans every user's data, so both
stores need backing up.

The two stores don't have the same owner:

- MongoDB is PSPad's.
- Keycloak holds the shared `psplace` realm for every self-hosted
  application. ADR-0026 already names its place inside PSPad's compose
  stack as the wrong ownership, to be split out once a second application
  is real.

ADR-0015 kept Keycloak on `start-dev`/H2, and H2 can't give a consistent
copy while Keycloak runs. Production already runs Keycloak on Postgres, and
#138 proposes the same for the repository.

## Decision

We will ship two independent backup stacks, each with its own compose file
and `.env`:

- `docker/backup/mongo/` runs `mongodump --oplog` against PSPad's MongoDB.
- `docker/backup/keycloak/` runs `pg_dump` against Keycloak's Postgres.

Both images build on one base in `docker/backup/base/`, which owns
packaging, age encryption, rclone upload to an optional S3-compatible
bucket and optional Google Drive, retention, the healthcheck and the ping.
Each stack:

- reaches its database over an external Docker network, or through a URI;
- runs daily at its own time, with Keycloak after Mongo, so a same-night
  pair never has Keycloak older than Mongo;
- always keeps a local plaintext copy, and never uploads plaintext.

Keycloak moves to Postgres (#138), amending ADR-0015's rejection of that
option. The Keycloak stack refuses to start against anything but Postgres.

Only the Mongo restore rotates the dataset id (ADR-0062).

## Considered alternatives

- **One backup service in `docker/compose.yaml` dumping both stores.**
  Rejected:
  - It ties the shared realm's backups to PSPad's lifecycle, which
    ADR-0026 calls the wrong ownership.
  - A Keycloak that moves to its own stack, or a production Keycloak on a
    different host, would need the PSPad stack to reach into it.
  - One failing dump would fail the whole run.
- **A hosted service in `PSPad.Api` exporting collections through the
  driver.** Rejected:
  - It reimplements `mongodump` without a point-in-time view.
  - It puts remote credentials inside the API.
  - It can't reach Keycloak at all.
- **A host crontab and a documented script.** Rejected: nothing would run,
  report on or test it. The manual one-liner stays as the fallback.
- **Plaintext uploads with bucket-side encryption.** Rejected: the bucket's
  operator, or anyone holding leaked keys, reads every task. Drive has no
  equivalent.
- **Tarring the `keycloak-data` volume, or exporting from H2.** Rejected:
  neither is consistent while Keycloak runs, and stopping it nightly takes
  sign-in down for every application.

## Consequences

- Each store is backed up, monitored and restored on its own. A lost realm
  doesn't force a Mongo restore, and a bad PSPad upgrade doesn't touch
  accounts.
- The keycloak stack moves with Keycloak, unchanged, when Keycloak leaves
  PSPad's compose stack.
- The pair is never one atomic snapshot. Restoring a Keycloak backup older
  than the Mongo one loses accounts created in between, and orphans their
  data. The schedule order and the documented pairing rule are the only
  guard.
- Two `.env` files, and two containers to watch. One age key pair can serve
  both.
- The repository compose gains a Postgres container, the cost ADR-0015
  avoided. Existing H2 installs need a one-off export and import.
- The recovery point is the last nightly run. Up to a day of writes can be
  lost.
- Losing the age private key makes off-host copies unreadable. The local
  copy is still usable.
- Two images and one base to build, pin and keep patched.
