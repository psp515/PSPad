# Plan 15 — Make the test signal real

**Goal.** CI must fail when tests fail, and must fail when no tests run. Today
it does neither: `dotnet test --filter Category=Unit` discovers zero tests in
this repo, exits 5, and every CI run since the workflow landed has been green on
an empty suite.

**Why first.** Every other track's "green" claim is worth nothing until this
lands, which is why the runbook merges this one first.

**Evidence.** Locally, `dotnet test` finds nothing; the xUnit v3 in-process
runner only runs through `dotnet run --project <test project> -- -trait ...`.
`.github/workflows/ci.yml:22` and `:36` both carry the broken form, and
`AGENTS.md` §7 documents it as the way to run tests. `ci.yml:36` also passes
`--project`, which `dotnet test` does not accept.

**Architecture.** xUnit v3 test projects are executables. They take runner
arguments after `--`. There is no VSTest adapter in play, so `dotnet test`'s
discovery finds nothing and reports success.

**Constraints.** Owns `.github/workflows/ci.yml`, `AGENTS.md` §7, and
`scripts/`. Touches no `src/` and no `test/` file — so it cannot conflict with
plans 12, 13, 14 or 16.

---

## Task 1 — Write down what the runner actually does

Before changing CI, capture the current behaviour so the fix is verifiable and
the claim in this plan is not taken on trust.

**Files**
- create `scripts/run-tests.sh`
- create `scripts/run-tests.ps1`

- [ ] Reproduce the failure and keep the output:

```bash
dotnet test --filter Category=Unit; echo "exit=$?"
```

Expect a report of zero tests and `exit=5`. If this repo's behaviour has changed,
stop and rewrite this plan rather than proceeding on a stale premise.

- [ ] `scripts/run-tests.sh`:

```bash
#!/bin/sh
set -e

UNIT="PSPad.Module.Tasks.Tests PSPad.Module.History.Tests PSPad.Module.Identity.Tests PSPad.App.Tests"

case "${1:-unit}" in
  unit)
    for project in $UNIT; do
      echo "== $project"
      dotnet run --project "test/$project" -- -trait "Category=Unit"
    done
    ;;
  integration)
    dotnet run --project test/PSPad.Api.Tests -- -trait "Category=Integration"
    ;;
  *)
    echo "usage: run-tests.sh [unit|integration]" >&2
    exit 2
    ;;
esac
```

- [ ] `scripts/run-tests.ps1`:

```powershell
param([ValidateSet("unit", "integration")] [string] $Suite = "unit")

$ErrorActionPreference = "Stop"

if ($Suite -eq "integration") {
    dotnet run --project test/PSPad.Api.Tests -- -trait "Category=Integration"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    exit 0
}

foreach ($project in @(
    "PSPad.Module.Tasks.Tests",
    "PSPad.Module.History.Tests",
    "PSPad.Module.Identity.Tests",
    "PSPad.App.Tests")) {
    Write-Host "== $project"
    dotnet run --project "test/$project" -- -trait "Category=Unit"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
```

- [ ] Verify both, on the host:

```bash
sh scripts/run-tests.sh unit
```

Expect the four projects to report 128, 81, 6 and 6 passing at branch point.

- [ ] The `.gitattributes` added on 2026-09-13 pins `*.sh` to LF, so the shell
      script survives a Windows checkout. Confirm before committing:

```bash
git check-attr eol -- scripts/run-tests.sh
```

Expect `eol: lf`.

- [ ] Commit: `chore: scripts that actually run the test suites`

---

## Task 2 — A zero-test run must fail

A runner that finds nothing is the failure mode this plan exists for, so the
guard belongs in CI, not in a comment.

**Files**
- modify `scripts/run-tests.sh`

- [ ] Capture the runner's output and fail when it reports no tests. Replace the
      `dotnet run` line in the `unit` branch with:

```bash
      output=$(dotnet run --project "test/$project" -- -trait "Category=Unit")
      status=$?
      echo "$output"
      if [ $status -ne 0 ]; then exit $status; fi
      case "$output" in
        *"0 tests"*|*"No test"*) echo "$project ran no tests" >&2; exit 1 ;;
      esac
```

