# Daily Backups and Two Compose Stacks — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task by task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close #120. The repository runs two compose stacks joined by the
`psplace` network: PSPad, and Keycloak on Postgres. Each stack backs itself
up nightly to a local directory, and optionally to S3 and Google Drive,
with age encryption before anything leaves the host. A Mongo restore makes
every client re-pull through a server dataset id.

**Architecture:**
- **Dataset id.** This is the only .NET change. A `meta` document is read,
  or created, before the sync snapshot transaction. It's returned as
  `SyncResponse.DatasetId`. The client compares it with replica meta and
  resets the replica on a mismatch.
- **Stacks.** Keycloak and a new `keycloak-db` leave `docker/compose.yaml`
  for `docker/keycloak/compose.yaml`, which owns the `psplace` network.
  `api` joins it as external.
- **Backups.** One multi-stage `docker/backup/Dockerfile` with targets
  `mongo` (FROM `mongo:8`) and `keycloak` (FROM `postgres:17`). They share
  shell scripts for package, encrypt, upload, prune, health and ping, under
  supercronic. One `backup` service in each stack, behind the `backup`
  profile.

**Tech stack:**
- .NET 10, MongoDB.Driver, xUnit, Testcontainers.
- Docker Compose, Postgres 17, Keycloak 26 `start-dev`.
- rclone, age, supercronic, MongoDB Database Tools, `pg_dump`/`pg_restore`.
- MinIO, in CI only.

**Spec:** `specs/backup-design.md`. **ADRs:** `adr/0061-…`, `adr/0062-…`
(both Proposed; set them to Active in the last task).

## Global constraints

- AGENTS.md rules:
  - No comments in code or scripts, except one line for a
    counter-intuitive *why*.
  - One type per file.
  - Every test class has `[UnitTest]` or `[IntegrationTest]`. Integration
    tests join `[Collection(MongoCollection.Name)]` and take
    `MongoFixture`.
- TDD: write the failing test first, run it, and confirm it fails for the
  stated reason.
- `TreatWarningsAsErrors` is on.
- `PSPad.Module.Tasks` isn't touched.
- Pin every image and binary to an exact version: look up the current
  release when you implement, never `latest` or a floating major. Verify
  downloaded binaries with sha256 in the Dockerfile.
- Shell scripts are bash with `set -euo pipefail`. Every failure prints one
  decisive line prefixed with `backup:`.
- Backup containers never get the Docker socket.
- Docs ship in the same PR (AGENTS.md §6): `install.astro`, both
  `.env.example` files, `features.astro`, `README.md`.
- Check that Docker is available before Phase B
  (`docker compose version`). Without it, Phases B–D are implemented, and
  `docker compose config` plus the CI job become the only proof. Say so in
  the report.

---

## Phase A — Dataset id (.NET)

### Task 1: `SyncResponse.DatasetId`

**Files:** Modify `src/shared/PSPad.Contracts/SyncResponse.cs`

- [ ] Add `Guid? DatasetId = null` as the **last** positional parameter,
  after `MemberListIds`, so existing constructors and older clients keep
  compiling and deserialising.
- [ ] Run `dotnet build`. It should be green; nothing consumes the field
  yet.

### Task 2: Server reads or creates the dataset id

**Files:**
- Create `src/shared/PSPad.Infrastructure/Mongo/DatasetIdentity.cs`
- Modify `src/PSPad.Api/Sync/SyncReader.cs`
- Modify `src/PSPad.Api/Program.cs` (register `DatasetIdentity` as a
  singleton)
- Test `test/PSPad.Api.Tests/Endpoints/SyncEndpointTests.cs`
- Test `test/PSPad.Api.Tests/Sync/DatasetIdentityTests.cs` (new)

**Interface:** `DatasetIdentity(MongoContext).CurrentAsync(CancellationToken) → Task<Guid>`

