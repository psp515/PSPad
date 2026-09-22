# The `psplace` realm

`realm-psplace.json` is imported once, on Keycloak's very first start
(`--import-realm`, `docker/compose.yaml`). Once the `keycloak-data` volume
exists the file is ignored — Keycloak's own database is authoritative from
then on, and the file is a bootstrap record, not a source of truth.

One realm holds every self-hosted application. Accounts, sessions and the
signing keys are realm-wide, so signing in to one application signs you in to
all of them. Each application contributes clients, never its own realm.

## Client naming

Two clients per application, `<app>-frontend` and `<app>-backend`:

| Client | Type | Purpose |
|--------|------|---------|
| `<app>-frontend` | public, PKCE `S256` | the browser signs in as this client |
| `<app>-backend` | bearer-only | the API validates tokens against this audience |

`<app>-frontend` carries an `oidc-audience-mapper` that adds `<app>-backend`
to the access token's `aud` claim. Without it the API rejects every token.

Give an application's frontend the audience of its own backend only. Sharing
an audience would let a token minted for one application's browser be spent
against another application's API.

## Why user identity survives these changes

PSPad derives its user id from the token's `sub` claim
(`src/PSPad.Api/Identity/CurrentUser.cs`), which is the Keycloak user's UUID.
That UUID belongs to the user record, not to the realm name or to any client.
Renaming the realm or its clients, and adding new clients, all leave `sub`
untouched, so existing PSPad data stays reachable.

What does destroy it: deleting the `keycloak-data` volume, or creating the
accounts again in a second realm. Both mint new UUIDs, and every task, area
and inbox written by the old account is orphaned in MongoDB.

**Never run `docker compose down -v` against a Keycloak that holds real
accounts.** `down` alone is safe; `-v` removes the volume and the accounts
with it.

## What the realm hardens, and what it does not

Beyond the clients, `realm-psplace.json` sets:

| Setting | Value | Why |
|---------|-------|-----|
| `bruteForceProtected` + `failureFactor` etc. | on, 10 failures | Keycloak's default is off. An internet-reachable realm without it accepts unlimited password guesses. |
| `passwordPolicy` | `length(12) and notUsername and notEmail` | the default policy is none — a one-character password is accepted. |
| `sslRequired` | `external` | Keycloak's own default, pinned so it cannot drift. |
| `registrationAllowed` | `false` | accounts are created by the operator, not by visitors. |
| `fullScopeAllowed` (both clients) | `false` | the default `true` puts *every* realm role into the token, including roles belonging to other applications sharing this realm. |
| `directAccessGrantsEnabled`, `implicitFlowEnabled`, `serviceAccountsEnabled` | `false` | only the authorization code flow with PKCE is wanted. Direct access grants make the client collect the user's password, bypassing PKCE. |

Deliberately not set, and still needed before a public deployment:

- **No SMTP server**, so `resetPasswordAllowed` stays `false`. A forgotten
  password is reset by the operator in the admin console.
- **No second factor.** No OTP required action, no MFA flow.
- **Redirect URIs are `localhost`.** See the last section.

Both are configured below. They are kept out of `realm-psplace.json` because
JSON has no comment syntax and Keycloak's importer rejects a file that tries
one — it refuses to start rather than skipping the line. A second `.json`
beside it is no better: `docker/keycloak` is mounted as the whole import
directory, so every `.json` in it is imported as its own realm.

## SMTP

Needed for self-serve password reset and for email verification. Without a
mail host neither can be switched on: Keycloak would show the buttons and
then fail to send.

Add to `realm-psplace.json`, as a sibling of `"clients"`, and flip the three
flags that depend on it:

```json
  "resetPasswordAllowed": true,
  "verifyEmail": true,
  "loginWithEmailAllowed": true,
  "duplicateEmailsAllowed": false,
  "smtpServer": {
    "host": "smtp.example.com",
    "port": "587",
    "from": "pspad@example.com",
    "fromDisplayName": "PSPad",
    "replyTo": "",
    "envelopeFrom": "",
    "ssl": "false",
    "starttls": "true",
    "auth": "true",
    "user": "pspad@example.com",
    "password": "REPLACE_ME"
  },
```

