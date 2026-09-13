# Handoff — UI redesign, plans 10 and 11

Written mid-run so the work can continue on another machine. Delete this file
when the branch merges; it describes a moment, not the project.

**Branch:** `feature/ui`, 31 commits ahead of `main` (merge base `60b0f60`).
**Head at handoff:** `1adbd4d`.
**Working tree:** clean.

---

## 1. Read these first

- `specs/ui-redesign-2-design.md` — the design this branch implements. D1…D14.
  The binding contract.
- `adr/0014-one-navigation-tree-at-every-width.md` — supersedes ADR-0013.
- `AGENTS.md` — house rules. §7 on testing is **wrong**; see section 3 below.

---

## 2. What is done

**Plan 10 — shell: complete.** Nine tasks, each reviewed, plus a whole-branch
review and a fix wave.

Sage palette in light and dark; `AvatarColor`; `/areas/{areaId}`;
`ReplicaSearch` + `/search?q=`; `AccountMenu` carrying Goals, History, Theme,
sync count and Sign out; `SidebarCounts`; the rewritten `NavSidebar`;
`AppShell` replacing `MainLayout`; the split FAB deleted with `FabContext` and
`FabAction`. `BottomNav`, `AreaSheet`, `ThemeToggle` and the flat `/areas` page
are gone.

**Plan 11 — screens: tasks 1 through 7 implemented, 1 through 6 reviewed.**

| # | Task | State |
|---|------|-------|
| 1 | `AppTestHost` shared test arrangement | complete |
| 2 | `CardCollapseState` | complete |
| 3 | `TaskRow` | complete |
| 4 | `ListCard` | complete |
| 5 | Area screen of cards + `NameDialog` | complete |
| 5b | *(inserted)* unblock command dispatch in bUnit | complete |
| 6 | `TaskDetailPanel` + `TaskQuery` | complete |
| 7 | List screen | **committed `046e518`, NOT yet reviewed** |
| 8 | My Day | not started |
| 9 | Inbox | not started |
| 10 | `⋯` menus for areas and lists | not started |

**Resume point: dispatch the task-scoped review of task 7**, then continue to
task 8.

Unit suite at head: **128 passing** in `PSPad.App.Tests`; 81 + 6 + 6 in the
three module test projects.

---

## 3. The one thing that will bite you

**`dotnet test` does not work in this repo.** It discovers zero tests and exits
5 — which reads as a pass to anything that only checks the exit code by eye.
The xUnit v3 in-process runner only runs through:

```bash
dotnet run --project test/PSPad.App.Tests -- -trait "Category=Unit"
```

Whole unit suite:

```bash
for p in PSPad.Module.Tasks.Tests PSPad.Module.History.Tests PSPad.Module.Identity.Tests PSPad.App.Tests; do
  dotnet run --project test/$p -- -trait "Category=Unit" || exit 1
done
```

**`AGENTS.md` §7 and `.github/workflows/ci.yml` both still carry the broken
form.** That means CI has been reporting green on zero tests. Neither was
changed here — it is outside this branch's scope and is the maintainer's call.
**This is the most important single item in this document.**

---

## 4. Local-only artifacts that do not travel

`.superpowers/` is in `.gitignore`, so **none of this reaches another machine
through git**:

- `.superpowers/sdd/ui-redesign-2/10-shell.md` and `11-screens.md` — the plans.
- `.superpowers/sdd/10-shell/` and `.superpowers/sdd/11-screens/` — ledgers
  (`progress.md`), per-task briefs, implementer reports, review diffs.

To continue elsewhere, **copy `.superpowers/sdd/` across by hand**, or
regenerate briefs from the plan files. The ledgers hold every ruling made
during the run; sections 5 and 6 below summarise what matters, but the ledgers
are the record.

The commits in git are the real state. If the workspace is lost, `git log` and
the spec are enough to continue.

---

## 5. Decisions taken during the run

Recorded so they are not silently re-litigated. Full versions with costs are in
the two `progress.md` ledgers.

- **Avatar colour** uses an explicit FNV-1a over the user id's bytes, not
  `Guid.GetHashCode()`, which .NET does not guarantee stable across runtime
  versions. A golden test pins one known value.
