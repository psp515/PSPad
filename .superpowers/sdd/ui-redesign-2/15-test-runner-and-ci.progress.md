# SDD ledger — plan: .superpowers/sdd/ui-redesign-2/15-test-runner-and-ci.md

## Pre-flight: Task 1's own stop condition fired

Task 1 says: "If this repo's behaviour has changed, stop and rewrite this plan
rather than proceeding on a stale premise." Reproducing the premise before
touching anything, it has changed:

- `dotnet test --filter Category=Unit` → 240 tests, all pass, exit 0.
- `dotnet test --filter Category=Nonexistent` (matches nothing anywhere) →
  correctly exits 8, not 0. (First pass at checking this piped through `tail`,
  which reports `tail`'s exit code, not `dotnet test`'s — redid it without a
  pipe to get the real code.)
- `dotnet test --project test/PSPad.Api.Tests --filter Category=Integration`
  (the exact form the plan calls not-accepted) → 30 tests, pass, exit 0.

Cause: `global.json` has carried `"test": { "runner": "Microsoft.Testing.Platform" }`
since the very first commit (`babcf02`, the same day this plan was written) —
it makes `dotnet test` natively drive the xUnit v3 / Microsoft.Testing.Platform
executables correctly, `--project` included, with a correct nonzero exit on a
zero-match filter. AGENTS.md §7 already documents exactly this working form.

Surfaced to the maintainer; decision was Task 5 only. Tasks 1–4 (the
`run-tests.sh`/`.ps1` workaround scripts, routing CI through them, rewriting
AGENTS.md §7) are not done — they would fix a failure mode that does not
reproduce here. Not marking plan 15 fully done; recording precisely what was
skipped and why so a future re-read of the plan doesn't re-trigger the same
work blind.

## Task 5 — image smoke tests

The plan's own drafted `api-image` job (commented out, never run before) does
not pass as written: it omits `ASPNETCORE_ENVIRONMENT=Development`, and
`Program.cs` sets `options.RequireHttpsMetadata = !builder.Environment.IsDevelopment()`
— without that env var the API defaults to Production, `RequireHttpsMetadata`
becomes true, and every request (including `/health`, which passes through
`UseAuthentication()` even though the endpoint itself needs no auth) 500s with
"The MetadataAddress or Authority must use HTTPS". Verified by running the
built image locally against a plain (non-replica-set) `mongo:8` exactly as the
job does: 500 without the env var, 200 with it added. Added it to the job.

The `app-image` job was verified as drafted (no changes needed): built the
image, ran it standalone, confirmed `text/html` content-type on `/` and
`pspad-app` present in `/appsettings.json`.

`api-image` and `app-image` added/enabled in `.github/workflows/ci.yml`, both
`needs: build-and-unit-test`. Both verified locally end-to-end before committing.

## Not done, worth a follow-up

Task 3 also proposed adding `feature/**` to the push trigger (`on.push.branches`)
so a long-lived branch is verified before merge, not after — independent of the
stale dotnet-test premise, still a reasonable idea. Left out here to stay
strictly inside the "Task 5 only" scope the maintainer chose; flagged instead
of silently included.

Plan 15: Task 5 complete, Tasks 1–4 deliberately not done (premise did not
reproduce). Commit: `ci: smoke-test the api and app images`.
