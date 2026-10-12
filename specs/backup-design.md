# Daily backups — design

Status: Proposed. Issue #120. Decisions of record:
[ADR-0061](../adr/0061-nightly-backups-from-a-compose-sidecar.md),
[ADR-0062](../adr/0062-a-restore-changes-the-dataset-id.md).
Builds on #138 (Keycloak on Postgres) and #143 (dedicated Mongo users).

Once a day, PSPad writes a consistent copy of everything it needs to come
back after a lost disk, a bad upgrade or a `down -v`. It keeps that copy on
the host and, optionally, off it. A restore brings back both the data and
the accounts that own it, and tells every open client to drop its replica.

## 1. Scope

In:

- A `backup` service in `docker/compose.yaml`, behind the `backup` compose
  profile, built from `docker/backup/`.
- One run per day at `BACKUP_AT` (in `BACKUP_TZ`), plus `backup now` on
  demand.
- Contents: a MongoDB logical dump of the whole instance and a `pg_dump`
  of Keycloak's Postgres database.
- Destinations: always a local directory; optionally an S3-compatible
  bucket; optionally Google Drive. Off-host copies are always encrypted.
- Retention: keep the last `BACKUP_KEEP_DAYS` days (14 by default) at every
  destination.
- `docker/backup/restore.sh`: restores both stores and rotates the dataset
  id (§6).
- Health: the container turns unhealthy when the last successful run is
  more than 26 h old. An optional ping URL is called on success and failure.
- Docs: `install.astro` Backups section, `.env.example`.

Out:

- Point-in-time recovery or oplog tailing. The recovery point is the last
  nightly run.
- Backups started from the app UI, and per-user export or import. A
  per-user JSON export is a separate feature.
- Any rclone backend other than S3 and Google Drive.
- Backing up `mongo-keyfile`. A logical dump restores into any instance,
  and a fresh install generates its own key.
- Backing up Keycloak while it runs on H2 (`start-dev`). See §4.

## 2. Shape

```
docker/backup/
  Dockerfile        FROM mongo:8 (mongodump/mongorestore) + postgresql-client,
                    rclone, age, supercronic — all pinned
  backup.sh         one run: dump → package → encrypt → upload → prune → ping
  restore.sh        fetch → decrypt → restore → rotate dataset id
  entrypoint.sh     validates env, renders the crontab, execs supercronic
```

There's no .NET code and no module. The API never knows backups exist,
except for the dataset id (§6), which is a sync concern.

Every licence is GPL-3-compatible: rclone (MIT), age (BSD-3), supercronic
(MIT), and the MongoDB Database Tools (Apache-2.0). CI builds and publishes
the image next to `pspad-api` and `pspad-app`.

## 3. One run

1. `mongodump --uri "$BACKUP_MONGO_URI" --oplog --archive --gzip` produces
   `mongo.archive.gz`. `--oplog` makes the dump a single point in time
   across collections. That matters because documents, `events`,
   `counters`, Statistics records and the dispatch marker (ADR-0036,
   ADR-0037) must agree. It needs a whole-instance dump of a replica set,
   which `rs0` already is. The connecting user has the built-in `backup`
   role (#143 keeps root out of the API; backups get their own user, not
   root either).
2. `pg_dump --format=custom` on `$BACKUP_KEYCLOAK_PG_URI` produces
   `keycloak.dump`.
3. Both files and a `manifest.json` (PSPad version, run time in UTC,
   sha256 of each file, sizes) go into
   `pspad-<yyyy-MM-ddTHHmmZ>.tar`.
4. **Local:** the tarball is written to `/backups` (the `backup-data`
   volume, or a bind mount set with `BACKUP_LOCAL_DIR`) unencrypted, so a
   restore on the same host needs no key. The directory is `0700` and owned
   by the container user.
5. **Off-host:** when either remote is configured, the tarball is encrypted
   once with `age -r "$BACKUP_AGE_RECIPIENT"` to `…tar.age` and that file
   is uploaded with `rclone copyto`. The plaintext is never uploaded.
6. **Prune:** every destination deletes `pspad-*.tar[.age]` older than
   `BACKUP_KEEP_DAYS` days. It never prunes when that day's run failed, so
   a broken run can't eat the last good copies.