`ssl` is implicit TLS on port 465; `starttls` is upgrade-on-port-587. Set one,
not both.

**The password is stored in clear text in whatever holds it.** Do not commit a
real one to this file. On a running installation set the mail host through
`kcadm.sh` instead, so the secret never reaches git:

```bash
cd docker
KC="docker compose -f compose.yaml exec -T keycloak /opt/keycloak/bin/kcadm.sh"

$KC config credentials --server http://localhost:8080 --realm master   --user "$KEYCLOAK_ADMIN_USER" --password "$KEYCLOAK_ADMIN_PASSWORD"

$KC update realms/psplace   -s 'smtpServer.host=smtp.example.com'   -s 'smtpServer.port=587'   -s 'smtpServer.from=pspad@example.com'   -s 'smtpServer.fromDisplayName=PSPad'   -s 'smtpServer.starttls=true'   -s 'smtpServer.auth=true'   -s 'smtpServer.user=pspad@example.com'   -s "smtpServer.password=$SMTP_PASSWORD"   -s resetPasswordAllowed=true   -s verifyEmail=true
```

Verify from **Realm settings** → **Email** → **Test connection**, which sends
to the admin account's own address. Links in those emails are built from
`KC_HOSTNAME`, so a wrong `KEYCLOAK_HOSTNAME` produces mail nobody can act on.

## MFA (TOTP)

The cheap route: make OTP enrolment a default required action. Every user who
signs in without one is sent through enrolment before reaching the
application. No custom authentication flow, nothing to maintain across
Keycloak upgrades.

Add to `realm-psplace.json`, as a sibling of `"clients"`:

```json
  "otpPolicyType": "totp",
  "otpPolicyAlgorithm": "HmacSHA1",
  "otpPolicyDigits": 6,
  "otpPolicyPeriod": 30,
  "otpPolicyLookAheadWindow": 1,
  "otpPolicyCodeReusable": false,
  "requiredActions": [
    {
      "alias": "CONFIGURE_TOTP",
      "name": "Configure OTP",
      "providerId": "CONFIGURE_TOTP",
      "enabled": true,
      "defaultAction": true,
      "priority": 10
    }
  ],
```

`HmacSHA1` is Keycloak's default and what every authenticator app supports.
`HmacSHA256` is stronger on paper and rejected by a meaningful share of apps —
change it only after testing the one your users actually run.

`defaultAction: true` applies to users created **after** the change. Existing
accounts keep signing in with a password alone until the action is added to
each of them:

```bash
$KC update realms/psplace/users/<user-id> -s 'requiredActions=["CONFIGURE_TOTP"]'
```

On an existing realm, without touching the file:

```bash
$KC update authentication/required-actions/CONFIGURE_TOTP -r psplace   -s enabled=true -s defaultAction=true
```

### Why not a conditional OTP flow

Keycloak's other route is a copy of the browser flow with a **Conditional
OTP** subflow, which prompts only when the user has an OTP credential. It is
what you want for *optional* MFA — users opt in, nobody is locked out. It is a
custom flow, so it is yours to re-check on every Keycloak upgrade, and it
makes MFA voluntary, which for a handful of self-hosted accounts is usually
the wrong default. The required action above makes it mandatory and costs
nothing to maintain.

### Recovery

An authenticator lost with no second admin account locks the realm out
permanently. Before making OTP mandatory, keep one break-glass admin in the
`master` realm without a required action, and store its password somewhere
that is not the phone running the authenticator.

## Adding another application to the realm

Do this in the admin console (`http://localhost:8080`, realm **psplace**), not
by editing this file — the file is no longer read after the first start.

1. **Clients** → **Create client**. Client ID `<app>-backend`, client
   authentication **Off**, all authentication flows **Off**. That is the
   bearer-only shape; it never runs a browser flow.
2. **Clients** → **Create client**. Client ID `<app>-frontend`, client
   authentication **Off** (public), **Standard flow** on, all other flows off
   — **Direct access grants** in particular, which the console turns on by
   default and which lets the client collect the user's password directly.