- **Search** reads the IndexedDB replica only — no endpoint, no Mongo index —
  and excludes orphans: a task whose list was deleted, a list whose area was
  deleted, and a task whose list's area was deleted. Deletion does not cascade
  in this domain, so these are reachable through sync.
- **No `Microsoft.AspNetCore.WebUtilities`.** It was added, then removed:
  Blazor's `[SupplyParameterFromQuery]` covers the page, and the layout's
  `?task=` read needs no URL decoding for a GUID.
- **Two drawers, not one.** `DrawerVariant.Responsive` never opens itself above
  the breakpoint, so the desktop rendered no navigation at all and no hamburger
  to summon it. It also resolves through a JS viewport service that ADR-0014
  rejects. `AppShell` now renders a persistent drawer classed
  `d-none d-md-flex` and a temporary one classed `d-md-none`, both holding the
  same `NavSidebar`. **Do not collapse these back into one.**
- **`NavSidebar` implements `INavigationEventReceiver`.** `MudNavLink` swallows
  `OnClick` entirely when `Href` is set — it renders a plain div, not an anchor
  — so the `Navigated` callback was dead code. This is MudBlazor's own
  interface for the purpose; `MudDrawer` uses it too. **Do not re-add `OnClick`
  to a nav link that has an `Href`.**
- **The account menu's email** comes from the OIDC `email` claim, falling back
  to `Identity.Name`. `MeResponse` carries no email; the repo's demo Keycloak
  user has none either.
- **`CommandSender.Sent`** fires only when the result is accepted. `AppShell`
  is its only subscriber and unsubscribes in `Dispose`.
- **bUnit scope validation is off in the test host.** Command handlers are
  registered scoped, and bUnit resolves from the root provider, so no component
  test could dispatch a command at all. `AppTestHost` sets
  `Services.Options.ValidateScopes = false`. Production DI is untouched. A WASM
  app builds one root container and never creates a child scope, so the
  validation was inert for this architecture.
- **The task detail panel shows no created date.** The design listed one; no
  aggregate carries a creation timestamp, and the only source is the
  server-side event log over HTTP, which fails offline. `specs/` D5 is amended
  with the reasoning.
- **MudBlazor questions go to mudblazor.com, bUnit questions to bunit.dev.**
  Several tasks lost significant time decompiling the assemblies to learn
  documented behaviour.

---

## 6. Open items

**Must happen before merge:**

1. **The hand browser check.** No test can do it: bUnit applies no stylesheet,
   and both navigation branches are always in the DOM. Open the app above and
   below 960px and confirm exactly one sidebar is visible at each, that the
   hamburger appears only below, that there is no flash of the wrong one on
   load, and that the detail overlay's back-button close keeps you on the
   screen. This never ran during the implementation — Docker was not available
   — and the one Critical defect the whole-branch review caught was exactly the
   kind this check exists to find. Needs the compose stack up for Keycloak and
   Mongo.
2. Tasks 8, 9 and 10, and the task-7 review.
3. A final whole-branch review over `60b0f60..HEAD` once plan 11 is done.

**Parked, with reasons in the ledgers:**

- `NavSidebar` has no one-line comment explaining the `MudNavLink`
  `Href`/`OnClick` gotcha. AGENTS.md's no-comments rule carves an exception for
  exactly this; the process allowed one fix wave, not two.
- The email-claim fallback has no test, though the demo Keycloak user is
  precisely that case.
- `AppState` is constructed twice in two test classes — inert today.
- `NameDialog` has no direct test.
- `ReplicaSearch` results are in arbitrary order.
- `AvatarColor.InitialOf` has no null guard and does not require a letter.

**Known cosmetic, to look at during the browser check:** the app bar is hidden
entirely at `md`+, so the desktop may carry an empty toolbar-height strip.

---

## 7. How the run was being driven

`superpowers:subagent-driven-development`: one implementer subagent per task
from a brief extracted out of the plan, then a task-scoped review of its diff,
then a fix loop until clean, with every ruling appended to the plan's ledger.
Reviews found real defects repeatedly — several of them in the plan's own
sample test code, including tests that could not fail. Keeping the review gate
is worth more than the speed of skipping it.
