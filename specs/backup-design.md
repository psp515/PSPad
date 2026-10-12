# Daily backups — design

Status: Built. Issue #120. Decisions of record:
[ADR-0061](../adr/0061-nightly-backups-from-two-compose-stacks.md),
[ADR-0062](../adr/0062-a-restore-changes-the-dataset-id.md).
Builds on #138 (Keycloak on Postgres) and #143 (dedicated Mongo users).

Once a day, PSPad's data and the accounts that own it are copied somewhere
that survives a lost disk, a bad upgrade or a `down -v`.

The repository runs two compose stacks, joined by one Docker network,
`psplace`:

- **PSPad** (`docker/compose.yaml`): `api`, `app`, `mongo` and
  **`mongo-backup`**.
- **Keycloak** (`docker/keycloak/compose.yaml`): `keycloak`, `keycloak-db`
  (Postgres) and **`keycloak-backup`**.

Keycloak holds the shared `psplace` realm for every self-hosted application
(ADR-0026). Production already runs it from its own repository. Here it
stays in this repository, as a separate stack.

Each backup service lives in its store's stack, behind the `backup`
profile, with that stack's `.env`. The two run, fail and restore
independently.

## 1. Scope

In:

- Splitting Keycloak out of `docker/compose.yaml` into
  `docker/keycloak/compose.yaml` on Postgres, with the shared external
  network `psplace`.
- `mongo-backup` in the PSPad stack and `keycloak-backup` in the Keycloak
  stack, both behind the `backup` profile.
- A .NET worker, `src/PSPad.Backup`, built by one multi-stage
  `src/PSPad.Backup/Dockerfile` into two images: target `mongo` and target
  `keycloak`.
- One run per day at `BACKUP_AT` (in `BACKUP_TZ`) in each stack, plus
  `backup now` on demand.
- Destinations, per stack: always a local directory; optionally an
  S3-compatible bucket; optionally Google Drive. Off-host copies are always
  age-encrypted.
- Retention: `BACKUP_KEEP_DAYS` days (14 by default) at every destination.
- Restore scripts: one per stack. Only the Mongo one rotates the dataset id
  (§7).
- Health: each container turns unhealthy when its last successful run is
  more than 26 h old. An optional ping URL is set per stack.
- Docs: `install.astro` (two stacks, Backups), `docker/.env.example`,
  `docker/keycloak/.env.example`.

Out:

- Point-in-time recovery or oplog tailing. The recovery point is the last
  nightly run of each stack.
- Backups started from the app UI, and per-user export or import.
- Any destination other than S3-compatible storage and Google Drive.
- Backing up `mongo-keyfile`. A logical dump restores into any instance.
- Backing up Keycloak while it runs on H2. See §5.
- Keycloak `start` mode, hostname and port hardening. That stays #138's
  work; this change only moves Keycloak's database.
- One combined, atomic snapshot of both stores. See §6.

## 2. Shape

```
docker/compose.yaml            api, app, mongo, mongo-backup[backup]   network psplace (external)
docker/keycloak/compose.yaml   keycloak, keycloak-db, keycloak-backup[backup]   network psplace (owner)
src/PSPad.Backup/              the worker; assembly `backup`, references PSPad.Infrastructure only
  BackupCli                    run | check | now | list | restore | healthcheck | gdrive-login
  Configuration/               BackupSettings.Read(env): every BACKUP_* variable, fail-fast rules
  Scheduling/DailySchedule     next run in BACKUP_TZ; DST gaps run an hour later, repeats run once
  Archives/                    BackupName, BackupArchive (SHA256SUMS + manifest.json + tar), RetentionPolicy
  Runs/                        BackupRun, RestoreRun, TcpServiceProbe, ConsoleConfirmation
  Databases/                   MongoDump (mongodump/mongorestore), PostgresDump (pg_dump/pg_restore)
  Storage/                     LocalStore, S3Store (AWS SDK), DriveStore (Drive API v3)
  Encryption/AgeEncryptor      the age binary
  Health/                      HealthStamps, HttpPing
  GoogleDrive/DriveDeviceLogin OAuth device flow for gdrive-login
  Dockerfile                   build: self-contained single-file publish for TARGETARCH
                               age:   age + age-keygen (pinned, sha256 in age.sha256)
                               target mongo:    FROM mongo:8.3.11  + /opt/backup + age
                               target keycloak: FROM postgres:17.11 + /opt/backup + age + CA bundle
test/PSPad.Backup.Tests/       unit tests for all of the above, with fakes for stores, dumps and age
docker/backup/test/
  run.sh                       black-box checks against both images
  roundtrip.sh                 both stacks through S3, wiped and restored
```