- [ ] Prove the guard fires. Run one project with a trait that matches nothing:

```bash
dotnet run --project test/PSPad.App.Tests -- -trait "Category=Nonexistent"
```

Note the exact wording of the "no tests" line in its output and make the `case`
pattern match that wording rather than a guess. Quote the line in the commit
body.

- [ ] Commit: `chore: fail the suite when a project runs no tests`

---

## Task 3 — CI uses the scripts

**Files**
- modify `.github/workflows/ci.yml`

- [ ] Replace the two test steps:

```yaml
      - run: sh scripts/run-tests.sh unit
```

and

```yaml
      - run: sh scripts/run-tests.sh integration
```

- [ ] Add the feature branches to the triggers, so a long-lived branch is
      verified before it merges rather than after:

```yaml
on:
  push:
    branches: [main, "feature/**"]
  pull_request:
```

- [ ] Commit: `ci: run the suites through the runner that works`

---

## Task 4 — AGENTS.md stops teaching the broken command

**Files**
- modify `AGENTS.md`

- [ ] In §7 "Testing", replace the three-line `dotnet test` block with:

```bash
sh scripts/run-tests.sh unit
sh scripts/run-tests.sh integration
```

- [ ] Add one line under it stating why, since this is exactly the kind of
      counter-intuitive constraint §11 allows a comment for:

> `dotnet test` does not work here — the xUnit v3 projects are executables with
> no VSTest adapter, so discovery finds nothing and the command exits reporting
> success.

- [ ] Commit: `docs: AGENTS.md §7 documents the runner that runs tests`

---

## Task 5 — Smoke-test the images CI builds

The commented-out `api-image` job at the foot of `ci.yml` was never enabled. The
2026-09-13 container run found three defects that no unit test could see — the
app served as `application/octet-stream`, a CRLF entrypoint that exited 127, and
a wrong Mongo env key in `compose.prod.yaml`. A smoke test over the built images
catches that class.

**Files**
- modify `.github/workflows/ci.yml`

- [ ] Uncomment the `api-image` job. Its env var is already
      `Mongo__ConnectionString`, which is correct.

- [ ] Add an `app-image` job that asserts the content type, because that is the
      bug that shipped:

```yaml
  app-image:
    runs-on: ubuntu-latest
    needs: build-and-unit-test
    steps:
      - uses: actions/checkout@v4

      - run: docker build -f src/PSPad.App/Dockerfile -t pspad-app:ci .

      - name: Serve the app and check what it serves
        run: |
          docker run -d --name pspad-app-ci -p 5001:8080 \
            -e API_BASE_ADDRESS=http://localhost:5000 \
            -e APP_BASE_ADDRESS=http://localhost:5001 \
            -e KEYCLOAK_AUTHORITY=http://localhost:8080/realms/pspad \
            -e KEYCLOAK_CLIENT_ID=pspad-app \
            pspad-app:ci
          for i in $(seq 1 20); do
            if curl -sf localhost:5001/ >/dev/null; then break; fi
            sleep 1
          done
          type=$(curl -s -o /dev/null -w '%{content_type}' localhost:5001/)
          echo "index content-type: $type"
          case "$type" in text/html*) ;; *) echo "the app is not served as HTML"; docker logs pspad-app-ci; exit 1 ;; esac
          curl -sf localhost:5001/appsettings.json | grep -q pspad-app

      - if: always()
        run: docker rm -f pspad-app-ci || true
```

- [ ] Commit: `ci: smoke-test the api and app images`

---

## Done when

- `sh scripts/run-tests.sh unit` reports 128 + 81 + 6 + 6 and fails loudly if a
  project runs nothing.
- CI runs both suites through the scripts, on `main` and on `feature/**`.
- AGENTS.md §7 no longer documents a command that reports success on zero tests.
- CI builds both images and fails if the app is not served as HTML.

## Deliberately not here

Fixing the 128-test suite itself, or adding coverage. This plan changes only how
tests are invoked and reported. A green run after it lands is the first
trustworthy green this repo has had.
