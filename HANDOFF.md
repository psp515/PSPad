# Handoff — parallel plan run, ui-redesign-2

Written to continue on another machine. Delete when all 5 tracks merge.

## State

`feature/ui` merged to `main` (squash, PR #11, `0a2704d`) on 2026-09-13.
All 5 tracks below branch from `main`, not the old `feature/ui`.

Plans live in `.superpowers/sdd/ui-redesign-2/` — normally gitignored
(`.gitignore`), force-added to `main` this once so they travel. Progress
ledgers for started tracks are alongside each plan as `<plan>.progress.md`.

| # | Track | Branch | Status |
|---|---|---|---|
| 12 | My Day | `feature/my-day` | **complete**, pushed. Final review clean, 135/135 unit tests, 2 rulings parked (see `12-my-day.progress.md`). Ready to merge. |
| 13 | Inbox | `feature/inbox` | started, no task dispatched yet. Ledger created, pre-flight scan not yet run. |
| 14 | Thing menus + New area | not started | no worktree yet |
| 15 | Test runner / CI | not started | no worktree yet |
| 16 | `/api/me` display name | not started | no worktree yet |

## Resume

```bash
git fetch origin
git worktree add .claude/worktrees/my-day feature/my-day   # already done, just merge or review
git worktree add .claude/worktrees/inbox  feature/inbox
git worktree add .claude/worktrees/menus  -b feature/thing-menus  main
git worktree add .claude/worktrees/ci     -b chore/ci-test-runner main
git worktree add .claude/worktrees/me-name -b fix/me-display-name main
```

Then, per worktree: `mkdir -p .superpowers/sdd/ui-redesign-2 && cp` the relevant
plan(s) from `main`'s `.superpowers/sdd/ui-redesign-2/` in — they are already
tracked, so a fresh clone/worktree has them without extra copying once you're
on a commit that includes this handoff. Read
`.superpowers/sdd/ui-redesign-2/00-parallel-runbook.md` first — file ownership,
merge order, and the two worktree traps it documents.

Use `superpowers:subagent-driven-development` per track: `sdd-workspace`
resolves `.superpowers/sdd/<plan-basename>/`, check for a `progress.md`
ledger before dispatching — `12-my-day.progress.md` shows a track fully
walked through the process if you want the shape of it.

## Merge order (runbook's, unchanged)

15 (CI) → 16 (API) → 12/13 → 14 last.

`feature/my-day` is done and can merge now, or wait for 15 to land first per
that order — maintainer's call.

## Test runner reminder

`dotnet test` discovers zero tests here, exits 5, reads as pass. Use:

```bash
dotnet run --project test/PSPad.App.Tests -- -trait "Category=Unit"
```

Plan 15 exists specifically to fix this at the CI level; until it merges,
trust nothing that used `dotnet test`.