7. **Report:** each step logs one line. On success it writes
   `/backups/.last-success`. It then calls `BACKUP_PING_URL` (`/fail` is
   appended on failure, in the healthchecks.io style), if set.

A failure in any step stops the run and exits non-zero. A failed upload to
one remote doesn't skip the other, but the run still fails.

## 4. Keycloak

PSPad keys users by their Keycloak UUID (`install.astro`, "Never pass -v").
A Mongo restore without the matching accounts orphans every user's data, so
both stores are always backed up together.

`pg_dump` is the one supported path. It's consistent while Keycloak runs
and matches production. This depends on #138 moving the repo compose from
`start-dev`/H2 to `start` on Postgres. The `keycloak-db` Postgres service it
adds is what `BACKUP_KEYCLOAK_PG_URI` points at.

If `KC_DB` isn't Postgres, the backup container refuses to start and names
#138. A run that silently skips the accounts is worse than no run.

**Migration for existing H2 installs** is a one-off and is documented in
`install.astro`:

1. Stop Keycloak.
2. Run `kc.sh export --dir /export --users realm_file` in a one-off
   container on `keycloak-data`.
3. Start the Postgres-backed Keycloak with `--import-realm` from that
   directory.

User UUIDs survive an export and import.

## 5. Configuration

New `docker/.env.example` entries, each with a comment in the file's
existing style:

| Variable | Default | Meaning |
|----------|---------|---------|
| `BACKUP_AT` | `03:17` | Daily run time (`HH:mm`) |
| `BACKUP_TZ` | `UTC` | Time zone for `BACKUP_AT` |
| `BACKUP_KEEP_DAYS` | `14` | Retention at every destination |
| `BACKUP_LOCAL_DIR` | — | Host path to bind instead of the `backup-data` volume |
| `BACKUP_MONGO_PASSWORD` | — (required) | Password of the `pspad-backup` Mongo user (`backup` + `restore` roles) |
| `BACKUP_KEYCLOAK_PG_PASSWORD` | — (required) | Keycloak Postgres password |
| `BACKUP_AGE_RECIPIENT` | — | age public key (`age1…`); required once any remote is set |
| `BACKUP_S3_ENDPOINT`, `_REGION`, `_BUCKET`, `_PREFIX`, `_ACCESS_KEY_ID`, `_SECRET_ACCESS_KEY` | — | S3-compatible remote (AWS, Backblaze B2, Cloudflare R2, MinIO). It's enabled when `_BUCKET` is set |
| `BACKUP_GDRIVE_TOKEN`, `_FOLDER_ID`, `_CLIENT_ID`, `_CLIENT_SECRET` | — | Google Drive remote. It's enabled when `_TOKEN` is set |
| `BACKUP_PING_URL` | — | Called on success, with `/fail` appended on failure |

`entrypoint.sh` maps these onto rclone's `RCLONE_CONFIG_<REMOTE>_*`
environment variables, so there's no rclone config file to mount.

**Fail fast at start**, with one decisive line each:

- a remote is configured but `BACKUP_AGE_RECIPIENT` is empty;
- the recipient isn't a valid age key;
- Keycloak isn't on Postgres;
- a required password is empty;
- `BACKUP_AT` doesn't parse.

**Google Drive.** It uses the `drive.file` scope, so rclone only sees
files it created. A self-hoster gets the token by running
`rclone authorize "drive"` on any machine with a browser. Two pitfalls are
documented:

- A service account has no storage quota in a personal My Drive. That path
  only works with a Workspace shared drive.
- An own OAuth client left in the *Testing* publishing state issues
  refresh tokens that expire after 7 days. Publish it, or use rclone's
  built-in client.

**S3 keys.** The docs recommend a bucket with versioning or object lock.
Remote pruning needs delete permission. A self-hoster who prefers
write-only keys sets `BACKUP_REMOTE_PRUNE=false` and leaves retention to a
bucket lifecycle rule.

## 6. Restore and the dataset id