The worker owns everything except the dump itself and the cipher. Those are
`mongodump`/`mongorestore`, `pg_dump`/`pg_restore` (already in each base
image) and `age`, run as child processes. There's no .NET library that
takes a consistent point-in-time dump of either database, nor a mature .NET
age implementation. The binary is self-contained, so the base images need no
.NET runtime. The containers run as uid 10001 (`pspad-backup`).

The API only learns about restores through the dataset id (§7). The worker
references `PSPad.Infrastructure` for `DatasetIdentity.ForgetAsync`, so the
API and the restore share one definition of where that id lives.

Every licence is GPL-3-compatible: AWS SDK for .NET and Google APIs .NET
client (Apache-2.0), age (BSD-3), MongoDB Database Tools (Apache-2.0) and the
PostgreSQL client (PostgreSQL licence). CI builds and publishes both images
next to `pspad-api` and `pspad-app`.

### Reaching the database

The Keycloak stack creates the network (`name: psplace`). The PSPad stack
joins it as `external: true`, so Keycloak starts first. Each backup service
reaches its own database by service name inside its own stack. The API
reaches Keycloak as `http://keycloak:8080` over `psplace`, as it does
today. No database port is published.

```
docker compose -f docker/keycloak/compose.yaml --profile backup up -d
docker compose -f docker/compose.yaml --profile backup up -d
```

When a database lives elsewhere (production's external
`MONGO_CONNECTIONSTRING`, a managed Postgres), its backup service takes a
URI instead.

## 3. One run (both stacks)

1. **Dump.** This step is stack-specific:
   - **mongo:** `mongodump --uri "$BACKUP_MONGO_URI" --oplog --gzip --out`
     produces a `mongo/` directory. `--oplog` makes the dump a single point
     in time across collections. Documents, `events`, `counters`, Statistics
     records and the dispatch marker (ADR-0036, ADR-0037) must agree.
     `--oplog` only works on a whole-instance dump, and its replay refuses
     namespace filters. The dump therefore deletes `admin/` and `config/`
     before packaging: restoring `admin` would replace the very user the
     restore is signed in as, and the oplog replay would then be refused.
     It connects as the stack's root user `pspad` for now. Once #143 adds
     dedicated Mongo users, it switches to `pspad-backup` with the `backup`
     and `restore` roles.
   - **keycloak:** `pg_dump --format=custom "$BACKUP_PG_URI"` produces
     `keycloak.dump`. It connects as Keycloak's own database user, which
     restore needs anyway.
2. **Package.** The files, a `SHA256SUMS` and a `manifest.json` (stack,
   name, run time in UTC, and the size and sha256 of each file) go into
   `<stack>-<yyyy-MM-ddTHHmmssZ>.tar`.
3. **Local.** The tarball goes to `/backups` (the stack's own volume, or a
   bind mount set with `BACKUP_LOCAL_DIR`) unencrypted, so a restore on the
   same host needs no key. The directory is `0700`.
4. **Off-host.** When a remote is configured, the tarball is encrypted once
   with `age -r "$BACKUP_AGE_RECIPIENT"` and uploaded to each remote. On
   S3 it goes under `<prefix>/<stack>/`; on Drive, into
   `<BACKUP_GDRIVE_FOLDER>/<stack>/`. Both stacks can share one bucket or
   folder. The AWS SDK computes checksums only when required, so
   S3-compatible stores without trailing checksums work.