- [ ] **Failing tests** (`[IntegrationTest]`):
  - `DatasetIdentityTests.TheSameIdComesBackOnEveryRead`: two calls are
    equal.
  - `DatasetIdentityTests.ConcurrentFirstReadsAgreeOnOneId`: run 10
    parallel `CurrentAsync` calls against an empty `meta` and assert one
    distinct value.
  - `DatasetIdentityTests.DeletingTheDocumentMintsANewId`: read, delete
    `{_id:"dataset"}` from `meta`, read again, and assert the values
    differ.
  - `SyncEndpointTests.EverySyncCarriesTheSameDatasetId`: two
    `GET /api/sync` calls return the same non-null `DatasetId`.
- [ ] **Implement** `FindOneAndUpdateAsync` on collection `meta`, filter
  `_id == "dataset"`, update `$setOnInsert: { value: Guid.NewGuid() }`,
  with `IsUpsert = true` and `ReturnDocument.After`.
  - Catch `MongoCommandException` with code 11000 (a duplicate key from a
    racing upsert) and re-read once.
  - Store the Guid with the standard representation the context already
    uses for other Guids.
- [ ] In `SyncReader.ReadAsync`, call `CurrentAsync` **before**
  `StartSessionAsync`. An upsert inside the snapshot transaction would
  write-conflict on the first concurrent syncs. Pass the id through to the
  returned `SyncResponse`.
- [ ] Run `dotnet test --project test/PSPad.Api.Tests --filter "FullyQualifiedName~DatasetIdentity|FullyQualifiedName~SyncEndpoint"`.
  It should be green.
- [ ] Commit: `feat(sync): server dataset id on every sync response (#120)`

### Task 3: Replica stores the dataset id

**Files:**
- Modify `src/PSPad.App/State/Replica/IReplica.cs`
- Modify `InMemoryReplica.cs` and `IndexedDbReplica.cs` in the same folder

- [ ] Add `Task<Guid?> DatasetIdAsync()` and
  `Task SetDatasetIdAsync(Guid id)`.
  - `IndexedDbReplica` uses `getMeta`/`setMeta` with key `"datasetId"`,
    mirroring `CollectionsFingerprintAsync`.
  - `InMemoryReplica` uses a field that `ClearAsync` also resets (check
    that it resets the others the same way).
- [ ] No new test here: Task 5's tests drive these through
  `InMemoryReplica`. `IndexedDbReplica` has no unit tests today; keep it
  that way.

### Task 4: `DatasetReset`

**Files:**
- Create `src/PSPad.App/Sync/IDatasetReset.cs`
- Create `src/PSPad.App/Sync/DatasetReset.cs`
- Modify `src/PSPad.App/Program.cs` (register it as scoped)
- Test `test/PSPad.App.Tests/Sync/DatasetResetTests.cs`

**Interface:** `IDatasetReset.ResetAsync(Guid datasetId) → Task`. It
clears the replica, the Statistics cache and the snapshot cache, restores
the recorded owner, and records `datasetId`. It never touches the outbox.

- [ ] **Failing test** (`[UnitTest]`), using `InMemoryReplica`,
  `InMemoryOutbox` and fakes for the two caches. Follow how
  `ReplicaOwnership` tests build `StatisticsCache`; if it needs
  `IJSRuntime`, use the existing test double.
  - Seed an owner, a marker, a document and one outbox entry, then call
    `ResetAsync(x)`.
  - Assert: the document is gone, the marker is 0, the owner is unchanged,
    the dataset id is `x`, the outbox still has its entry, and both caches
    were cleared.
- [ ] Implement it, reusing `replica.ClearAsync()`. That clears meta,
  including the owner, which is why the owner is re-set afterwards.
- [ ] Commit with Task 5.

### Task 5: `SyncService` reacts to a new dataset id

**Files:**
- Modify `src/PSPad.App/Sync/SyncService.cs` (add an `IDatasetReset`
  constructor parameter)
- Test `test/PSPad.App.Tests/Sync/SyncServiceTests.cs`: update
  `ServiceFor` and add a `FakeDatasetReset` that records calls and
  delegates to a real `DatasetReset` over the same `InMemoryReplica`

