# Handoff — UI polish and burndown (feature/ui-polish)

**Status: merged, not shippable yet.** All four plans are merged into `psp/feature`
(merge commits `519adc1`, `8aca93a`, `7694c8a`, `21defa9`, on top of spec commit
`95b9a57`). The merged tree builds clean (0 warnings, 0 errors) and every existing
test passes: 293 unit + 41 integration, all green, run directly on `psp/feature`
after the merges — see the session transcript, not re-verified by writing this
file. That is the *floor*, not a sign-off: three final whole-branch reviews ran
before merging and each came back **"With fixes"**, and none of those fixes have
been applied yet. Two plans also have code tasks that were never finished.

Read `specs/ui-polish-design.md` first — it is the spec every plan below argues
from. Then read whichever plan's own ledger you're picking up
(`.superpowers/sdd/plan-<n>-<name>/progress.md`) — it has the full history,
every ruling, every fix round, in order. This file is the summary and the map
of what's left; the ledgers are the detail.

Delete this file when the branch is actually ready to merge to `main`.

---

## Do this first: clean up the four worktrees and stale branches

Four sibling worktrees exist beside this one, each already merged and now
redundant:

```
E:/PSPad-wt/identity   (branch psp/feature-identity, merged)
E:/PSPad-wt/burndown   (branch psp/feature-burndown, merged — has uncommitted work, see below)
E:/PSPad-wt/ui         (branch psp/feature-ui, merged)
E:/PSPad-wt/settings   (branch psp/feature-settings, merged)
```

**`E:/PSPad-wt/burndown` has uncommitted, unfinished work** — a killed agent's
partial edit to `AGENTS.md` and `adr/README.md`, plus an untracked
`adr/0019-created-at-on-todo-task.md`. It was mid-way through Task 5 (see
"Plan 2" below) when stopped. Either finish that work in place and commit it
as part of resuming Task 4/5, or discard it (`git checkout -- AGENTS.md
adr/README.md && rm adr/0019-created-at-on-todo-task.md`) and start Task 5
fresh from the brief — your call, but decide before touching the worktree
further, and don't silently lose it.

Once each worktree's work is confirmed no longer needed there, remove the
worktrees (`git worktree remove <path>`) and delete the four
`psp/feature-*` branches. Not urgent, but they'll confuse anyone who runs
`git branch` looking for what's active.

---

## Plan 1 — identity (merged, one fix wave never dispatched)

Ledger: `.superpowers/sdd/plan-1-identity/progress.md`
Final review: "With fixes" — findings below were verified directly against the
merged code before this handoff was written, not just taken from the review.

### Important — fix before shipping

**1. `RenameAsync` can undo a healed display name.**
`src/PSPad.Api/Identity/UserProvisioner.cs:59-64`

```csharp
public async Task<User> RenameAsync(User user, string displayName, CancellationToken ct)
{
    if (string.IsNullOrWhiteSpace(displayName) || user.DisplayName == displayName)
    {
        return user;
    }
    ...
```

`ClaimsCurrentUser.DisplayName` (in `CurrentUser.cs`) falls back to `Subject`
(the raw GUID string) when a token carries none of `name`, `ClaimTypes.Name`,
`preferred_username`. That fallback value is neither blank nor equal to a
previously-healed real name, so `RenameAsync` treats it as authoritative and
overwrites the good name with the GUID — silently re-introducing the exact
defect this plan exists to remove, and writing a bogus `UserDisplayNameSet`
into the user's permanent action history in the process.

Fix: give `ICurrentUser` a way to say "the token named nobody" — a nullable
`Name` alongside `DisplayName`, or a `HasName` bool — and skip the rename
call entirely when it's false. Cheapest version: at the `MeEndpoints.cs` call
site, don't call `RenameAsync` when `current.DisplayName == current.Subject`.

**2. `UserDisplayNameSet` has no History description.**
`src/modules/PSPad.Module.History/HistoryDescriptions.cs` — the `Known`
dictionary has an entry for every other event type, including the sibling
`UserProvisioned` and `UserTimeZoneSet`. `UserDisplayNameSet` (added by this
plan) has none, so it falls through to `Humanise` and renders on the History
screen as **"User display name set"** — a type name in system voice, next to
32 entries written in first-person past tense ("Changed the time zone").

