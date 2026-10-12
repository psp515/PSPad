---
title: Two compose stacks joined by a network, each backing itself up nightly, encrypted before it leaves the host
tags: [deployment, persistence, security, identity]
date: 2026-10-12
status: Active (amends [0015](0015-one-self-host-compose-stack.md))
---

# ADR-0061: Two compose stacks joined by a network, each backing itself up nightly

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

We will split the repository's compose into two stacks joined by one
Docker network, `psplace`:

- `docker/compose.yaml`: PSPad (`api`, `app`, `mongo`).
- `docker/keycloak/compose.yaml`: Keycloak and its Postgres. This stack
  creates the network.

Production already runs Keycloak from its own repository. This repository
keeps a copy of that shape so that development and self-hosting match it.

Each stack gets its own backup service behind a `backup` profile:

- `mongo-backup` runs `mongodump --oplog` against PSPad's MongoDB.
- `keycloak-backup` runs `pg_dump` against Keycloak's Postgres.

Both are targets of one `docker/backup/Dockerfile` and share its packaging,
age encryption, rclone upload to an optional S3-compatible bucket and
optional Google Drive, retention, the healthcheck and the ping. Each
service:

- reaches its database by service name inside its own stack, or through
  a URI;
- runs daily at its own time, with Keycloak after Mongo, so a same-night
  pair never has Keycloak older than Mongo;
- always keeps a local plaintext copy, and never uploads plaintext.

Keycloak moves to Postgres, still in `start-dev` (#138 owns `start`).
This amends ADR-0015 twice: it rejected Postgres, and it chose one stack. The Keycloak stack refuses to start against anything but Postgres.

Only the Mongo restore rotates the dataset id (ADR-0062).

## Considered alternatives

- **One stack, with one backup service dumping both stores.** Rejected:
  - It ties the shared realm and its backups to PSPad's lifecycle, which
    ADR-0026 calls the wrong ownership.
  - It doesn't match production, where Keycloak is its own stack.
  - One failing dump would fail the whole run.
- **Standalone backup compose files beside each stack.** Rejected: a third
  and fourth `.env` to keep in sync with the stacks they back up, and an
  external network for each only to reach a database the stack already
  owns.
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
- `docker compose down -v` on PSPad no longer destroys accounts.
- Self-hosters run two `up` commands, Keycloak first. The old H2 volume
  isn't migrated: production's accounts already live in Postgres in their
  own repository, and the repository stack starts fresh from the realm
  file.
- The pair is never one atomic snapshot. Restoring a Keycloak backup older
  than the Mongo one loses accounts created in between, and orphans their
  data. The schedule order and the documented pairing rule are the only
  guard.
- Two `.env` files, and two backup containers to watch. One age key pair
  can serve both.
- The repository compose gains a Postgres container, the cost ADR-0015
  avoided.
- The recovery point is the last nightly run. Up to a day of writes can be
  lost.
- Losing the age private key makes off-host copies unreadable. The local
  copy is still usable.
- Two images and one base to build, pin and keep patched.