- [ ] **Failing tests:**
  - `AFirstDatasetIdIsRecordedWithoutAReset`: the replica has no id and the
    response carries `A`. The id becomes `A`, there's no reset, and the
    marker advances as today.
  - `ANewDatasetIdResetsTheReplicaAndPullsFromZero`: the replica holds id
    `A`, marker 40 and a document, and the outbox has an entry. The first
    response carries `B`.
    - Assert that one reset happened with `B`.
    - Assert that a second `SyncAsync(0, …)` call was made.
    - Assert that the stale document is gone and the outbox entry
      survives.
    - Assert that the final marker is the second response's.
  - `AnUnchangedDatasetIdChangesNothing`: no reset happens, and `since` is
    the stored marker.
  - `AResponseWithoutADatasetIdChangesNothing`: an old server returns
    `null`, and no reset happens.
  - Extend `FakeApi` so `Pull` can be a queue of responses, so the re-pull
    returns different data.
- [ ] **Implement** in `PullAsync`, right after the null check:

  ```
  if (response.DatasetId is { } current)
  {
      var known = await replica.DatasetIdAsync();
      if (known is null) await replica.SetDatasetIdAsync(current);
      else if (known != current)
      {
          await datasetReset.ResetAsync(current);
          response = await api.SyncAsync(0, []);
          if (response is null) return null;
      }
  }
  ```

  The rest of `PullAsync` continues unchanged with `response`. That
  includes membership reconciliation, which now runs with the owner
  restored. Extract the block into a private method so `PullAsync` stays
  readable.
- [ ] Run `dotnet test --filter Category=Unit`. It should be green.
- [ ] Commit: `feat(sync): a new dataset id resets the replica and re-pulls (#120)`

### Task 6: Backend spec

- [ ] `specs/backend-spec.md` §5: add one paragraph after the fingerprint
  paragraph covering `datasetId`, the reset rule, and that the outbox is
  kept (link `adr/0062`).
- [ ] Commit together with Task 5 or on its own.

---

## Phase B — Two stacks

### Task 7: Move the realm import

**Files:**
- Move `docker/keycloak/realm-psplace.json` to
  `docker/keycloak/import/realm-psplace.json`
- Modify `test/PSPad.App.Tests/Auth/RealmConfigurationTests.cs:35`

- [ ] Update the path in the test. Run it red before the move and green
  after.

  Reason: the new `docker/keycloak/` also holds `compose.yaml` and `.env`,
  and mounting the whole folder into Keycloak would hand it the `.env`.

### Task 8: `docker/keycloak/compose.yaml`

**Files:** Create `docker/keycloak/compose.yaml` and
`docker/keycloak/.env.example`

- [ ] `name: keycloak`.
- [ ] **`keycloak-db`:**
  - `postgres:17.<x>` (pinned);
  - `POSTGRES_DB=keycloak`, `POSTGRES_USER=keycloak`,
    `POSTGRES_PASSWORD=${KEYCLOAK_DB_PASSWORD:?}`;
  - volume `keycloak-db-data`;
  - healthcheck `pg_isready -U keycloak`;
  - network `keycloak` only, with no port published.
- [ ] **`keycloak`:**
  - `quay.io/keycloak/keycloak:26.0`; leave the version bump to #143;
  - `start-dev --import-realm`;
  - `KC_DB=postgres`, `KC_DB_URL=jdbc:postgresql://keycloak-db:5432/keycloak`,
    `KC_DB_USERNAME=keycloak`, `KC_DB_PASSWORD`;
  - carry over every existing env var from `docker/compose.yaml`
    unchanged;
  - mount `./import:/opt/keycloak/data/import:ro`;
  - `depends_on: keycloak-db: service_healthy`;
  - port `8080:8080`;
  - networks `keycloak` and `psplace`;
  - no data volume: state now lives in Postgres.
- [ ] Networks:
  - `psplace: { name: psplace }`; this stack creates it;
  - `keycloak: {}`.
- [ ] `.env.example`: the Keycloak variables moved out of
  `docker/.env.example`, with their comments, plus `KEYCLOAK_DB_PASSWORD`
  (left blank, with a "set before first start" comment) and the backup
  variables (Task 13).
  - `KEYCLOAK_ADMIN_USER`/`PASSWORD` stay in **both** files, because the
    API reads them for account deletion. The comment in each file says to
    keep them equal.
