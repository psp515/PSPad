# SDD ledger — plan: .superpowers/sdd/ui-redesign-2/16-me-display-name.md

Tasks 1–2 done as one TDD pass: added `X-Test-Name` / `X-Test-Preferred-Username`
to `TestAuthenticationHandler`, wrote `MeEndpointTests.cs` (confirmed the first
two facts fail with the subject GUID in the message, the third already passes),
then added `ICurrentUser.DisplayName` and wired it into `MeEndpoints.cs`. The
plan's own test snippets were missing `TestContext.Current.CancellationToken` on
the async calls — this project's xUnit analyzer config treats that as a build
error (xUnit1051) — fixed to match `AuthenticationTests.cs`'s existing pattern.

33/33 `PSPad.Api.Tests` integration facts green (Testcontainers Mongo), 240/240
unit facts across the other four projects, full solution build clean.

## Task 3 — verified against the real demo realm

Rebuilt and restarted the `api` container from `docker/compose.yaml` (stack
already running from earlier session work). Enabled direct access grants on
the `pspad-app` client via the Keycloak admin API (not committed to
`realm-pspad.json`; reverted back to `false` after verification, confirmed by
re-reading the client back), fetched a password-grant token for `demo`, and
called `/api/me`.

First call reused a `demo` user already provisioned in Mongo from earlier
session work (plan 12/13 screenshot verification) — `EnsureAsync` returned
early as documented, `displayName` stayed the old stored GUID. Not a defect:
deleted that one `users` document (`_id: 3840152c-30aa-9926-64b4-6c33f1a72aa0`)
to force a genuine fresh provisioning and re-called:

```
{"userId":"3840152c-30aa-9926-64b4-6c33f1a72aa0","displayName":"Demo User","timeZone":"Europe/Warsaw"}
```

Called again immediately after — same `displayName`, confirming stability
post-provisioning as `AuthenticationTests.MeProvisionsOnTheFirstCallAndIsStableAfterwards`
already covers for `UserId`.

No repo commit for Task 3 — it changed no files, per the plan's own "docs only,
or no commit if nothing changed" instruction. This ledger is the record.

Plan 16 COMPLETE. One commit:
`fix(api): /api/me reports the name from the token, not the subject twice`.
