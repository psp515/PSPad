validate_stack() {
  [[ -n ${BACKUP_PG_URI:-} ]] || fail "BACKUP_PG_URI is not set"
}

require_postgres() {
  PGCONNECT_TIMEOUT=10 psql "$BACKUP_PG_URI" -Atqc 'select 1' >/dev/null 2>&1 \
    || fail "BACKUP_PG_URI is not a reachable Postgres; keycloak-backup only supports Keycloak on Postgres (KC_DB=postgres)"
}

dump() {
  require_postgres
  pg_dump --format=custom --file="$1/keycloak.dump" "$BACKUP_PG_URI"
}

confirmation_word() { echo keycloak; }

restore_data() {
  require_postgres
  pg_restore --clean --if-exists --single-transaction --dbname="$BACKUP_PG_URI" "$1/keycloak.dump"
}
