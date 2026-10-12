readonly BACKUP_DIR=/backups
readonly STARTED_STAMP=/tmp/.backup-started
readonly SUCCESS_STAMP=$BACKUP_DIR/.last-success
readonly IDENTITY_FILE=/run/secrets/age-identity
readonly HEALTHY_WINDOW_SECONDS=$((26 * 3600))

log() { echo "backup: $*" >&2; }

fail() {
  log "$*"
  exit 1
}

remotes() {
  [[ -n ${BACKUP_S3_BUCKET:-} ]] && echo s3
  [[ -n ${BACKUP_GDRIVE_TOKEN:-} ]] && echo gdrive
  return 0
}

remote_path() {
  case $1 in
    s3) echo "s3:${BACKUP_S3_BUCKET}/${BACKUP_S3_PREFIX:+${BACKUP_S3_PREFIX%/}/}${BACKUP_STACK}" ;;
    gdrive) echo "gdrive:${BACKUP_STACK}" ;;
  esac
}

validate() {
  local at=${BACKUP_AT:-03:17}
  [[ $at =~ ^([01][0-9]|2[0-3]):[0-5][0-9]$ ]] || fail "BACKUP_AT must be HH:mm between 00:00 and 23:59, got '$at'"
  [[ ${BACKUP_KEEP_DAYS:-14} =~ ^[1-9][0-9]*$ ]] || fail "BACKUP_KEEP_DAYS must be a whole number of days, got '${BACKUP_KEEP_DAYS}'"

  validate_stack
  configure_rclone
}

require_recipient() {
  local recipient=${BACKUP_AGE_RECIPIENT:-}
  if [[ -n $(remotes) && -z $recipient ]]; then
    fail "BACKUP_AGE_RECIPIENT is required once S3 or Google Drive is set; off-host copies are never uploaded in plaintext"
  fi
  if [[ -n $recipient ]] && ! age -r "$recipient" -o /dev/null </dev/null 2>/dev/null; then
    fail "BACKUP_AGE_RECIPIENT is not an age recipient (age1…)"
  fi
}

configure_rclone() {
  : >"$RCLONE_CONFIG"
  if [[ -n ${BACKUP_S3_BUCKET:-} ]]; then
    export RCLONE_CONFIG_S3_TYPE=s3
    export RCLONE_CONFIG_S3_PROVIDER=${BACKUP_S3_ENDPOINT:+Other}
    export RCLONE_CONFIG_S3_PROVIDER=${RCLONE_CONFIG_S3_PROVIDER:-AWS}
    export RCLONE_CONFIG_S3_ENDPOINT=${BACKUP_S3_ENDPOINT:-}
    export RCLONE_CONFIG_S3_REGION=${BACKUP_S3_REGION:-}
    export RCLONE_CONFIG_S3_ACCESS_KEY_ID=${BACKUP_S3_ACCESS_KEY_ID:-}
    export RCLONE_CONFIG_S3_SECRET_ACCESS_KEY=${BACKUP_S3_SECRET_ACCESS_KEY:-}
    export RCLONE_CONFIG_S3_NO_CHECK_BUCKET=true
  fi
  if [[ -n ${BACKUP_GDRIVE_TOKEN:-} ]]; then
    export RCLONE_CONFIG_GDRIVE_TYPE=drive
    export RCLONE_CONFIG_GDRIVE_SCOPE=drive.file
    export RCLONE_CONFIG_GDRIVE_TOKEN=$BACKUP_GDRIVE_TOKEN
    export RCLONE_CONFIG_GDRIVE_ROOT_FOLDER_ID=${BACKUP_GDRIVE_FOLDER_ID:-}
    export RCLONE_CONFIG_GDRIVE_CLIENT_ID=${BACKUP_GDRIVE_CLIENT_ID:-}
    export RCLONE_CONFIG_GDRIVE_CLIENT_SECRET=${BACKUP_GDRIVE_CLIENT_SECRET:-}
  fi
}