- [ ] `docker compose -f docker/keycloak/compose.yaml --env-file docker/keycloak/.env.example config -q`
  should exit 0 once a placeholder password is supplied.

### Task 9: Slim `docker/compose.yaml`

- [ ] Remove the `keycloak` service and the `keycloak-data` volume.
- [ ] `api` joins networks `pspad` and `psplace`. Declare
  `psplace: { external: true }`.
  - `KEYCLOAK_INTERNAL_AUTHORITY=http://keycloak:8080/...` keeps working
    over `psplace`.
- [ ] Remove the Keycloak-only variables from `docker/.env.example`
  (`KEYCLOAK_HOSTNAME`). Keep the ones the API and app read.
- [ ] Run `config -q` on both files.
- [ ] **Smoke test, if Docker is available:**
  1. `up -d` Keycloak, then PSPad.
  2. Confirm that
     `curl -sf localhost:8080/realms/psplace/.well-known/openid-configuration`
     and `curl -sf localhost:5000/health` both succeed.
  3. Confirm the API logs show JWKS fetched from `keycloak:8080`.
- [ ] Commit: `feat(docker): Keycloak and its Postgres become their own stack (#120)`

### Task 10: (dropped — no Keycloak migration)

The new Keycloak stack starts fresh from `realm-psplace.json`. The old
`pspad_keycloak-data` volume isn't migrated; production's accounts already
live in Postgres in their own repository.

---

## Phase C — Backup image and services

### Task 11: Script test harness

**Files:** Create `docker/backup/test/run.sh` and
`docker/backup/test/cases/*.sh`

- [ ] A minimal bash harness. Each case builds nothing itself: it runs the
  image passed as `$1` with
  `docker run --rm -e … <image> <command>`, then asserts on the exit code
  and on the `backup:` line.
- [ ] The first cases fail until Task 12 exists:
  - a remote is set without `BACKUP_AGE_RECIPIENT`: exit 1, and the
    message names the variable;
  - the recipient isn't an `age1…` key: exit 1;
  - `BACKUP_AT=25:00`: exit 1;
  - the URI is empty: exit 1;
  - valid local-only env with `entrypoint check`: exit 0, and the rendered
    crontab line is `17 3 * * *`.

### Task 12: `docker/backup/Dockerfile`, shared scripts

**Files:**
- `docker/backup/Dockerfile`
- `docker/backup/lib.sh`
- `docker/backup/entrypoint.sh`
- `docker/backup/healthcheck.sh`

- [ ] **Stage `tools`** (`debian:bookworm-slim`, pinned): download pinned
  `rclone`, `age`/`age-keygen` and `supercronic` release binaries for
  `TARGETARCH`, `sha256sum -c` each one, and put them in `/out/bin`.
- [ ] **Target `mongo`:** FROM `mongo:8.0.<x>` (pinned; it carries
  `mongodump` and `mongorestore`). Copy `/out/bin`, the shared scripts and
  `mongo/*.sh`, and run as a non-root `backup` user (uid 10001).
- [ ] **Target `keycloak`:** FROM `postgres:17.<x>` (the same pin as
  `keycloak-db`, so `pg_dump` ≥ server). Otherwise the same as `mongo`.
- [ ] **`entrypoint.sh <run|check|now|restore|list>`:**
  - Validate env (Task 11 cases).
  - Render the rclone remotes as `RCLONE_CONFIG_S3_*` and
    `RCLONE_CONFIG_GDRIVE_*` environment variables:
    - S3: `type=s3`, `provider=Other`, endpoint, region, keys.
    - Drive: `type=drive`, `scope=drive.file`, `token`,
      `root_folder_id`, and the optional client id and secret.
  - Touch `/backups/.started`.
  - `run`: write `/tmp/crontab` (`M H * * * entrypoint now`) and exec
    `supercronic -no-reap /tmp/crontab`, with `TZ=$BACKUP_TZ`.
