# End-to-end tests — design

Status: Proposed. Issue #107. Decision of record:
`adr/0077-end-to-end-tests-drive-the-compose-stack-with-playwright.md`.
Extends `specs/testing-spec.md`, which keeps the unit and integration rules.

## 1. Problem and scope

Unit tests cover the domain and bUnit components; integration tests cover
handlers and HTTP against a Testcontainers MongoDB. Nothing drives the real
application: the WebAssembly client in a browser, signed in through Keycloak,
talking to the API over HTTP, syncing a replica. Regressions in that seam
(OIDC redirects, CORS, CSP, sync, offline) reach the maintainer's own phone.

In:

- A Playwright suite in `test/e2e/` that runs fifteen whole-scenario
  journeys through a browser against the Docker Compose stack.
- Fixtures that create a Keycloak user per test and sign in through the real
  login form.
- A CI job that builds the stack, runs the suite and gates deploy.

Out:

- Separate API-only end-to-end tests. The browser scenarios call the API
  directly where a check needs it (§3.4); the Testcontainers integration
  suite already covers endpoints in isolation.
- Phone layout, cross-browser runs, visual regression, load.
- Any `data-testid` in the application. Selectors use roles, labels, text
  and the existing `pspad-*` classes. A flow that cannot be driven that way
  is a separate change to the UI, with its own review.

Budget: the whole suite finishes in **two to three minutes** wall clock on a
laptop and in CI, excluding the image build.

## 2. Stack and tooling

- Playwright Test, TypeScript, Chromium only, desktop viewport 1280×800.
- Own `package.json` in `test/e2e/`, outside the .NET solution. `dotnet
  test` never sees it; `npm test` in that folder runs it. Node 22, pinned
  through `engines` and `package-lock.json`.
- Target: `docker/compose.yaml` with `docker/.env.example`, built from the
  working tree (`docker compose --env-file docker/.env.example -p pspad-e2e
  up -d --build`). Project name `pspad-e2e` keeps its volumes apart from the
  developer stack. Ports are the stack's: app `5001`, API `5000`, Keycloak
  `8080`. The realm's redirect URI is `http://localhost:5001/*`, so the
  developer stack must be down while the suite runs; the runner says so when
  the ports are taken by a different compose project.
- Before the first test a global setup polls `GET /health` on the API,
  `GET /` on the app and `GET /realms/psplace` on Keycloak, up to 120 s.

## 3. Harness

### 3.1 Accounts

Keycloak seeds no users and registration is off. A test fixture `account`
creates a user through the admin API before each test and deletes it after:

- Admin token: password grant on the `master` realm, client `admin-cli`,
  credentials `KEYCLOAK_ADMIN_USER` / `KEYCLOAK_ADMIN_PASSWORD` from
  `.env.example` (`admin` / `admin`), overridable through the same
  environment variables.
- User: `e2e-<8 hex>`, email `<username>@e2e.local`, first name `E2E`, last
  name the hex, enabled, email verified, password `e2e-password`, non
  temporary.
- A second fixture `otherAccount` does the same for the sharing scenario.

One user per test means every scenario starts from an empty account and may
assert exact contents ("My Day shows exactly these two tasks"). Deleting the
user afterwards keeps the stack small across local runs; the stack is thrown
away in CI anyway.

### 3.2 Sign-in

`signIn(page, account)` opens `/`, clicks **Log in** on Welcome, fills
Keycloak's form (`#username`, `#password`, `#kc-login`) and waits for the
heading **My Day**. The app keeps its session in IndexedDB and the OIDC
state in `sessionStorage`, so Playwright's storage state is not reused: each
test signs in, which costs about three seconds and buys full isolation.
First sign-in provisions the user with time zone `Etc/UTC` (no `zoneinfo`
claim), so "today" in every test is the UTC date, computed in the test.

### 3.3 Page helpers

Thin functions, not page-object classes: `openArea`, `createList(kind)`,
`createTask(name)`, `openTaskPanel(name)`, `setDueDate(day)`, `syncNow`.
Each lives in `test/e2e/helpers/` next to what it drives. Helpers locate by
`getByRole`, `getByLabel`, `getByText` and the `pspad-*` classes the UI spec
names; the panel's **Add …** button is `.pspad-panel-save`.

