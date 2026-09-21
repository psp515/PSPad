#!/usr/bin/env bash
set -euo pipefail

here=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)

COMPOSE_SERVICE="${COMPOSE_SERVICE:-keycloak}"
CONTAINER="${CONTAINER:-}"
KC_SERVER="${KC_SERVER:-http://localhost:8080}"
REALM="${REALM:-psplace}"
FRONTEND_REDIRECT_URI="${FRONTEND_REDIRECT_URI:-https://pspad.psplace.dev/*}"

env_candidates=("$here/.env" "$here/../.env" "$PWD/.env")
[ -n "${ENV_FILE:-}" ] && env_candidates=("$ENV_FILE")

loaded_env_file=""
for candidate in "${env_candidates[@]}"; do
  if [ -f "$candidate" ]; then
    loaded_env_file="$candidate"
    break
  fi
done

strip_decoration() {
  local line=$1
  line=${line%$'\r'}
  line=${line#$'\xEF\xBB\xBF'}
  line=${line#"${line%%[![:space:]]*}"}
  case $line in
    export\ *|export$'\t'*)
      line=${line#export}
      line=${line#"${line%%[![:space:]]*}"}
      ;;
  esac
  printf '%s' "$line"
}

env_file_keys() {
  local line key
  [ -n "$loaded_env_file" ] || return 0
  while IFS= read -r line || [ -n "$line" ]; do
    line=$(strip_decoration "$line")
    case $line in ''|'#'*) continue ;; esac
    key=${line%%=*}
    [ "$key" = "$line" ] && continue
    key=${key%"${key##*[![:space:]]}"}
    printf '%s\n' "$key"
  done < "$loaded_env_file"
}

key_from_env_file() {
  local wanted=$1 line key value
  [ -n "$loaded_env_file" ] || return 0
  while IFS= read -r line || [ -n "$line" ]; do
    line=$(strip_decoration "$line")
    case $line in ''|'#'*) continue ;; esac
    key=${line%%=*}
    [ "$key" = "$line" ] && continue
    key=${key%"${key##*[![:space:]]}"}
    [ "$key" = "$wanted" ] || continue
    value=${line#*=}
    value=${value#"${value%%[![:space:]]*}"}
    case $value in
      \"*\") value=${value#\"}; value=${value%\"} ;;
      \'*\') value=${value#\'}; value=${value%\'} ;;
    esac
    printf '%s' "$value"
    return 0
  done < "$loaded_env_file"
}

first_set() {
  local name value
  for name in "$@"; do
    value="${!name:-}"
    [ -n "$value" ] || value=$(key_from_env_file "$name")
    if [ -n "$value" ]; then
      printf '%s' "$value"
      return 0
    fi
  done
}

if [ -n "$loaded_env_file" ]; then
  echo "environment read from $loaded_env_file"
else
  echo "no env file found; looked at: ${env_candidates[*]}"
fi

KEYCLOAK_ADMIN_USER=$(first_set KEYCLOAK_ADMIN_USER KC_BOOTSTRAP_ADMIN_USERNAME KEYCLOAK_ADMIN KEYCLOAK_ADMIN_USERNAME)
KEYCLOAK_ADMIN_PASSWORD=$(first_set KEYCLOAK_ADMIN_PASSWORD KC_BOOTSTRAP_ADMIN_PASSWORD)

if [ -z "$KEYCLOAK_ADMIN_USER" ] || [ -z "$KEYCLOAK_ADMIN_PASSWORD" ]; then
  echo "admin credentials missing."
  echo "accepted user names:     KEYCLOAK_ADMIN_USER, KC_BOOTSTRAP_ADMIN_USERNAME, KEYCLOAK_ADMIN, KEYCLOAK_ADMIN_USERNAME"
  echo "accepted password names: KEYCLOAK_ADMIN_PASSWORD, KC_BOOTSTRAP_ADMIN_PASSWORD"
  if [ -n "$loaded_env_file" ]; then
    echo "keys found in $loaded_env_file:"
    env_file_keys | sed 's/^/  /'
  fi
  echo "pass them inline, or point ENV_FILE at a file that has them."
  exit 1
fi

if [ -n "$CONTAINER" ]; then
  kc() { docker exec -i "$CONTAINER" /opt/keycloak/bin/kcadm.sh "$@"; }
else
  compose_candidates=()
  for dir in "$here" "$here/.." "$PWD"; do
    for name in compose.yaml compose.yml docker-compose.yaml docker-compose.yml; do
      compose_candidates+=("$dir/$name")
    done
  done
  [ -n "${COMPOSE_FILE:-}" ] && compose_candidates=("$COMPOSE_FILE")

  COMPOSE_FILE=""
  for candidate in "${compose_candidates[@]}"; do
    if [ -f "$candidate" ]; then
      COMPOSE_FILE="$candidate"
      break
    fi
  done

  if [ -z "$COMPOSE_FILE" ]; then
    echo "no compose file found; looked at:"
    printf '  %s\n' "${compose_candidates[@]}"
    echo "set COMPOSE_FILE, or set CONTAINER to exec into a container directly."
    exit 1
  fi

  echo "compose file $COMPOSE_FILE, service $COMPOSE_SERVICE"
  kc() { docker compose -f "$COMPOSE_FILE" exec -T "$COMPOSE_SERVICE" /opt/keycloak/bin/kcadm.sh "$@"; }
fi

client_id_of() {
  kc get clients -r "$REALM" -q "clientId=$1" --fields id --format csv --noquotes | tr -d '\r'
}

kc config credentials \
  --server "$KC_SERVER" --realm master \
  --user "$KEYCLOAK_ADMIN_USER" --password "$KEYCLOAK_ADMIN_PASSWORD"

kc update "realms/$REALM" \
  -s enabled=true \
  -s sslRequired=external \
  -s registrationAllowed=false \
  -s resetPasswordAllowed=false \
  -s 'passwordPolicy=length(12) and notUsername and notEmail' \
  -s bruteForceProtected=true \
  -s permanentLockout=false \
  -s failureFactor=10 \
  -s waitIncrementSeconds=60 \
  -s maxFailureWaitSeconds=900 \
  -s minimumQuickLoginWaitSeconds=60 \
  -s quickLoginCheckMilliSeconds=1000 \
  -s maxDeltaTimeSeconds=43200

backend=$(client_id_of pspad-backend)
[ -n "$backend" ] || { echo "client pspad-backend not found in realm $REALM"; exit 1; }

kc update "clients/$backend" -r "$REALM" \
  -s bearerOnly=true \
  -s publicClient=false \
  -s standardFlowEnabled=false \
  -s implicitFlowEnabled=false \
  -s directAccessGrantsEnabled=false \
  -s serviceAccountsEnabled=false \
  -s fullScopeAllowed=false

frontend=$(client_id_of pspad-frontend)
[ -n "$frontend" ] || { echo "client pspad-frontend not found in realm $REALM"; exit 1; }

echo "current attributes on pspad-frontend (the next step replaces this object):"
kc get "clients/$frontend" -r "$REALM" --fields attributes

kc update "clients/$frontend" -r "$REALM" \
  -s publicClient=true \
  -s standardFlowEnabled=true \
  -s implicitFlowEnabled=false \
  -s directAccessGrantsEnabled=false \
  -s serviceAccountsEnabled=false \
  -s fullScopeAllowed=false \
  -s "redirectUris=[\"$FRONTEND_REDIRECT_URI\"]" \
  -s 'webOrigins=["+"]' \
  -s 'attributes={"pkce.code.challenge.method":"S256"}'

mapper=$(kc get "clients/$frontend/protocol-mappers/models" -r "$REALM" \
  --fields id,name --format csv --noquotes | tr -d '\r' \
  | awk -F, '$2 == "pspad-backend-audience" { print $1 }')

if [ -n "$mapper" ]; then
  kc delete "clients/$frontend/protocol-mappers/models/$mapper" -r "$REALM"
fi

kc create "clients/$frontend/protocol-mappers/models" -r "$REALM" -f - <<'JSON'
{
  "name": "pspad-backend-audience",
  "protocol": "openid-connect",
  "protocolMapper": "oidc-audience-mapper",
  "config": {
    "included.client.audience": "pspad-backend",
    "id.token.claim": "false",
    "access.token.claim": "true"
  }
}
JSON

echo
echo "realm:"
kc get "realms/$REALM" --fields realm,enabled,sslRequired,registrationAllowed,resetPasswordAllowed,passwordPolicy,bruteForceProtected,permanentLockout,failureFactor,waitIncrementSeconds,maxFailureWaitSeconds,minimumQuickLoginWaitSeconds,quickLoginCheckMilliSeconds,maxDeltaTimeSeconds

echo "pspad-backend:"
kc get "clients/$backend" -r "$REALM" --fields clientId,bearerOnly,publicClient,standardFlowEnabled,implicitFlowEnabled,directAccessGrantsEnabled,serviceAccountsEnabled,fullScopeAllowed

echo "pspad-frontend:"
kc get "clients/$frontend" -r "$REALM" --fields clientId,publicClient,standardFlowEnabled,implicitFlowEnabled,directAccessGrantsEnabled,serviceAccountsEnabled,fullScopeAllowed,redirectUris,webOrigins,attributes

echo "pspad-frontend mappers:"
kc get "clients/$frontend/protocol-mappers/models" -r "$REALM" --fields name,protocolMapper,config
