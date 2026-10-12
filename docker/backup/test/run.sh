#!/usr/bin/env bash
set -euo pipefail

usage="usage: run.sh <mongo-backup-image> <keycloak-backup-image>"
mongo_image=${1:?$usage}
keycloak_image=${2:?$usage}
run_id=backup-test-$$
network=$run_id
passed=0
failed=0

cleanup() {
  docker ps -aq --filter "label=$run_id" | xargs -r docker rm -f >/dev/null
  docker network rm "$network" >/dev/null 2>&1 || true
  docker volume ls -q --filter "label=$run_id" | xargs -r docker volume rm >/dev/null
}
trap cleanup EXIT

pass() { passed=$((passed + 1)); echo "ok   $1"; }
fail() { failed=$((failed + 1)); echo "FAIL $1"; echo "     $2"; }

expect_failure() {
  local name=$1 needle=$2; shift 2
  local output
  if output=$(docker run --rm --label "$run_id" "$@" 2>&1); then
    fail "$name" "exited 0: $output"
  elif [[ $output != *"$needle"* ]]; then
    fail "$name" "missing '$needle' in: $output"
  else
    pass "$name"
  fi
}

expect_success() {
  local name=$1 needle=$2; shift 2
  local output
  if ! output=$(docker run --rm --label "$run_id" "$@" 2>&1); then
    fail "$name" "exited non-zero: $output"
  elif [[ $output != *"$needle"* ]]; then
    fail "$name" "missing '$needle' in: $output"
  else
    pass "$name"
  fi
}

docker network create --label "$run_id" "$network" >/dev/null
docker volume create --label "$run_id" "$run_id-mongo-backups" >/dev/null
docker volume create --label "$run_id" "$run_id-keycloak-backups" >/dev/null
docker volume create --label "$run_id" "$run_id-s3" >/dev/null

identity=$(docker run --rm --entrypoint age-keygen "$mongo_image" 2>/dev/null)
recipient=$(grep -o 'age1[0-9a-z]*' <<<"$identity" | head -1)
identity_file=$(mktemp)
printf '%s\n' "$identity" >"$identity_file"
chmod 644 "$identity_file"