Fix: one line —
`["UserDisplayNameSet"] = "Updated the display name from your account"`
(worded to signal it's automatic, not something the user did).

Consider also: `test/PSPad.Module.History.Tests/` has no test asserting every
`DomainEvent` subtype has a `Known` entry. A reflection-based guard test would
have caught this automatically and will catch the next one. `PSPad.Module.
History.Tests` would need references to the Tasks and Identity modules to see
their event types — `PSPad.TestInfrastructure`'s architecture guards might be
a better home for it. Not required to ship; worth doing.

### Should fix — file hygiene

**3. Two test classes both named `MeEndpointTests`.**
`test/PSPad.Api.Tests/MeEndpointTests.cs` (namespace `PSPad.Api.Tests`, added
by this plan) duplicates `test/PSPad.Api.Tests/Identity/MeEndpointTests.cs`
(namespace `PSPad.Api.Tests.Identity`, pre-existing). One test
(`ItReturnsTheTokenNameRatherThanTheSubject` vs
`ItReportsTheNameClaimAsTheDisplayName`) is the same assertion twice. Fold the
three new tests into the existing `Identity/MeEndpointTests.cs`, delete the
root-level duplicate. Behaviour is unaffected — this is purely so
`-class "*MeEndpointTests*"` doesn't match two unrelated files.

### Still owed

**Task 5 — the manual Keycloak sign-in check** was never run (needed Docker +
a real realm with a named user, and the session never got a clean window for
it). When you run it, per the plan: confirm the sidebar shows a real name and
email after sign-in, and confirm the heal path (change the Keycloak user's
name, sign out, sign in, confirm the sidebar picks it up). **Also add**: clear
the user's first/last name in Keycloak entirely and sign in again, to check
finding 1 above against a real token, not just reasoning about it.

---

## Plan 2 — burndown chart (INCOMPLETE — 3 of 5 tasks landed)

Ledger: `.superpowers/sdd/plan-2-burndown/progress.md` — **read the entry
near the top marked "CONTROLLER ERROR"**: this plan was incorrectly reported
as done earlier in the session. It is not. `BurndownRule` (Tasks 1-2, merged)
and the Mongo backfill (Task 3, merged, one fix round already applied) exist
and are fully tested, but **nothing calls `BurndownRule` in production** —
`grep -rn "BurndownRule" src/` matches only its own declaration.

### Task 4 — the chart itself (not started)

Brief: `.superpowers/sdd/plan-2-burndown/task-4-brief.md`

Rewrite `src/PSPad.App/Pages/HistoryPage.razor` to call
`BurndownRule.Build(tasks, State.Today, days, TimeZoneInfo.FindSystemTimeZoneById(State.TimeZone))`
and render the result with `MudChart` (already in MudBlazor 9.9.0, no new
package), 30/90-day toggle, above the existing event log. `_loaded` gate +
skeleton, per the same pattern every other screen in this branch already
uses. Full detail and the exact test list is in the brief.

### Task 5 — ADR-0019 and docs (not started, partially drafted uncommitted)

Brief: `.superpowers/sdd/plan-2-burndown/task-5-brief.md`

`E:/PSPad-wt/burndown` has an uncommitted, unfinished start on this (see the
worktree-cleanup note above). The brief requires the ADR to describe three
things the plan text couldn't have anticipated — make sure whoever finishes
this includes them, they're easy to drop:

1. The backfill does not bump `seq` (see the Important finding below) — state
   as a known, accepted consequence.
2. Deleting a task today retroactively changes what the chart shows for past
   days, because state is truth (AD-2), not an immutable event replay.
3. Recurring task templates are permanently excluded from the open-count
   line — restate this as a deliberate decision with its reasoning (they
   never close, so counting them as open would add a flat, ever-growing
   offset), not as an afterthought.

Also: AGENTS.md §8 currently ends with a stale sentence pointing at
"plan 10 — shell and plan 11 — screens", long since merged. Task 5 is
supposed to correct it to point at `specs/ui-polish-design.md` instead.

### Important — from the final whole-branch review, verify before Task 4 ships

**The backfill doesn't bump `seq`, so an already-synced replica never
receives the recovered `CreatedAt`.**
`src/shared/PSPad.Infrastructure/Mongo/MongoBackfill.cs:30-33` writes with a
raw `UpdateOneAsync` touching only `createdAt`. Delta sync
(`SyncReader.cs:64-65`) selects by `seq > since`. A client that has already
synced past a task's `seq` will not see the backfilled value until some
unrelated command touches that task again. Since D8 puts the whole burndown
computation on the client, this makes the backfill largely ineffective for
any *existing* installation — it only helps a replica built fresh (new
device, or after ADR-0018's purge-on-user-switch).

This was confirmed against the code, not just claimed by the reviewer.
Decide deliberately: either make the backfill bump `seq` from the `counters`
document as part of its update (so delta sync picks it up), or accept the
current behaviour explicitly and say so in the ADR — the degradation is
graceful (`BurndownRule.IsOpenOn` treats a null `CreatedAt` as "already
existed for the whole window", so the chart is merely flat-shifted, not
wrong-shaped) but nothing currently records that this is intentional.

### Minor, your call whether to fix

- `(aggregateId, type)` has no Mongo index, so `MongoBackfill`'s per-task
  event lookup is a full collection scan of `events`, run synchronously at
  every host boot before `app.Run()`. Fine at this app's single-user scale;
  a two-line addition to `MongoIndexes.EnsureAsync` (which runs immediately
  before) removes the only startup-blocking scan in the app, if you want it.
- `MongoBackfill.cs` loads the whole missing-document set with `ToListAsync`
  rather than a cursor — same scale argument, same cheap fix if you want it.
- `MongoBackfill` hardcodes `"todotasks"`, `"createdAt"`, `"TaskCreated"` as
  string literals from `PSPad.Infrastructure`, which is forbidden to
  reference a module (AD-4/AGENTS.md §6) and so has no compile-time link to
  `TodoTask` or `nameof(TaskCreated)`. This is a plan-text issue, not an
  implementation one — the plan named this exact file path. `PSPad.Api` is
  where a module meets MongoDB per AGENTS.md §6; worth moving there if you're
  touching this file again for another reason. Not worth it on its own.
- No test pins a task created and completed on the same day, and no test
  proves `CompletedDays` survives a real client sync round-trip (the field is
  private, backed by `IncludeFields = true` on the server and NOT included on
  the client's deserializer — `TodayRule` already depends on this working, so
  it almost certainly does, but Task 4 is the first thing that puts it on
  screen). One assertion when Task 4 is written would close this.

---

## Plan 3 — loading and grid (merged, one fix wave killed mid-run)

Ledger: `.superpowers/sdd/plan-3-loading-and-grid/progress.md`
Final review: "With fixes" — three Important findings, all confirmed directly
against the merged code (not just the review's claim) before writing this.
A fix-wave agent was dispatched and got partway through before being
stopped; **nothing from that attempt was committed**, so all three are still
open on `psp/feature` as merged.

### Important — fix before shipping

**1. `GoalsPage`'s achieved-goals panel lost its spacing and never gained the
grid.**
`src/PSPad.App/Pages/GoalsPage.razor` — the `_active` loop (around line 34)
is wrapped in `<div class="pspad-grid">`; the `_achieved` loop (around line
51, inside the `MudExpansionPanel`) is not. An earlier task in this plan
removed `mb-3` from `GoalCard` globally (it was defeating the grid's gap
elsewhere) — but only the active loop got the grid to replace it with, so the
achieved cards now have neither margin nor gap. They render flush against
each other.

Fix: wrap the `_achieved` loop the same way the `_active` loop already is.

**2. The grid's gap-over-background hairline technique paints every empty
cell as a solid block.**
`src/PSPad.App/wwwroot/css/app.css`, the `.pspad-grid` rule:

```css
.pspad-grid {
    display: grid;
    gap: 1px;
    background: var(--mud-palette-lines-default);
    grid-template-columns: repeat(auto-fill, minmax(340px, 1fr));
}
.pspad-grid > * { background: var(--mud-palette-surface); }
```

The container's background paints the whole box; children paint over their
own cells only. With `auto-fill`, any item count that isn't an exact multiple
of the column count leaves at least one empty cell — which then shows as a
solid `--mud-palette-lines-default` rectangle where a card would be. This is
the common case, not an edge case: three list cards at two columns, four
goals at three columns, and so on.

Fix (confirmed by direct inspection, not yet applied):

```css
.pspad-grid {
    display: grid;
    gap: 1px;
    background: var(--mud-palette-surface);
    grid-template-columns: repeat(auto-fill, minmax(340px, 1fr));
}
.pspad-grid > * {
    background: var(--mud-palette-surface);
    box-shadow: 0 0 0 1px var(--mud-palette-lines-default);
}
```

This draws a 1px ring around every real card instead of relying on empty
cells showing the container through the gaps. **Check for a doubled-border
look** where `ListCard`/`GoalCard` also carry `Outlined="true"` on their own
`MudPaper` — if the two lines are visibly doubled, that's a separate visual
call (remove `Outlined="true"` from those components, or don't), not
something to silently fix as part of this change.

**3. `minmax(340px, 1fr)` overflows narrow viewports.**
Same `.pspad-grid` rule. `minmax`'s first argument is a hard minimum — a
single-column layout still holds its track at 340px even when the container
is narrower, causing horizontal overflow. `MudContainer`'s phone-width
content box (after side padding) can be as narrow as ~326-366px depending on
exact device width — a very common Android width (360px) already overflows
by roughly 14px.

Fix:
`grid-template-columns: repeat(auto-fill, minmax(min(340px, 100%), 1fr));`

**Both 2 and 3 are defects in what `specs/ui-polish-design.md`'s decision D6
describes, not in an implementation that departed from it.** Fix the CSS,
then update D6's snippet in the spec to match — AGENTS.md §11 makes the spec
authoritative, and shipping code that deliberately differs from what the spec
says, without updating the spec, is exactly the drift that rule exists to
prevent.

### Minor — cheap, worth doing in the same pass

- `ListPage.razor`'s loading branch has no title skeleton (every other
  gridded screen shows a title-shaped placeholder while loading; `ListPage`
  shows nothing, so its header space isn't reserved and jumps when data
  lands). Copy whatever title-skeleton markup `AreaBoard.razor` already uses.
- No `aria-busy`/`role="status"` on any loading region. `RowSkeleton.razor`,
  `CardSkeleton.razor`, and `NavSidebar`'s areas-loading branch could each
  use `role="status" aria-busy="true"` on their root, matching the pattern
  the boot mark and `BrandLoader` already use with `aria-label`.
- Five tests assert `Assert.NotNull(page.Find(".pspad-grid"))` — bUnit's
  `Find` throws before `Assert.NotNull` can ever see a null, so the assertion
  is a no-op (the test still works, via the throw, but reads as if the
  `Assert` is doing something). Change to a bare `page.Find(...)` call or
  `Assert.NotEmpty(page.FindAll(...))`.

### Deliberately not fixed, parked with reasoning already recorded

`_loaded` never resets on a route-parameter change (`AreaBoard`/`ListPage`
reload from `OnParametersSetAsync`, and Blazor reuses the component instance
across `/lists/A` → `/lists/B`), so navigating between two lists briefly
shows the old one's content under the new URL before the reload resolves.
Pre-existing behaviour, not a regression from this plan — the maintainer's
call whether it's worth a one-line fix now that `_loaded` exists.

### Task 6 — the manual browser check — still owed, and needs widening

Brief: `.superpowers/sdd/plan-3-loading-and-grid/task-6-brief.md`. Its step 3
currently checks column counts at "roughly 400px" — which is exactly wide
enough to miss finding 3 above. **Before running it, widen step 3 to include
a check at ~360px, and a screen with a deliberately non-multiple item count
(e.g. 3 cards where the layout fits 2 columns)** — those are the specific
conditions that exposed findings 2 and 3, and nothing automated can catch
either one.

---

## Plan 4 — settings and navigation (4 of 6 tasks landed, merged)

Ledger: `.superpowers/sdd/plan-4-settings-and-nav/progress.md`. No final
whole-branch review has been run yet for this plan — run one once Tasks 5-6
land, the same way the other three plans got theirs
(`superpowers:requesting-code-review`'s `code-reviewer.md` template, most
capable available model, package the diff with
`scripts/review-package PLAN_FILE 95b9a57 <head>`).

Tasks 1-4 are done and each passed its own task review clean: the
`PUT /api/me/timezone` endpoint, the client method, the settings screen
(time zone picker, theme, sync status, all reading from `AppState` per the
plan's own binding design decision), and the account menu rebuild (shows
name + email, links to Settings/History/Sign out).

### Task 5 — History joins the sidebar (not started)

Brief: `.superpowers/sdd/plan-4-settings-and-nav/task-5-brief.md`

Add a `History` `MudNavLink` to `NavSidebar.razor`, after Goals. One test.
Small.

**While you're in this file: three test fixtures nearby still carry a real
personal email address, not the codebase's synthetic convention.** Confirmed
just now, after the merge:

```
test/PSPad.App.Tests/Layout/NavSidebarTests.cs:102,119,161,171 — "kolberu@gmail.com"
test/PSPad.App.Tests/State/AvatarColorTests.cs:46 — "kolberu@gmail.com"
```

(A third file, `AccountMenuTests.cs`, had the same issue and was already
cleaned by Plan 4's Task 4 rebuild — these two are what's left.) Replace both
with the established convention (`"Ada Lovelace"` / `"ada@example.com"`,
used everywhere else in this branch's tests). Pure literal substitution, run
the suite after.

**This is a small piece of a larger, already-surfaced question that is the
maintainer's call, not something to decide unilaterally:** the same real
email address is already committed to `origin/main` (commit `0a2704d`,
merged PR #11), predating this entire feature. Cleaning the two files above
stops the mistake from spreading further on this branch; it does not remove
the exposure already on `main`. That needs the maintainer to decide whether
to rewrite shared history (destructive to every existing clone) and whether
to do anything on GitHub's side if the repo is public. Already raised with
the maintainer once during this session — resolve it before merging to
`main`, not as part of this branch's own work.

### Task 6 — ADR-0020 and docs (not started)

Brief: `.superpowers/sdd/plan-4-settings-and-nav/task-6-brief.md`

Records: History becomes a sidebar row, theme/sync move into Settings
(supersedes part of `ui-redesign-2-design.md`'s D12/D13, amends the sidebar
row set from ADR-0014/0017). Update `docs/src/pages/features.astro` and the
landing page; `install.astro` is untouched (no compose/env/port change).
Build the docs site (`npm --prefix docs run build`) to confirm it still
builds — this is the same check Task 5 of Plan 2 owes, so if both are being
done around the same time, one person could reasonably do both docs updates
together.

---

## Before merging `psp/feature` to `main`

In order:

1. Finish Plan 2 (Tasks 4-5) and Plan 4 (Tasks 5-6).
2. Apply the fix waves above for Plans 1 and 3 (nothing from either has been
   applied yet — both are exactly as described in this file).
3. Decide and apply the Plan 2 `seq`-bump question.
4. Run Plan 1's Task 5 and Plan 3's Task 6 manual checks against the full
   merged branch with the compose stack up (`docker compose -f
   docker/compose.yaml up -d`) — checking all four plans' visual and
   behavioural claims together, not one at a time, since they now share a
   shell.
5. Run one final whole-branch review over the *entire* branch
   (`main`..`psp/feature`, not per-plan) before opening the PR — the four
   per-plan final reviews each looked at one plan in isolation; nothing has
   yet reviewed how all four sit together as one deliverable.
6. Resolve the real-email-in-history question with the maintainer before
   pushing, if a decision on that hasn't already been made.
7. Clean up the four worktrees and stale branches (see top of this file).
8. Delete this file.

Every build and test claim in this document was run directly against the
merged `psp/feature`, not copied from an agent's report: `dotnet build`
(0 warnings, 0 errors), 293 unit tests across all four unit-test projects,
41 integration tests — all green, immediately after the four merges, before
anything below "Do this first" was touched.