3. On `<app>-frontend` set **Valid redirect URIs** to the application's public
   URL plus `/*`, and **Web origins** to `+`. `+` means "the origins of the
   redirect URIs", so it needs no second edit when the URL changes.
4. On `<app>-frontend`, **Advanced** → **Proof Key for Code Exchange Code
   Challenge Method** → `S256`.
5. On `<app>-frontend`, **Client scopes** → `<app>-frontend-dedicated` →
   **Add mapper** → **By configuration** → **Audience**. Name it
   `<app>-backend-audience`, **Included Client Audience** `<app>-backend`,
   **Add to ID token** off, **Add to access token** on.
6. On both new clients, **Client scopes** → **Full scope allowed** → off.
   Left on, the token carries every realm role, including other
   applications'.
7. Point the new application at the realm: authority
   `<keycloak public url>/realms/psplace`, its client id, and its API's
   audience `<app>-backend`.

No existing client changes, and no user is touched.

## Applying file changes to a realm that already exists

A restart does not pick them up. `--import-realm` imports a realm only when
that realm is absent; once `psplace` is in Keycloak's database the file is
read and skipped. Editing `realm-psplace.json` on a running installation
changes nothing.

Two ways to apply it.

### Surgical, with `apply-realm.sh` — preferred

`apply-realm.sh` beside this file brings a running realm up to what
`realm-psplace.json` describes, through the admin API. It touches only the
realm settings and the two PSPad clients: accounts, sessions, roles and any
client another application added are left alone.

```bash
FRONTEND_REDIRECT_URI='https://pspad.example.com/*' ./docker/keycloak/apply-realm.sh
```

It prints the resulting realm, clients and mappers so the run can be checked,
and is idempotent — running it twice changes nothing the second time.

Everything it needs is an environment variable with a default, so it also
drives a deployment whose files are laid out differently from this
repository:

| Variable | Default | What it is |
|----------|---------|------------|
| `COMPOSE_FILE` | first of `compose.yaml`, `compose.yml`, `docker-compose.yaml`, `docker-compose.yml` found beside the script, one directory up, or in the working directory | the compose file holding Keycloak. Set it only when the search misses. |
| `COMPOSE_SERVICE` | `keycloak` | the service name inside that file. |
| `CONTAINER` | unset | a container name to `docker exec` into directly. Set it and the compose variables are ignored — for a Keycloak started without compose. |
| `ENV_FILE` | first `.env` found beside the script, one directory up, or in the working directory | the file the admin credentials are read from. It is parsed for those keys only, never sourced, so a BOM, CRLF endings, `export` prefixes and quoted values all work. |
| `KEYCLOAK_ADMIN_USER`, `KEYCLOAK_ADMIN_PASSWORD` | from `ENV_FILE` | required. `KC_BOOTSTRAP_ADMIN_USERNAME` / `KC_BOOTSTRAP_ADMIN_PASSWORD` and `KEYCLOAK_ADMIN` are accepted under those names too. Pass them inline to keep them out of a file. |
| `KC_SERVER` | `http://localhost:8080` | Keycloak's own address **as seen from inside its container**, not the public URL. Only change it if Keycloak listens on another port. |
| `REALM` | `psplace` | the realm to update. |
| `FRONTEND_REDIRECT_URI` | `https://pspad.psplace.dev/*` | what `pspad-frontend`'s redirect URI becomes. |

For a stack in `docker-compose.yml` with the credentials passed inline:

```bash
COMPOSE_FILE=./docker-compose.yml KEYCLOAK_ADMIN_USER=admin KEYCLOAK_ADMIN_PASSWORD='...' FRONTEND_REDIRECT_URI='https://pspad.example.com/*'   ./docker/keycloak/apply-realm.sh
```

Credentials passed this way land in the shell history. `ENV_FILE` pointing at
a file outside the repository avoids that.

Three things it does that are worth knowing:

- **It replaces the frontend client's `attributes` object wholesale**, because
  `kcadm.sh`'s `-s key=value` cannot address a key that itself contains dots
  (`pkce.code.challenge.method`). The script prints the current attributes
  before overwriting them. PSPad's client carries only that one attribute; a
  client that has grown others needs them added to the same `-s` argument.