- [ ] **`lib.sh`:**
  - `package` → tar + `manifest.json`, via `sha256sum` and `jq`. Install
    `jq` in both targets, or build the JSON with `printf`; `printf` is
    preferred, so there's one less package.
  - `encrypt` → `age -r`.
  - `upload <remote>` → `rclone copyto --s3-no-check-bucket` to
    `<remote>:<bucket|folder>/<prefix>/<stack>/`.
  - `prune_local` → `find -mtime +N -delete`.
  - `prune_remote` → `rclone delete --min-age Nd --include "<stack>-*"`.
    Skip it when `BACKUP_REMOTE_PRUNE=false`.
  - `ping ok|fail`.
  - `now` runs the steps in order. Make sure an upload failure to one
    remote still attempts the other, and the run still fails. Pruning only
    runs when everything earlier succeeded.
- [ ] **`healthcheck.sh`:** healthy if `.last-success` is younger than
  26 h, or there's no success yet and `.started` is younger than 26 h.
- [ ] Run `docker build --target mongo`, then `--target keycloak`, then
  `docker/backup/test/run.sh <image>` for both. It should be green.

### Task 13: Mongo dump, restore and service

**Files:**
- `docker/backup/mongo/dump.sh`
- `docker/backup/mongo/restore.sh`
- Modify `docker/compose.yaml` and `docker/.env.example`

- [ ] **`dump.sh`:**
  `mongodump --uri "$BACKUP_MONGO_URI" --oplog --archive="$WORK/mongo.archive.gz" --gzip`.
- [ ] **`restore.sh`:**
  1. Resolve `latest`, or a name, at `--from`.
  2. Fetch it. Decrypt it with `/run/secrets/age-identity` if it's
     `.age`.
  3. Verify the manifest.
  4. Run the `/dev/tcp/api/8080` probe, and refuse if it connects.
  5. Confirm by typing `MONGO_DATABASE`, unless `--yes` is passed.
  6. `mongorestore --oplogReplay --drop --archive --gzip`.
  7. `mongosh "$BACKUP_MONGO_URI" --eval 'db.getSiblingDB(process.env.MONGO_DATABASE).meta.deleteOne({_id:"dataset"})'`.
  8. Print the backup timestamp.
- [ ] **Service `mongo-backup`:**
  - `build: { context: ./backup, target: mongo }`, `profiles: [backup]`;
  - env from `.env`; `BACKUP_MONGO_URI` defaults to the root URI with
    `MONGO_PASSWORD`;
  - volume `mongo-backups:/backups`, or `${BACKUP_LOCAL_DIR}` when set
    (document the bind form);
  - optional `secrets: age-identity` from `${BACKUP_AGE_IDENTITY_FILE:-/dev/null}`;
    check that compose accepts `/dev/null`, and otherwise document adding
    the secret only when restoring from a remote;
  - healthcheck `healthcheck.sh`;
  - `depends_on: mongo: service_healthy`;
  - network `pspad`.
- [ ] `.env.example`: every variable in spec §4 for this stack, each with a
  one-line comment in the file's style.
- [ ] Add round-trip cases to the harness, using a throwaway Mongo replica
  set container and MinIO: seed, `now`, check the local file and the
  remote `.age`, drop the DB, `restore latest --from s3 --yes`, then check
  that the documents are back and `meta.dataset` is gone.
- [ ] Commit: `feat(backup): nightly encrypted MongoDB backups (#120)`

### Task 14: Keycloak dump, restore and service

**Files:**
- `docker/backup/keycloak/dump.sh`
- `docker/backup/keycloak/restore.sh`
- Modify `docker/keycloak/compose.yaml` and its `.env.example`

- [ ] **`dump.sh`:** `pg_dump --format=custom --file="$WORK/keycloak.dump" "$BACKUP_PG_URI"`.
  Fail fast if `psql -Atc 'select 1'` fails. That's the "not Postgres"
  case.
- [ ] **`restore.sh`:** the same flow as Mongo, with a probe of
  `keycloak:8080` and `pg_restore --clean --if-exists --single-transaction --dbname "$BACKUP_PG_URI"`.
  It has no dataset-id step.
- [ ] **Service `keycloak-backup`** in the Keycloak stack:
  - target `keycloak`, profile `backup`, default `BACKUP_AT=03:47`;
  - `depends_on: keycloak-db`;
  - network `keycloak`;
  - volume `keycloak-backups`.