Dates: `DueDateRow` quick picks cover Today and Tomorrow; any other day goes
through the picker dialog (`.pspad-due-pick`, then the day button in the
static `MudDatePicker`, stepping a month back first when the target month
differs from the shown one).

### 3.4 API access from a test

Some checks are about the server, not the screen. `bearerToken(page)`
triggers **Sync now** and reads the `Authorization` header of the next
`/api/sync` request, so the test holds the same token the client does. An
`APIRequestContext` with that header then calls the API: `GET
/api/sync?since=0`, `POST /api/commands` with `[{type, payload}]`
envelopes (`CommandCatalogue` resolves `type` by class name, e.g.
`CompleteTask`), and reads `CommandResponse[]` (`accepted`, `rejection`).

### 3.5 Offline

`context.setOffline(true)` drops `navigator.onLine` and fails every fetch;
the client's `BrowserConnectivity` listens to the `offline` event and the
status footer shows it. Reloading while offline depends on the service
worker having cached the whole bundle, which a fresh context cannot
guarantee inside the budget, so no scenario reloads offline.

## 4. Scenarios

One spec file per scenario, one `test` per file, named by number so the
report reads in order. Every scenario is a whole journey: it creates what it
needs, checks the outcome in the UI, and where the server matters, checks
through the API. All names carry the account's hex suffix so a leak between
tests would be visible, not silent.

1. **First sign-in.** Fresh user signs in through Keycloak; lands on My Day
   with "Nothing planned for today."; the sidebar badge shows the Keycloak
   name and email; Settings shows the same and time zone `Etc/UTC`.
2. **Capture and triage.** Inbox: capture two items; open one, pick a list
   (created on the way through Areas) and organise it into a task; discard
   the other. Inbox shows "Nothing captured."; the list shows the task.
3. **Area, list, task lifecycle.** New area, new task list, task with two
   steps. Tick a step: the row shows 1/2. Complete the task: area subtitle
   reads "1 list · no open tasks". Delete the list, then the area: Areas
   shows "No areas yet."
4. **Today rule and time zone.** Three tasks: due today, due tomorrow, no
   due date. My Day lists exactly the first; the second sits under Coming
   up; the third is absent. Complete the first: it moves to Completed. In
   Settings pick a time zone whose local date differs from UTC right now
   (`Pacific/Kiritimati` when the UTC hour is 12 or later, else
   `Pacific/Pago_Pago`); My Day's day bar shows that local date and the
   second task moves to the plan or stays under Coming up accordingly.
   The client exposes no step due dates, so the step half of the Today rule
   stays with the unit tests.
5. **Overdue and the week strip.** Task due yesterday through the picker;
   My Day shows it under Overdue in error colour. Previous week, next week,
   pick a date in the dialog, **Today** returns; the picked day's bar text
   follows each move.
6. **Recurring task.** Task set to repeat Daily. My Day shows it in the
   anytime section, never under Overdue. Complete it: Completed section.
   Next day in the strip shows it pending again.
7. **Goal with linked tasks.** New goal; two tasks linked through the task
   panel's Goal row; complete one: goal page lists one open task and
   "Completed (1)". Star the goal's
   remaining task: it appears under Starred on My Day but not in the plan.
   Set status Achieved: Goals lists it under Achieved.
8. **Reference list and field types.** Reference list; item with five
   fields, one per display kind: Text, Link (renders a link with `href`),
   Path (renders as code), Quantity (normalised), Automatic with a URL value
   (detected as a link). Reorder two fields by their drag handles, edit one,
   remove one. Reload: order and kinds persist. Delete the item.
9. **Reorder lists and move a task.** Area with two lists; reorder them in
   the area panel by drag; the board follows after reload. Move a task to
   the other list through the task panel's List row; it shows there and the
   source list's count drops.
10. **Share a list, join, remove, and the API says no.** Owner opens Members,
    **Create link**, reads the code. Other user opens the join link, enters
    the code, picks an area; the list shows under Shared with me. Member
    completes a task; owner syncs and sees it done. Owner removes the member.
    Member syncs: the list is gone. With the member's token the API returns
    a sync without the list or its id in `memberListIds`, and `CompleteTask`
    on the list's other task comes back `accepted: false`.