A restore rolls `counters.events` back. Every open client holds a marker
above it, so it would pull nothing until the server overtakes that marker.
It would also keep documents that the restored server no longer has. AD-6
(the server is truth, the replica is disposable) only holds if the client
learns that the server's dataset changed.

**Server.** A `meta` collection holds one document,
`{ _id: "dataset", value: <Guid> }`. `SyncReader` reads it, creating it on
first use, and returns it as a new `SyncResponse.DatasetId`. Nothing else
writes it. `restore.sh` deletes it after `mongorestore`, so the next sync
mints a new id.

**Client.** `SyncService` stores the dataset id in replica meta next to the
marker and the collections fingerprint (ADR-0049):

- If the stored id is missing, it records the id it receives. This is
  today's install upgrading, and it causes no purge.
- If the stored id differs from the one received, the sync **discards
  this response**. It clears every replica collection and the marker, keeps
  the recorded owner and the **outbox**, records the new id, and pulls
  again with `since = 0`.
- The outbox then replays under last-write-wins (AD-5). Commands that refer
  to aggregates the restore lost come back rejected and are surfaced the
  usual way (Rejected belt, Settings → Sync). They're never dropped
  silently.

The push of the outbox happens before the pull in today's `SyncAsync`. On
a dataset change those commands land on the restored server first, which
is the intended LWW order.

**`restore.sh <file|latest> [--from local|s3|gdrive]`:**

1. Fetch the backup, decrypt it with `BACKUP_AGE_IDENTITY_FILE` (a mounted
   private key, only needed for restore), and verify each sha256 in the
   manifest.
2. Ask for confirmation by typing the instance's `MONGO_DATABASE`. Skip
   the prompt with `--yes`.
3. Stop `api` and `keycloak` (`docker compose stop`), so the restore isn't
   racing writes.
4. Run `mongorestore --oplogReplay --drop --archive --gzip` and
   `pg_restore --clean --if-exists`.
5. Delete `meta.dataset`.
6. Start `api` and `keycloak`.
7. Print the backup's timestamp so the operator knows what was lost.

## 7. Testing

- **Unit** (`[UnitTest]`, `test/PSPad.App.Tests`):
  - `SyncService` records the first dataset id without purging.
  - A changed id clears the replica and marker, keeps the outbox and owner,
    and re-pulls from 0.
  - An equal id changes nothing.
- **Integration** (`[IntegrationTest]`, `test/PSPad.Api.Tests`):
  - `GET /api/sync` returns a stable `datasetId`.
  - Deleting `meta.dataset` yields a new one on the next call.
- **Round trip** (new CI job, `backup-roundtrip`):
  1. Bring up the compose stack with the `backup` profile, a MinIO
     container as the S3 remote, and a throwaway age key.
  2. Seed data through `/api/commands` and run `backup now`.
  3. Assert that the local tarball and the remote `.age` object exist, and
     that the remote object isn't a readable tar.
  4. `down -v` and bring the stack up again.
  5. `restore.sh latest --from s3 --yes`.
  6. Assert that the seeded user signs in with the same UUID, their
     documents sync back, and `datasetId` changed.

  The job also checks that the entrypoint rejects a remote without a
  recipient.

Google Drive isn't exercised in CI because there's no hermetic fake. Its
rclone wiring is covered by the entrypoint's config rendering, checked with
`rclone config show` in the round trip.

## 8. Documentation

- `install.astro` → **Backups** is rewritten:
  - enable the profile;
  - generate an age key pair (keep the private key off the server);
  - configure S3 or Google Drive;
  - check health;
  - restore;
  - the H2 → Postgres migration.

  The manual `mongodump` one-liner stays as the no-profile fallback.
- `docker/.env.example`: every variable in §5.
- `features.astro`: one line, "Nightly encrypted backups, on the host and
  off it".
- `specs/backend-spec.md` §5 Sync: `datasetId` and the purge rule.

## 9. Open questions

- Should the local copy be optionally encrypted too, for hosts whose disk
  is itself backed up elsewhere? (`BACKUP_LOCAL_ENCRYPT=true`)
- Should a per-run size anomaly (for example the dump shrinking by more
  than 50 % against the previous run) fail the ping, to catch a silently
  emptied database before retention rotates the good copies out?