- [ ] Harness round trip against a throwaway Postgres.
- [ ] Commit: `feat(backup): nightly encrypted Keycloak backups (#120)`

### Task 15: CI

**Files:** Modify `.github/workflows/ci.yml`

- [ ] **`backup-image` job** (needs `build-and-unit-test`):
  - build both targets;
  - run `docker/backup/test/run.sh` against each;
  - on `main` push, publish `ghcr.io/psp515/pspad-backup-mongo` and
    `ghcr.io/psp515/keycloak-backup`, tagged `latest` and the commit sha,
    mirroring `api-image`.
- [ ] **`backup-roundtrip` job** (needs `backup-image`, `api-image`):
  1. Create `.env` files from the examples with generated secrets and a
     generated age key (`age-keygen`).
  2. Start MinIO (pinned) on `psplace` and create the bucket with
     `mc` (pinned) or the `rclone mkdir` of the image under test.
  3. `up -d --profile backup` both stacks.
  4. Create a Keycloak user through the admin API and get a token
     (password grant on a test client, or the admin token plus
     impersonation; pick the simplest one the realm allows, and don't
     loosen the shipped realm for it). Seed an area through
     `/api/commands`, record `sub` and `datasetId`.
  5. `run --rm keycloak-backup now`, then `run --rm mongo-backup now`.
  6. `down -v` both stacks, then `up` again. Stop `api` and `keycloak`.
     Restore Keycloak, then Mongo, with `--from s3 --yes` and the identity
     mounted. Start both.
  7. Assert that the same `sub` signs in, the seeded area syncs back,
     `datasetId` differs, and the MinIO objects don't start with `ustar`
     at offset 257.
- [ ] If step 4 turns out to need realm changes, stop and ask before
  adding a test-only client to the shipped realm.

---

## Phase D — Docs and wrap-up

### Task 16: `.env.example` comments

- [ ] Review both files against spec §4. Every variable has a comment that
  says what breaks if it's wrong.

### Task 17: `install.astro`

- [ ] **Install steps:** two `.env` files, `up` Keycloak first, then
  PSPad, and why (`psplace` is created by the Keycloak stack).
  - `install.astro:8` reads `docker/compose.yaml` to show it verbatim. Add
    the Keycloak compose next to it the same way.
- [ ] **Rewrite Backups:**
  - the two backup services and the profile;
  - `age-keygen`, keeping the private key off the server;
  - S3 setup (with versioning or object lock, and the write-only-keys
    option);
  - Google Drive setup (`rclone authorize "drive"`, the `drive.file`
    scope, and the two pitfalls);
  - health and ping;
  - restore order and the pairing rule;
  - the manual `mongodump` fallback;
  - the `down -v` callout updated (accounts now live in
    `keycloak_keycloak-db-data`, in the Keycloak stack, and the old
    `pspad_keycloak-data` volume is no longer used);
- [ ] Build the docs site (`docs/`: `npm ci && npm run build`).

### Task 18: Remaining docs

- [ ] `README.md:85,100`: two `up` commands.
- [ ] `specs/backend-spec.md:755`: containers line.
- [ ] `docs/src/pages/features.astro` and the landing page: one line,
  "Nightly encrypted backups, on the host and off it".
- [ ] `adr/0061`, `adr/0062`: status `Active`, with the README index rows
  to match.
- [ ] `AGENTS.md` §4: add a row,
  `| 14 | Two compose stacks; each backs itself up nightly; restore rotates dataset id | 0061, 0062 |`.
- [ ] `specs/backup-design.md`: `Status: Built`.

## Final verification

- [ ] `dotnet build`
- [ ] `dotnet test --filter Category=Unit`
- [ ] `dotnet test --project test/PSPad.Api.Tests --filter Category=Integration`
- [ ] `docker compose … config -q` for both stacks, with and without
  `--profile backup`
- [ ] The harness, against both images
- [ ] The docs build
- [ ] Re-read the diff adversarially: no secrets in examples, no floating
  tags, no Docker socket, no plaintext upload path.
- [ ] Push, open the PR referencing #120, and mention #138/#143 as
  follow-ups (`start` mode, dedicated Mongo users).
