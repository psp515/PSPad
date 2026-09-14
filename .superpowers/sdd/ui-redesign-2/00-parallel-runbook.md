# Parallel runbook — plans 12 to 16

How five plans run at once without stepping on each other. Read before starting
any of them.

## Why these five

Plan 11 left My Day, the Inbox and the `⋯` menus unwritten. Bringing the compose
stack up on 2026-09-13 exposed three more: the app was served entirely as
`application/octet-stream` (fixed), CI has been green on zero tests, and
`/api/me` returns a GUID where a display name belongs. `+ New area` in the
sidebar was also found to be wired to nothing — D6 of the design is unbuilt.

Each plan below owns a disjoint set of files. That is what makes them parallel;
it is not an accident and it is the thing to preserve when editing them.

| Plan | Track | Owns | Depends on |
|---|---|---|---|
| [12](12-my-day.md) | My Day screen | `Pages/Today.razor`, `Pages/TodayTests.cs` | nothing |
| [13](13-inbox.md) | Inbox screen | `Pages/InboxPage.razor`, `Pages/InboxPageTests.cs` | nothing |
| [14](14-thing-menus.md) | `⋯` menus + New area | `Components/ThingMenu.razor`, `Components/ConfirmDialog.razor`, `Layout/NavSidebar.razor`, `Layout/AppShell.razor`, `Components/ListCard.razor`, `Pages/AreaBoard.razor`, `Pages/ListPage.razor`, their tests | nothing |
| [15](15-test-runner-and-ci.md) | CI truth | `.github/workflows/ci.yml`, `AGENTS.md` §7, `scripts/` | nothing |
| [16](16-me-display-name.md) | API identity | `src/PSPad.Api/Identity/`, `src/PSPad.Api/Endpoints/MeEndpoints.cs`, `test/PSPad.Api.Tests/` | nothing |

## File ownership — the hard rules

1. **No plan edits a file another plan owns.** If a track finds it needs one,
   it stops and the conflict is resolved by the maintainer, not by editing.
2. **`wwwroot/css/app.css` belongs to plan 14 alone.** Plans 12 and 13 style
   with MudBlazor utility classes only. A shared stylesheet is the one file
   three screen tracks would otherwise all want.
3. **`Components/TaskRow.razor` is read-only in every track.** It is the D10
   contract. A track that believes it needs a change to `TaskRow` stops.
4. **No track extracts a shared helper.** Plans 12, 13 and 14 each carry their
   own toggle/star handlers, duplicated from `ListPage.razor`. The duplication
   is deliberate for the duration of the parallel run; collapsing it is a
   follow-up after all three merge, when the real shape is visible.
5. **Nobody touches `PSPad.Module.*`.** AD-3 and AD-4; these are presentation
   plans. Plan 16 touches `PSPad.Api` only.

## Worktrees

**2026-09-13, later:** `feature/ui` merged to `main` (squash, PR #11,
`0a2704d`) and deleted. `origin/main` == local `main` == `0a2704d`, docker/nginx
fixes and `.gitattributes` confirmed present in it. Base branch for every track
below is now **`main`**, not `feature/ui`. `worktree.baseRef` default `fresh`
(branches from `origin/main`) is correct as-is — no manual worktree needed for
that reason anymore. Still create them by hand for naming control:

```bash
git worktree add .claude/worktrees/my-day    -b feature/my-day       main
git worktree add .claude/worktrees/inbox     -b feature/inbox        main
git worktree add .claude/worktrees/menus     -b feature/thing-menus  main
git worktree add .claude/worktrees/ci        -b chore/ci-test-runner main
git worktree add .claude/worktrees/me-name   -b fix/me-display-name  main
```

Tear down when a branch is merged:

```bash
git worktree remove .claude/worktrees/<name>
```

## Driving the tracks

One subagent per plan, each pinned to its worktree, each told to work the plan
task by task. The review gate from the plan-10/11 run stays: after a track
reports done, dispatch a task-scoped review of `feature/ui..<branch>` before
merging it. That gate found real defects repeatedly — including tests that could
not fail — and parallelism is not a reason to drop it.

A track's brief is the plan file plus this runbook's rules 1 to 5. Nothing else
from this conversation travels; the worktree is a cold start.

**These plan files do not exist inside a worktree.** `.superpowers/` is
gitignored (`.gitignore:432`), so a fresh worktree's tree does not contain it.
Give each agent the absolute path into the main working tree —
`E:\PSPad\.superpowers\sdd\ui-redesign-2\<plan>.md` — or copy the one plan into
its worktree before starting. This is the same trap that cost the plan-10/11
ledgers when the work moved machines.

## Merge order

Merge back into `feature/ui` in this order, running the full unit suite after
each:

1. **15 (CI)** — first, so every later merge is verified by something real.
2. **16 (API)** — different project, cannot conflict.
3. **12 (My Day)** and **13 (Inbox)** — disjoint single files.
4. **14 (menus)** — last. It is the only track touching `AppShell`,
   `NavSidebar`, `ListCard`, `AreaBoard` and `ListPage`, so merging it last
   means it rebases over settled screens rather than the reverse.

## Verification, every track

```bash
dotnet run --project test/PSPad.App.Tests -- -trait "Category=Unit"
```

`dotnet test` discovers zero tests in this repo and exits 5. Until plan 15
lands, do not trust any command of that shape, including the one in AGENTS.md §7.

Whole unit suite before any merge:

```bash
for p in PSPad.Module.Tasks.Tests PSPad.Module.History.Tests PSPad.Module.Identity.Tests PSPad.App.Tests; do
  dotnet run --project test/$p -- -trait "Category=Unit" || exit 1
done
```

Baseline at branch point: 128 passing in `PSPad.App.Tests`; 81 + 6 + 6 in the
three module projects.

## Still not parallel

The hand browser check (ADR-0014, above and below 960px) runs once, on
`feature/ui`, after all five merge. It cannot be split and no test replaces it:
bUnit applies no stylesheet and both navigation branches are always in the DOM.
The compose stack now builds and serves the app, so it is finally possible.