11. **Public snapshot.** Owner publishes a 1 day snapshot from Public
    snapshots; opens the **Open** link in a fresh unauthenticated context;
    ticks a task; the public page shows Marked. Owner syncs: the task row
    shows the snapshot chip; the panel's Dismiss clears it. Revoke: the
    public page shows "This snapshot has expired or never existed."
12. **Budget month.** New budget; expense in PLN and income in EUR with a
    typed rate; Entries shows both and the hero totals in PLN equal the
    expected sums. Switch month back and forth. Settings: Archive; the
    archived banner shows and the quick add buttons are gone.
13. **Balance and CSV import.** Balance: Start month, add a cash holding and
    an investment holding in EUR; net worth equals the sum in PLN. Settings:
    import the entries fixture then the balances fixture; preview counts
    match the fixture rows; **Import N rows** completes; Entries and Balance
    show the imported data.
14. **Offline capture and sync.** Create a list with one task; go offline;
    the footer says offline; add a task and complete the first; go online;
    **Sync now**. A second signed-in context of the same user shows both
    changes after its first sync.
15. **Search, statistics, theme, sign out.** Create and complete a task;
    `/search?q=` finds it; Statistics shows Done today at 1; Settings:
    theme Dark sets `html[data-theme="dark"]` and survives a reload; Sign
    out with confirmation lands on Welcome.

## 5. Timing

- `fullyParallel: true`, four workers locally and in CI, one test per file
  so every scenario is its own worker slot.
- Per test timeout 90 s; expect timeout 10 s; the suite target is under
  three minutes. A scenario that cannot fit splits, it never widens the
  timeout.
- Retries: 0 locally, 1 in CI; a retry is a flake to fix, reported in the
  HTML report.
- Scenarios 10, 11 and 14 are the heaviest (two contexts or two users).

## 6. CI

A new `e2e` job in `.github/workflows/ci.yml`, after
`build-and-unit-test`:

1. `docker compose -f docker/compose.yaml --env-file docker/.env.example -p
   pspad-e2e up -d --build --wait`.
2. `npm ci` and `npx playwright install --with-deps chromium` in
   `test/e2e`.
3. `npm test`.
4. On failure upload `test/e2e/playwright-report` and the traces (`trace:
   on-first-retry`), and print the stack's logs.
5. Always `docker compose -p pspad-e2e down -v`.

`deploy` adds `e2e` to its `needs`. Dependabot gets an `npm` entry for
`/test/e2e`, weekly, grouped minor and patch, like the others.

## 7. Layout

```
test/e2e/
  package.json  package-lock.json  tsconfig.json  playwright.config.ts
  fixtures/      keycloak.ts (admin API)  accounts.ts (account fixtures)
  helpers/       sign-in.ts  areas.ts  lists.ts  tasks.ts  panels.ts  sync.ts  api.ts  dates.ts
  data/          entries.csv  balances.csv
  scenarios/     01-first-sign-in.spec.ts … 15-search-statistics-settings.spec.ts
  global-setup.ts
```

One helper module per screen family, one scenario per file, no shared
mutable state between files. `README.md` in the folder says how to run it
locally.

## 8. Docs and records

- `specs/testing-spec.md` gains an **End-to-end** category pointing here.
- `README.md` Development section gets the three commands.
- `AGENTS.md` §3 table gets the row; §4 gets AD-18 for the ADR.
- `adr/0075` records the decision and the alternatives (Playwright for
  .NET inside the solution; API-only end-to-end tests; storage state reuse).

## 9. Risks

- **Drag and drop** (scenarios 8 and 9) rides on MudBlazor's HTML5 drag
  events; Playwright's `dragTo` drives them in Chromium. If a handle
  refuses, the scenario falls back to keyboard or the step is reported as
  a UI gap, not papered over.
- **Keycloak start** takes 20–40 s with realm import; the global setup's
  120 s wait covers it, and the compose `--wait` only covers services with
  a health check.
- **Clock edges.** Tests that depend on "today" compute it in UTC at the
  start and would be wrong across midnight UTC; a run that straddles it is
  rerun, not patched.