render_crontab() {
  local at=${BACKUP_AT:-03:17}
  local hour=$((10#${at%%:*})) minute=$((10#${at##*:}))
  echo "$minute $hour * * * backup now" | tee /tmp/crontab
}

start_clock() { touch "$STARTED_STAMP"; }

schedule() {
  render_crontab >/dev/null
  log "next runs at ${BACKUP_AT:-03:17} ${BACKUP_TZ:-UTC}, destinations: local $(remotes | tr '\n' ' ')"
  TZ=${BACKUP_TZ:-UTC} exec supercronic -quiet /tmp/crontab
}

seconds_since() { echo $(($(date +%s) - $(stat -c %Y "$1"))); }

healthcheck() {
  if [[ -f $SUCCESS_STAMP ]] && (($(seconds_since "$SUCCESS_STAMP") < HEALTHY_WINDOW_SECONDS)); then
    return 0
  fi
  if [[ ! -f $SUCCESS_STAMP && -f $STARTED_STAMP ]] && (($(seconds_since "$STARTED_STAMP") < HEALTHY_WINDOW_SECONDS)); then
    return 0
  fi
  fail "no successful backup in the last 26 hours"
}

ping() {
  [[ -n ${BACKUP_PING_URL:-} ]] || return 0
  curl -fsS -m 10 --retry 3 -o /dev/null "${BACKUP_PING_URL%/}$1" || log "ping to BACKUP_PING_URL failed"
}

write_manifest() {
  local dir=$1 name=$2 entries="" file
  (cd "$dir" && find . -type f -printf '%P\n' | sort | xargs -d '\n' sha256sum -- >"$dir.sums") && mv "$dir.sums" "$dir/SHA256SUMS"
  while read -r sum file; do
    entries+="${entries:+,}{\"name\":\"$file\",\"bytes\":$(stat -c %s "$dir/$file"),\"sha256\":\"$sum\"}"
  done <"$dir/SHA256SUMS"
  printf '{"stack":"%s","name":"%s","createdAt":"%s","files":[%s]}\n' \
    "$BACKUP_STACK" "$name" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$entries" >"$dir/manifest.json"
}

prune() {
  local keep=${BACKUP_KEEP_DAYS:-14} remote
  find "$BACKUP_DIR" -maxdepth 1 -type f -name "${BACKUP_STACK}-*.tar" -mmin +$((keep * 1440)) -print -delete \
    | sed "s/^/backup: pruned /" >&2
  [[ ${BACKUP_REMOTE_PRUNE:-true} == false ]] && return 0
  for remote in $(remotes); do
    rclone delete "$(remote_path "$remote")" --min-age "${keep}d" --include "${BACKUP_STACK}-*.tar.age" \
      || fail "pruning $remote failed"
  done
}

finish_run() {
  local status=$?
  [[ -n ${WORK_DIR:-} ]] && rm -rf "$WORK_DIR"
  if ((status != 0)); then
    log "run failed"
    ping /fail
  fi
  exit "$status"
}

run_backup() {
  exec 9>/tmp/.backup.lock
  flock -n 9 || fail "another backup run is still in progress"

  local name remote upload_failed=0
  name="${BACKUP_STACK}-$(date -u +%Y-%m-%dT%H%M%SZ)"
  WORK_DIR=$(mktemp -d "$BACKUP_DIR/.work.XXXXXX")
  trap finish_run EXIT

  mkdir "$WORK_DIR/content"
  log "dumping $BACKUP_STACK"
  dump "$WORK_DIR/content"
  write_manifest "$WORK_DIR/content" "$name"
  tar -C "$WORK_DIR/content" -cf "$WORK_DIR/$name.tar" .
  mv "$WORK_DIR/$name.tar" "$BACKUP_DIR/$name.tar"
  log "kept $BACKUP_DIR/$name.tar ($(stat -c %s "$BACKUP_DIR/$name.tar") bytes)"

  if [[ -n $(remotes) ]]; then
    age -r "$BACKUP_AGE_RECIPIENT" -o "$WORK_DIR/$name.tar.age" "$BACKUP_DIR/$name.tar"
    for remote in $(remotes); do
      if rclone copyto "$WORK_DIR/$name.tar.age" "$(remote_path "$remote")/$name.tar.age"; then
        log "uploaded to $remote"
      else
        log "upload to $remote failed"
        upload_failed=1
      fi
    done
    ((upload_failed == 0)) || fail "at least one upload failed; nothing was pruned"
  fi

  prune
  touch "$SUCCESS_STAMP"
  ping ""
  log "done: $name"
}

list_backups() {
  local remote
  echo "local:"
  find "$BACKUP_DIR" -maxdepth 1 -type f -name "${BACKUP_STACK}-*.tar" -printf '  %f\n' | sort
  for remote in $(remotes); do
    echo "$remote:"
    rclone lsf "$(remote_path "$remote")" --include "${BACKUP_STACK}-*.tar.age" | sort | sed 's/^/  /'
  done
}

refuse_while_service_listens() {
  local target=${BACKUP_SERVICE_PROBE:-}
  [[ -n $target ]] || return 0
  local host=${target%:*} port=${target##*:}
  if timeout 3 bash -c "exec 3<>/dev/tcp/$host/$port" 2>/dev/null; then
    fail "$target is still accepting connections; stop it first: docker compose stop $host"
  fi
}

fetch_backup() {
  local name=$1 from=$2 into=$3 file
  if [[ $from == local ]]; then
    if [[ $name == latest ]]; then
      file=$(find "$BACKUP_DIR" -maxdepth 1 -type f -name "${BACKUP_STACK}-*.tar" -printf '%f\n' | sort | tail -1)
      [[ -n $file ]] || fail "no local backups in $BACKUP_DIR"
    else
      file=${name%.age}
      file=${file%.tar}.tar
    fi
    [[ -f $BACKUP_DIR/$file ]] || fail "no such local backup: $file"
    cp "$BACKUP_DIR/$file" "$into/backup.tar"
    echo "$file"
    return
  fi

  remotes | grep -qx "$from" || fail "remote '$from' is not configured (local, s3 or gdrive)"
  [[ -s $IDENTITY_FILE ]] || fail "restoring from $from needs the age private key: set BACKUP_AGE_IDENTITY_FILE"
  if [[ $name == latest ]]; then
    file=$(rclone lsf "$(remote_path "$from")" --include "${BACKUP_STACK}-*.tar.age" | sort | tail -1)
    [[ -n $file ]] || fail "no backups on $from"
  else
    file=${name%.age}
    file=${file%.tar}.tar.age
  fi
  rclone copyto "$(remote_path "$from")/$file" "$into/backup.tar.age"
  age -d -i "$IDENTITY_FILE" -o "$into/backup.tar" "$into/backup.tar.age"
  rm -f "$into/backup.tar.age"
  echo "$file"
}

restore_backup() {
  local name=latest from=local confirmed=false file reply
  while (($#)); do
    case $1 in
      --from) from=${2:?--from needs local, s3 or gdrive}; shift 2 ;;
      --yes) confirmed=true; shift ;;
      -*) fail "unknown option $1" ;;
      *) name=$1; shift ;;
    esac
  done

  refuse_while_service_listens

  WORK_DIR=$(mktemp -d /tmp/restore.XXXXXX)
  trap 'rm -rf "$WORK_DIR"' EXIT
  file=$(fetch_backup "$name" "$from" "$WORK_DIR")
  mkdir "$WORK_DIR/content"
  tar -C "$WORK_DIR/content" -xf "$WORK_DIR/backup.tar"
  (cd "$WORK_DIR/content" && sha256sum --quiet -c SHA256SUMS) || fail "$file failed its checksum; not restoring it"
  log "verified $file"

  if [[ $confirmed != true ]]; then
    [[ -t 0 ]] || fail "restore overwrites $(confirmation_word); pass --yes or run it with a terminal"
    read -r -p "This replaces $(confirmation_word) with $file. Type '$(confirmation_word)' to continue: " reply
    [[ $reply == "$(confirmation_word)" ]] || fail "not confirmed; nothing changed"
  fi

  restore_data "$WORK_DIR/content"
  log "restored $file"
}