5. **Prune.** Retention reads the time in the file name, not the store's
   modification time. Files older than `BACKUP_KEEP_DAYS` days are deleted, per
   destination. Nothing is pruned on a day whose run failed.
6. **Report.** Each step logs one line. On success it writes
   `/backups/.last-success` and calls `BACKUP_PING_URL` (with `/fail`
   appended on failure).

A failure stops the run and exits non-zero. A failed upload to one remote
doesn't skip the other, but the run still fails.

## 4. Configuration

Both `.env.example` files carry the shared variables. Each adds its own
connection settings.

| Variable | Default | Stack | Meaning |
|----------|---------|-------|---------|
| `BACKUP_AT` | `03:17` mongo, `03:47` keycloak | both | Daily run time (`HH:mm`); see §6 for why Keycloak runs later |
| `BACKUP_TZ` | `UTC` | both | Time zone for `BACKUP_AT` |
| `BACKUP_KEEP_DAYS` | `14` | both | Retention at every destination |
| `BACKUP_LOCAL_DIR` | — | both | Host path to bind instead of the stack's volume |
| `BACKUP_AGE_RECIPIENT` | — | both | age public key; required once any remote is set |
| `BACKUP_AGE_IDENTITY_FILE` | — | both | Host path of the age private key, mounted read-only at `/run/secrets/age-identity`; only for restoring from a remote |
| `BACKUP_S3_ENDPOINT`, `_REGION`, `_BUCKET`, `_PREFIX`, `_ACCESS_KEY_ID`, `_SECRET_ACCESS_KEY` | — | both | S3-compatible remote (AWS, B2, R2, MinIO); enabled when `_BUCKET` is set |
| `BACKUP_REMOTE_PRUNE` | `true` | both | `false` for write-only keys; retention moves to a bucket lifecycle rule |
| `BACKUP_GDRIVE_CLIENT_ID`, `_CLIENT_SECRET`, `_REFRESH_TOKEN`, `_FOLDER` | —, —, —, `pspad-backups` | both | Google Drive remote; enabled when `_REFRESH_TOKEN` is set, which then requires the client |
| `BACKUP_PING_URL` | — | both | Called on success, with `/fail` appended on failure |
| `BACKUP_MONGO_URI` | `mongodb://pspad:${MONGO_PASSWORD}@mongo:27017/?authSource=admin&directConnection=true` | mongo | Whole instance, not one database (for `--oplog`) |
| `BACKUP_MONGO_DATABASE` | `${MONGO_DATABASE}` | mongo | The database a restore empties and confirms by name |
| `BACKUP_SERVICE_PROBE` | `api:8080` / `keycloak:8080` | both | Set by compose: a restore refuses while this still accepts connections |
| `BACKUP_PG_URI` | `postgresql://keycloak:${KEYCLOAK_DB_PASSWORD}@keycloak-db:5432/keycloak` | keycloak | Keycloak's database |

The age key can be the same in both stacks or different. The docs
recommend one key pair, kept off the server, for simplicity.

**Fail fast**, with one decisive `backup:` line each:

- when scheduling or running a backup: a remote is configured without a
  recipient, or the recipient isn't a valid age key. A restore only needs the
  identity;
- the URI is empty, or `BACKUP_MONGO_DATABASE` is;
- `BACKUP_AT` doesn't parse, or `BACKUP_KEEP_DAYS` isn't a whole number;
- for keycloak, at dump or restore time: the target doesn't answer as
  Postgres within 10 s (§5).

**Google Drive.** It uses the `drive.file` scope, so the worker sees only
files it created. That is why it creates its own folder rather than taking
a folder id: a folder the user made is invisible to it. The refresh token
comes from `gdrive-login`, an OAuth device flow. It prints a URL and a
code, polls until the user approves on any device, then prints
`BACKUP_GDRIVE_REFRESH_TOKEN=…`. Two pitfalls are documented:

- The OAuth client must be of type *TVs and Limited Input devices*. Google
  allows `drive.file` for that type.
- A client left in *Testing* issues refresh tokens that expire after 7 days.

## 5. Keycloak on Postgres

`pg_dump` is the one supported path. It's consistent while Keycloak runs,
and it matches production.

The Keycloak stack keeps `start-dev` and moves only its database:
`KC_DB=postgres` against `keycloak-db` (`postgres:17`, volume
`keycloak-db-data`). Dev mode accepts any database, so this change is
independent of #138's switch to `start`.

There is no migration from the old H2 volume. The new stack starts from
`realm-psplace.json`. Production already runs Keycloak on Postgres from its
own repository, so no deployment holding real accounts goes through this
change.

## 6. Two stacks, one restore

The stores are dumped at different times, so the pair is never one atomic
snapshot. That's safe in one direction only:

- **Keycloak newer than Mongo:** extra accounts, or accounts with no PSPad
  data yet. Those users sign in and sync an empty or older dataset.
  Harmless.
- **Keycloak older than Mongo:** an account created between the two dumps
  is missing, and its documents are orphaned (ADR-0026: `sub` is the PSPad
  user id).

So Keycloak runs **after** Mongo (default 30 minutes later). The restore
docs say to pair a Mongo backup with the first Keycloak backup taken at or
after it.

Restoring `latest` in both stacks from the same night satisfies this by
default. Each restore prints its backup's timestamp. `restore list` shows
the backups at each destination, so the operator can pick a pair by hand.

Restoring only one store is supported:

- **Keycloak alone:** for example, a lost realm while Mongo is fine. Sync
  isn't affected.
- **Mongo alone:** for example, a bad PSPad upgrade while accounts are
  fine. Clients re-sync through §7.

## 7. Mongo restore and the dataset id

A Mongo restore rolls `counters.events` back. Every open client holds a
marker above it, so it would pull nothing until the server overtakes that
marker. It would also keep documents that the restored server no longer
has. AD-6 only holds if the client learns the dataset changed.

**Server.** A `meta` collection holds `{ _id: "dataset", value: <Guid> }`.
`SyncReader` reads it, creating it on first use, and returns it as
`SyncResponse.DatasetId`.

**Client.** `SyncService` stores the id in replica meta next to the marker
and the collections fingerprint (ADR-0049):

- If the stored id is missing, it records the id it receives. Existing
  installs upgrade with no purge.
- If the stored id differs, the sync discards that response. It clears
  every replica collection and the marker, and the Statistics and snapshot
  caches. It keeps the recorded owner and the outbox, records the new id,
  and pulls with `since = 0`.
- The outbox replays under last-write-wins (AD-5). Commands that refer to
  aggregates the restore lost come back rejected and are surfaced.

**Restoring.** Restore runs as a one-off of the backup service. The
operator stops the writer first; the backup container never gets the
Docker socket.

```
docker compose -f docker/compose.yaml stop api
docker compose -f docker/compose.yaml run --rm mongo-backup restore latest [--from local|s3|gdrive] [--yes]
docker compose -f docker/compose.yaml start api
```

`restore <name|latest>`:

1. Refuse if `BACKUP_SERVICE_PROBE` (`api:8080`) still accepts connections.
   This is a bash `/dev/tcp` probe over the stack network, and the message
   names `docker compose stop api`.
2. Fetch the backup. From a remote, decrypt it with the private key mounted
   from `BACKUP_AGE_IDENTITY_FILE`. Verify `SHA256SUMS`.
3. Ask for confirmation by typing the database name, unless `--yes` is
   passed. Without a terminal, `--yes` is required.
