validate_stack() {
  [[ -n ${BACKUP_MONGO_URI:-} ]] || fail "BACKUP_MONGO_URI is not set"
  [[ -n ${BACKUP_MONGO_DATABASE:-} ]] || fail "BACKUP_MONGO_DATABASE is not set"
}

dump() {
  mongodump --uri "$BACKUP_MONGO_URI" --oplog --gzip --out="$1/mongo" --quiet
  # Restoring admin would swap the users the restore itself is signed in as, mid-restore.
  rm -rf "$1/mongo/admin" "$1/mongo/config"
}

confirmation_word() { echo "$BACKUP_MONGO_DATABASE"; }

restore_data() {
  # --oplogReplay rejects namespace filters, so the database is emptied first: collections created
  # after the backup would otherwise survive the restore.
  mongosh "$BACKUP_MONGO_URI" --quiet --eval \
    "db.getSiblingDB(process.env.BACKUP_MONGO_DATABASE).dropDatabase()" >/dev/null
  mongorestore --uri "$BACKUP_MONGO_URI" --oplogReplay --drop --gzip --dir="$1/mongo" \
    >"$1/mongorestore.log" 2>&1 || {
    grep -iE "fail|error" "$1/mongorestore.log" | tail -5 >&2
    fail "mongorestore failed"
  }
  grep -o "[0-9]* document(s) restored successfully" "$1/mongorestore.log" | sed "s/^/backup: /" >&2 || true
  mongosh "$BACKUP_MONGO_URI" --quiet --eval \
    "db.getSiblingDB(process.env.BACKUP_MONGO_DATABASE).meta.deleteOne({_id: 'dataset'})" >/dev/null
}
