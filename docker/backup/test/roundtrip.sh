#!/usr/bin/env bash
set -euo pipefail

repo=$(cd "$(dirname "$0")/../../.." && pwd)
work=$(mktemp -d)
build_images=${ROUNDTRIP_BUILD:-true}
keycloak_url=http://localhost:8080

kc() { docker compose -f "$repo/docker/keycloak/compose.yaml" --env-file "$work/keycloak.env" --profile backup "$@"; }
ps() { docker compose -f "$repo/docker/compose.yaml" --env-file "$work/pspad.env" --profile backup "$@"; }
step() { echo "== $*"; }
die() { echo "FAIL: $*"; exit 1; }

cleanup() {
  docker rm -f roundtrip-s3 >/dev/null 2>&1 || true
  ps down -v >/dev/null 2>&1 || true
  kc down -v >/dev/null 2>&1 || true
  docker volume rm roundtrip-s3 >/dev/null 2>&1 || true
  rm -rf "$work"
}
trap cleanup EXIT

wait_for_keycloak() {
  for _ in $(seq 1 120); do
    curl -sf "$keycloak_url/realms/psplace/.well-known/openid-configuration" >/dev/null && return 0
    sleep 2
  done
  kc logs keycloak | tail -30
  die "Keycloak never became ready"
}

admin_token() {
  curl -sf -d client_id=admin-cli -d username=admin -d password=admin -d grant_type=password \
    "$keycloak_url/realms/master/protocol/openid-connect/token" | jq -r .access_token
}

mongo_eval() {
  ps exec -T mongo mongosh --quiet -u pspad -p roundtrip-mongo --authenticationDatabase admin pspad --eval "$1"
}

step "configure both stacks"
cp "$repo/docker/.env.example" "$work/pspad.env"
cp "$repo/docker/keycloak/.env.example" "$work/keycloak.env"
for env in "$work/pspad.env" "$work/keycloak.env"; do
  sed -i -e 's|^BACKUP_S3_ENDPOINT=.*|BACKUP_S3_ENDPOINT=http://roundtrip-s3:8333|' \
    -e 's|^BACKUP_S3_REGION=.*|BACKUP_S3_REGION=us-east-1|' \
    -e 's|^BACKUP_S3_BUCKET=.*|BACKUP_S3_BUCKET=backups|' \
    -e 's|^BACKUP_S3_ACCESS_KEY_ID=.*|BACKUP_S3_ACCESS_KEY_ID=roundtripaccess|' \
    -e 's|^BACKUP_S3_SECRET_ACCESS_KEY=.*|BACKUP_S3_SECRET_ACCESS_KEY=roundtripsecretkey1234|' \
    -e "s|^BACKUP_AGE_IDENTITY_FILE=.*|BACKUP_AGE_IDENTITY_FILE=$work/age-identity|" "$env"
done
sed -i 's|^MONGO_PASSWORD=.*|MONGO_PASSWORD=roundtrip-mongo|' "$work/pspad.env"
sed -i 's|^KEYCLOAK_DB_PASSWORD=.*|KEYCLOAK_DB_PASSWORD=roundtrip-keycloak-db|' "$work/keycloak.env"
touch "$work/age-identity"

if [[ $build_images == true ]]; then
  kc build keycloak-backup
  ps build mongo-backup
fi
docker run --rm --entrypoint age-keygen pspad-backup-mongo:local >"$work/age-identity" 2>/dev/null
chmod 644 "$work/age-identity"
recipient=$(grep -o 'age1[0-9a-z]*' "$work/age-identity" | head -1)
sed -i "s|^BACKUP_AGE_RECIPIENT=.*|BACKUP_AGE_RECIPIENT=$recipient|" "$work/pspad.env" "$work/keycloak.env"

step "start both stacks and an S3 server"
kc up -d --no-build
ps up -d --no-build mongo mongo-backup
docker volume create roundtrip-s3 >/dev/null
docker run -d --name roundtrip-s3 --network psplace --user 0 -v roundtrip-s3:/data --entrypoint rclone \
  pspad-backup-mongo:local serve s3 /data --addr :8333 --auth-key roundtripaccess,roundtripsecretkey1234 >/dev/null
docker network connect pspad_pspad roundtrip-s3
docker network connect keycloak_keycloak roundtrip-s3
docker exec roundtrip-s3 mkdir -p /data/backups
wait_for_keycloak

step "seed an account and its data"
token=$(admin_token)
curl -sf -H "Authorization: Bearer $token" -H 'Content-Type: application/json' \
  -d '{"username":"ada","enabled":true}' "$keycloak_url/admin/realms/psplace/users"
user_id=$(curl -sf -H "Authorization: Bearer $token" "$keycloak_url/admin/realms/psplace/users?username=ada" | jq -r '.[0].id')
mongo_eval "db.areas.insertOne({_id: 'home', userId: '$user_id', name: 'Home'}); db.meta.insertOne({_id: 'dataset', value: 'before'})" >/dev/null

step "back up both stacks"
ps run --rm mongo-backup now
kc run --rm keycloak-backup now
remote=$(docker exec roundtrip-s3 find /data/backups -type f | sort)
echo "$remote"
grep -q '/backups/pspad/mongo/mongo-.*\.tar\.age$' <<<"$remote" || die "no Mongo backup on S3"
grep -q '/backups/pspad/keycloak/keycloak-.*\.tar\.age$' <<<"$remote" || die "no Keycloak backup on S3"
while read -r object; do
  [[ $(docker exec roundtrip-s3 head -c 21 "$object") == "age-encryption.org/v1" ]] || die "$object is not age ciphertext"
done <<<"$remote"

step "lose both hosts, local backups included"
docker network disconnect pspad_pspad roundtrip-s3
docker network disconnect keycloak_keycloak roundtrip-s3
ps down -v
kc down -v

step "restore from S3"
kc up -d --no-build
ps up -d --no-build mongo
docker network connect pspad_pspad roundtrip-s3
docker network connect keycloak_keycloak roundtrip-s3
wait_for_keycloak
kc stop keycloak
kc run --rm keycloak-backup restore latest --from s3 --yes
kc start keycloak
ps run --rm mongo-backup restore latest --from s3 --yes
wait_for_keycloak

step "check what came back"
restored_id=$(curl -sf -H "Authorization: Bearer $(admin_token)" "$keycloak_url/admin/realms/psplace/users?username=ada" | jq -r '.[0].id')
[[ $restored_id == "$user_id" ]] || die "account came back as $restored_id, expected $user_id"
[[ $(mongo_eval "db.areas.countDocuments({userId: '$user_id'})") == 1 ]] || die "the account's data did not come back"
[[ $(mongo_eval "db.meta.countDocuments({_id: 'dataset'})") == 0 ]] || die "the dataset id survived the restore"

echo "round trip ok: account $user_id and its data restored from S3"