4. Drop the PSPad database, then run
   `mongorestore --oplogReplay --drop --gzip --dir`. The dump holds no
   `admin`, so Mongo's users and `MONGO_PASSWORD` stay as they are now.
5. Delete `meta.dataset`.
6. Log the name of the backup it restored.

Keycloak is the same sequence in its own stack: `stop keycloak`, then
`run --rm keycloak-backup restore latest`, which makes the same probe
against `keycloak:8080` and runs
`pg_restore --clean --if-exists --single-transaction`, then
`start keycloak`. It has no dataset-id step.

## 8. Testing

- **Unit** (`[UnitTest]`, `test/PSPad.App.Tests`):
  - `SyncService` records the first dataset id without purging.
  - A changed id clears the replica and marker, keeps the outbox and owner,
    and re-pulls from 0.
  - An equal id changes nothing.
- **Integration** (`[IntegrationTest]`, `test/PSPad.Api.Tests`):
  - `GET /api/sync` returns a stable `datasetId`.
  - Concurrent first reads agree on one id.
  - `ForgetAsync` makes the next read mint a new one.
- **Worker unit tests** (`[UnitTest]`, `test/PSPad.Backup.Tests`):
  - settings and every fail-fast rule;
  - the schedule, including DST gaps and repeats;
  - backup names, and retention by name;
  - pack and verify, including a tampered file;
  - health stamps;
  - `BackupRun`: plain local copy and ciphertext-only uploads; a failed
    upload still tries the other remote, fails the run and prunes nothing;
    pruning; a failed dump;
  - `RestoreRun`: latest, named, from a remote with the identity, the probe
    refusal, a missing identity, an unknown source, no backups, a declined
    confirmation;
  - the Drive device login: pending → approved, denied, wrong client type.
- **Image checks** (`docker/backup/test/run.sh`, CI job `backup-images`).
  Against both images, the script checks:
  - every fail-fast rule in §4;
  - the rendered schedule;
  - a Mongo run with an S3 upload (`rclone serve s3`, from the
    `rclone/rclone` image, stands in for the bucket): the local copy, the success stamp, pruning that leaves other
    stacks' files alone, the remote copy under `<prefix>/<stack>/` as age
    ciphertext;
  - restore refusing while the probe target listens, restore from S3, and
    the dataset id gone;
  - a Keycloak run and restore from local, with row ids kept;
  - refusing a non-Postgres target;
  - the healthcheck before the first run and after a stale success.
- **Round trip** (`docker/backup/test/roundtrip.sh`, same CI job):
  1. Bring up both stacks with the `backup` profile, an `rclone serve s3`
     bucket, and a throwaway age key.
  2. Create a Keycloak user and seed their data, then run `now` in each
     stack.
  3. Assert that both stacks' remote objects exist and are age ciphertext.
  4. `down -v` both stacks, which removes the local backups too, then bring
     them up again.
  5. Restore Keycloak, then Mongo, from `s3`.
  6. Assert that the user has the same UUID, their data is back, and the
     dataset id is gone.

  The round trip stops short of signing in through the API: that would need
  a test client in the shipped realm.

Google Drive isn't exercised in CI, because there's no hermetic fake.

## 9. Documentation

- `install.astro`:
  - two stacks and their start order;
  - **Backups**: profile, schedule, health, age keys, S3 and Google Drive,
    restore order and pairing rule, the re-sync callout;
  - the manual `mongodump` fallback;
  - a note for single-stack installs whose H2 volume isn't carried over.
- `docker/.env.example`, `docker/keycloak/.env.example`: every variable,
  commented.
- `features.astro` and the landing page: nightly backups.
- `README.md`: two stacks.
- `specs/backend-spec.md`: §5 dataset id, and the containers paragraph.

## 10. Open questions

- Should the local copy be optionally encrypted too
  (`BACKUP_LOCAL_ENCRYPT=true`)?
- Should a dump that shrinks by more than 50 % compared with the previous
  run fail the ping, so a silently emptied database is caught before
  retention rotates the good copies out?