- **It deletes and recreates the audience mapper** rather than updating it,
  for the same dotted-key reason. The mapper has no state to lose.
- **The password policy applies to passwords set from then on.** Existing
  passwords shorter than 12 characters keep working until they are changed.

`redirectUris` is the one value that is not in the file for a real
deployment. Pass it through `FRONTEND_REDIRECT_URI`, and keep it equal to
`APP_BASE_ADDRESS` in `docker/.env` plus `/*` — the API's CORS origin is
built from the same variable, so the two cannot be allowed to drift.

### Wholesale, with `kc.sh import` — destroys accounts

`--override true` does not merge. It removes the existing realm and recreates
it from the file. The file holds no users, so **every account in the realm is
deleted**, and recreated accounts get new UUIDs — new `sub`, so every task,
area and inbox the old account wrote is orphaned in MongoDB, exactly as if
the volume had been dropped.

Also lost: clients added in the console for other applications, redirect URIs
edited there, roles, mappers, and the realm's signing keys (so every session
ends).

`--override false` is harmless and does nothing to an existing realm.

Prefer `kcadm.sh` above. If a wholesale import is genuinely wanted, export
first — that export, not the volume alone, is what makes the accounts
recoverable.

The server must be stopped: `start-dev` holds an exclusive lock on its
database, so `docker compose exec ... kc.sh import` against a running
container fails.

```bash
docker compose -f docker/compose.yaml stop keycloak

docker compose -f docker/compose.yaml run --rm --no-deps   --entrypoint /opt/keycloak/bin/kc.sh keycloak   export --dir /opt/keycloak/data/export --realm psplace --users realm_file

docker run --rm -v pspad_keycloak-data:/data -v "$PWD:/backup" alpine   tar czf /backup/keycloak-data-$(date +%F).tar.gz -C /data .

docker compose -f docker/compose.yaml run --rm --no-deps   --entrypoint /opt/keycloak/bin/kc.sh keycloak   import --file /opt/keycloak/data/import/realm-psplace.json --override true

docker compose -f docker/compose.yaml start keycloak
```

Check the log for `Realm 'psplace' imported` before going further.

To put the accounts back, import the export instead of the file. It carries
the users and their UUIDs, so PSPad data becomes reachable again — but it
also restores the old configuration, which then needs the `kcadm.sh` block
above:

```bash
docker compose -f docker/compose.yaml stop keycloak
docker compose -f docker/compose.yaml run --rm --no-deps   --entrypoint /opt/keycloak/bin/kc.sh keycloak   import --dir /opt/keycloak/data/export --override true
docker compose -f docker/compose.yaml start keycloak
```

## Renaming an existing `pspad` realm to `psplace`

For an installation that already ran the old realm. Keeps every account.

1. Back up first: `docker compose -f docker/compose.yaml stop keycloak`, then
   copy the `pspad_keycloak-data` volume.
2. Start it again. In the admin console, realm **pspad** → **Realm settings**
   → **General** → change **Realm ID** to `psplace` → **Save**. The admin
   console URL changes with it; user UUIDs do not.
3. **Clients** → rename `pspad-app` to `pspad-frontend` and `pspad-api` to
   `pspad-backend` (the **Client ID** field on each client's settings page).
4. Delete the `pspad-client` client if it is still there. It was never used
   and it is a public client with an open redirect.
5. On `pspad-frontend`, open the audience mapper and set **Included Client
   Audience** to `pspad-backend`.
6. Update `docker/.env`: `KEYCLOAK_AUTHORITY`, `KEYCLOAK_INTERNAL_AUTHORITY`,
   `KEYCLOAK_AUDIENCE`, `KEYCLOAK_CLIENT_ID`.
7. `docker compose -f docker/compose.yaml up -d --force-recreate api app`.

Sessions issued before the rename carry the old `iss` and stop validating.
Users sign in again once. Their data is unaffected.

## Redirect URIs are literal

Keycloak 26 does not substitute `${env.NAME}` during realm import — it
validates the raw string as a URI and refuses to start. The URLs in
`realm-psplace.json` are therefore hard-coded to `localhost`. A deployment on
real hostnames edits them in the admin console after the first start; see
`docs/src/pages/install.astro`.