mongo_env=(-e BACKUP_MONGO_URI=mongodb://mongo:27017/?directConnection=true -e BACKUP_MONGO_DATABASE=pspad)
keycloak_env=(-e BACKUP_PG_URI=postgresql://keycloak:secret@postgres:5432/keycloak)
s3_env=(-e BACKUP_S3_ENDPOINT=http://s3:8333 -e BACKUP_S3_REGION=us-east-1 -e BACKUP_S3_BUCKET=backups
  -e BACKUP_S3_PREFIX=ci -e BACKUP_S3_ACCESS_KEY_ID=ciaccesskey -e BACKUP_S3_SECRET_ACCESS_KEY=cisecretaccesskey1234)

echo "# configuration"
for image in "$mongo_image" "$keycloak_image"; do
  [[ $image == "$mongo_image" ]] && env=("${mongo_env[@]}") || env=("${keycloak_env[@]}")
  expect_failure "$image: a remote without a recipient is refused" "BACKUP_AGE_RECIPIENT" \
    "${env[@]}" -e BACKUP_S3_BUCKET=b "$image" check
  expect_failure "$image: Google Drive without a recipient is refused" "BACKUP_AGE_RECIPIENT" \
    "${env[@]}" -e BACKUP_GDRIVE_TOKEN='{}' "$image" check
  expect_failure "$image: a recipient that is not an age key is refused" "not an age recipient" \
    "${env[@]}" -e BACKUP_S3_BUCKET=b -e BACKUP_AGE_RECIPIENT=ssh-rsa "$image" check
  expect_failure "$image: an impossible run time is refused" "BACKUP_AT" \
    "${env[@]}" -e BACKUP_AT=25:00 "$image" check
  expect_failure "$image: a non-numeric retention is refused" "BACKUP_KEEP_DAYS" \
    "${env[@]}" -e BACKUP_KEEP_DAYS=two "$image" check
  expect_success "$image: a local-only setup renders the daily schedule" "17 3 * * *" \
    "${env[@]}" -e BACKUP_AT=03:17 "$image" check
done
expect_failure "mongo: a missing connection string is refused" "BACKUP_MONGO_URI" "$mongo_image" check
expect_failure "keycloak: a missing connection string is refused" "BACKUP_PG_URI" "$keycloak_image" check

echo "# services"
docker run -d --label "$run_id" --network "$network" --name mongo --network-alias mongo \
  mongo:8 --replSet rs0 --bind_ip_all >/dev/null
docker run -d --label "$run_id" --network "$network" --name postgres --network-alias postgres \
  -e POSTGRES_DB=keycloak -e POSTGRES_USER=keycloak -e POSTGRES_PASSWORD=secret postgres:17 >/dev/null
docker run -d --label "$run_id" --network "$network" --name s3 --network-alias s3 --user 0 \
  -v "$run_id-s3:/data" --entrypoint rclone "$mongo_image" \
  serve s3 /data --addr :8333 --auth-key ciaccesskey,cisecretaccesskey1234 >/dev/null

for _ in $(seq 1 60); do
  docker exec mongo mongosh --quiet --eval \
    "try { rs.status().ok } catch (e) { rs.initiate({_id:'rs0',members:[{_id:0,host:'mongo:27017'}]}).ok }" 2>/dev/null \
    | grep -q 1 && docker exec mongo mongosh --quiet --eval "db.hello().isWritablePrimary" 2>/dev/null | grep -q true && break
  sleep 1
done
for _ in $(seq 1 60); do docker exec postgres pg_isready -U keycloak -d keycloak >/dev/null 2>&1 && break; sleep 1; done
docker exec s3 mkdir -p /data/backups

mongo_eval() { docker exec mongo mongosh --quiet pspad --eval "$1"; }
pg_eval() { docker exec postgres psql -U keycloak -d keycloak -Atc "$1"; }

backup() {
  local image=$1 volume=$2; shift 2
  docker run --rm --label "$run_id" --network "$network" -v "$volume:/backups" \
    -v "$identity_file:/run/secrets/age-identity:ro" "$@" "$image" "${BACKUP_ARGS[@]}"
}

echo "# mongo round trip"
mongo_eval 'db.areas.insertMany([{_id:1,name:"Home"},{_id:2,name:"Work"}]); db.meta.insertOne({_id:"dataset",value:"old"})' >/dev/null
docker run --rm --label "$run_id" -v "$run_id-mongo-backups:/backups" --entrypoint sh "$mongo_image" \
  -c 'touch -d "30 days ago" /backups/mongo-2000-01-01T000000Z.tar /backups/keep-me.tar' >/dev/null

BACKUP_ARGS=(now)
if out=$(backup "$mongo_image" "$run_id-mongo-backups" "${mongo_env[@]}" "${s3_env[@]}" \
  -e BACKUP_AGE_RECIPIENT="$recipient" -e BACKUP_KEEP_DAYS=14 2>&1); then pass "mongo: a run succeeds"; else fail "mongo: a run succeeds" "$out"; fi

local_files=$(docker run --rm -v "$run_id-mongo-backups:/backups" --entrypoint sh "$mongo_image" -c 'ls -A /backups')
[[ $local_files == *"mongo-"*".tar"* && $local_files == *".last-success"* ]] \
  && pass "mongo: the local copy and the success stamp exist" || fail "mongo: the local copy and the success stamp exist" "$local_files"
[[ $local_files != *"mongo-2000-01-01T000000Z.tar"* ]] \
  && pass "mongo: copies older than the retention are pruned" || fail "mongo: copies older than the retention are pruned" "$local_files"
[[ $local_files == *"keep-me.tar"* ]] \
  && pass "mongo: pruning leaves files of other stacks alone" || fail "mongo: pruning leaves files of other stacks alone" "$local_files"

remote=$(docker exec s3 sh -c 'find /data/backups -type f')
[[ $remote == */backups/ci/mongo/mongo-*.tar.age ]] \
  && pass "mongo: the remote copy lands under prefix/stack" || fail "mongo: the remote copy lands under prefix/stack" "$remote"
[[ $(docker exec s3 sh -c "head -c 21 '$remote'") == "age-encryption.org/v1" ]] \
  && pass "mongo: the remote copy is age ciphertext" || fail "mongo: the remote copy is age ciphertext" "$(docker exec s3 sh -c "head -c 40 '$remote'")"

mongo_eval 'db.dropDatabase()' >/dev/null
BACKUP_ARGS=(restore latest --from s3 --yes)
expect_failure "mongo: restore refuses while the writer still listens" "still accepting connections" \
  --network "$network" -v "$run_id-mongo-backups:/backups" -v "$identity_file:/run/secrets/age-identity:ro" \
  "${mongo_env[@]}" "${s3_env[@]}" -e BACKUP_SERVICE_PROBE=mongo:27017 "$mongo_image" "${BACKUP_ARGS[@]}"
if out=$(backup "$mongo_image" "$run_id-mongo-backups" "${mongo_env[@]}" "${s3_env[@]}" \
  -e BACKUP_SERVICE_PROBE=api:8080 2>&1); then pass "mongo: restore from s3 succeeds"; else fail "mongo: restore from s3 succeeds" "$out"; fi
[[ $(mongo_eval 'db.areas.countDocuments()') == 2 ]] \
  && pass "mongo: restored documents are back" || fail "mongo: restored documents are back" "$(mongo_eval 'db.areas.countDocuments()')"
[[ $(mongo_eval 'db.meta.countDocuments({_id:"dataset"})') == 0 ]] \
  && pass "mongo: restore drops the dataset id" || fail "mongo: restore drops the dataset id" "still present"

echo "# keycloak round trip"
pg_eval "create table user_entity(id text primary key, username text); insert into user_entity values ('u-1','ada')" >/dev/null
BACKUP_ARGS=(now)
if out=$(backup "$keycloak_image" "$run_id-keycloak-backups" "${keycloak_env[@]}" 2>&1); then
  pass "keycloak: a local-only run succeeds"; else fail "keycloak: a local-only run succeeds" "$out"; fi
pg_eval "drop table user_entity" >/dev/null
BACKUP_ARGS=(restore latest --from local --yes)
if out=$(backup "$keycloak_image" "$run_id-keycloak-backups" "${keycloak_env[@]}" -e BACKUP_SERVICE_PROBE=keycloak:8080 2>&1); then
  pass "keycloak: restore from local succeeds"; else fail "keycloak: restore from local succeeds" "$out"; fi
[[ $(pg_eval "select id from user_entity") == "u-1" ]] \
  && pass "keycloak: restored rows keep their ids" || fail "keycloak: restored rows keep their ids" "$(pg_eval 'select id from user_entity' 2>&1)"
expect_failure "keycloak: a database that is not Postgres is refused" "is not a reachable Postgres" \
  --network "$network" -e BACKUP_PG_URI=postgresql://keycloak:secret@mongo:27017/keycloak "$keycloak_image" now

echo "# health"
expect_success "a fresh container is healthy before its first run" "healthy" "${mongo_env[@]}" --entrypoint sh "$mongo_image" \
  -c 'backup start-clock && backup healthcheck && echo healthy'
expect_failure "a container whose last success is stale is unhealthy" "no successful backup" "${mongo_env[@]}" --entrypoint sh "$mongo_image" \
  -c 'touch -d "2 days ago" /tmp/.backup-started /backups/.last-success && backup healthcheck'

rm -f "$identity_file"
echo "passed $passed, failed $failed"
[[ $failed -eq 0 ]]
