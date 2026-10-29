# End-to-end tests — design

Status: Built. Issue #107. Decision of record:
`adr/0077-end-to-end-tests-drive-the-compose-stack-with-playwright.md`.
Extends `specs/testing-spec.md`, which keeps the unit and integration rules.

## 1. Problem and scope

Unit tests cover the domain and bUnit components; integration tests cover
handlers and HTTP against a Testcontainers MongoDB. Nothing drives the real
application: the WebAssembly client in a browser, signed in through Keycloak,
talking to the API over HTTP, syncing a replica. Regressions in that seam
(OIDC redirects, CORS, CSP, sync, offline) reach the maintainer's own phone.

In:

- A Playwright suite in `test/e2e/` that runs thirty-four whole-scenario
  journeys through a browser against the Docker Compose stack.
- Fixtures that create a Keycloak user per test and sign in through the real
  login form.
- A CI job that builds the stack, runs the suite and gates deploy.

Out:

- Separate API-only end-to-end tests. The browser scenarios call the API
  directly where a check needs it (§3.4); the Testcontainers integration
  suite already covers endpoints in isolation.
- Phone layouts beyond single steps (scenario 17 checks My Day's tabs at
  phone width), cross-browser runs, visual regression, load.
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
- Target: `docker/compose.yaml` layered with `docker/compose.e2e.yaml`,
  `docker/.env.example` as the env file, built from the working tree:
  `docker compose -f docker/compose.yaml -f docker/compose.e2e.yaml
  --env-file docker/.env.example -p pspad-e2e up -d --build`. Project name
  `pspad-e2e` keeps its volumes apart from the developer stack. The override
  moves Keycloak to host port `18080` (its hostname and the app's authority
  follow), so the suite runs beside a developer's own Keycloak on `8080`.
  The app stays on `5001` and the API on `5000`, because the realm's
  redirect URI names `http://localhost:5001/*`; the developer stack's app
  and API must be down while the suite runs.
- Before the first test a global setup polls `GET /health` on the API,
  `GET /` on the app and `GET /realms/psplace` on Keycloak, up to 180 s,
  then signs a throwaway account in once through the browser. A freshly
  imported realm generates its signing keys on the first login, and
  parallel first logins lose that race with `invalid_token` on userinfo;
  the warm-up also pays the app's first WASM load and the API's first
  provisioning before any scenario is timed.
- Base addresses come from the environment with those defaults:
  `E2E_APP_URL`, `E2E_API_URL`, `E2E_KEYCLOAK_URL`, plus
  `E2E_KEYCLOAK_ADMIN_USER` and `E2E_KEYCLOAK_ADMIN_PASSWORD`.

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
`createTask(name)`, `openTaskPanel(name)`, `setDueDate(day)`, `syncNow`,
`liveStreamOpened` (resolves when the page's `/api/live` stream answers 200),
`setTaskTime(start, end)`, `timelineBlock(name)`, `setLanguage(nativeName)`
(waits for the reload the language change forces).
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
`CompleteTask`), and reads `CommandResponse[]` (`accepted`, `rejection`). `anotherDevice(page)`
wraps this as a second device of the same user: scenarios 22 and 25–31
arrange many tasks, items, goals and repeats through it (`createTasksOn`,
`createItemsOn`, `createGoalOn`), and 12, 18, 32, 33 and 34 budgets, entries
and balances (`createBudgetOn`, `recordOn`, `startBalanceOn`), which also
proves the open page picks them up without a reload. The sharing scenarios
also use the API for what a user can't click cheaply: 10 and 34 spend join
attempts (`tryJoin`) to close a link by wrong codes or reach the join limit,
and 11 publishes a second snapshot and ticks it as an anonymous visitor
(`publishSnapshotOn`, `markOnSnapshot`).

### 3.5 Offline

`context.setOffline(true)` drops `navigator.onLine` and fails every fetch
the page makes; the client's `BrowserConnectivity` then probes the API,
fails, and the status footer and sync stamp show offline. Chromium's offline
emulation does not reach requests a service worker makes, and the PWA's
worker proxies the probe, so the offline scenario's context is created with
`serviceWorkers: 'block'` (13, 14 and the sharing scenarios 10, 11 and 34
too). Reloading while offline would need that worker's cache, so no
scenario reloads offline; a page that must open offline is reached by
in-app navigation (`openInApp`, Blazor's `navigateTo`).

## 4. Scenarios

One spec file per scenario, one `test` per file, named by number so the
report reads in order. Every scenario is a whole journey: it creates what it
needs, checks the outcome in the UI, and where the server matters, checks
through the API. All names carry the account's hex suffix so a leak between
tests would be visible, not silent.

1. **First sign-in.** A visitor sent to Welcome from `/inbox` sees the
   headline, the GTD and docs links, a preview of My Day and a Log in link
   carrying the return address. Fresh user signs in through Keycloak; lands
   on My Day with "Nothing planned for today." and no Overdue or Starred
   section, and the Inbox says "Nothing captured."; the sidebar badge shows
   the Keycloak name and email; Settings shows the same and time zone
   `Etc/UTC`.
2. **Capture and triage.** Inbox: the empty state opens Capture, a blank
   capture adds nothing; capture two items (Enter and Add), newest first with
   their capture time; reword one while no list exists and Convert is off.
   With lists, a goal and a reference list made, convert it with list, due
   date, priority, goal, star, steps and a description; the
   next item keeps the list; discard it: "Nothing captured.". An item
   captured through the API shows up without a reload; the list's task
   carries everything the panel set. After picking `Asia/Kolkata` in
   Settings, the Inbox shows capture times in that zone.
3. **Area, list, task lifecycle.** A new area waits for a name (a blank
   Enter adds nothing) and opens once added; its empty state, by Enter,
   opens "New list · <area>", which also waits for a name and makes a task
   list. The list reads "No tasks yet." and counts open tasks under its
   area name; a task with two steps, one ticked, shows 1/2; completing it
   moves it under "Completed · 1" and the area reads "1 list · no open
   tasks". Deleting the list warns how many tasks go with it and lands on
   the area; its old address says the list is gone. The area panel renames
   as you type, says it has no lists, and its Delete asks first (Cancel
   keeps it); the FAB's Delete area lands on My Day.
4. **Today rule and time zone.** Three tasks: due today, due tomorrow, no
   due date. My Day lists exactly the first and neither of the others;
   picking tomorrow in the week strip's date dialog lists exactly the
   second. A row names its list, there is no Overdue or Starred section,
   today's and tomorrow's week-strip days read "has tasks" and the My Day
   link counts 1. Clicking a row opens its panel; on tomorrow the first task
   is carried under Overdue, the heading reads "Tomorrow, any time" and
   opening a task keeps the day. Complete the first: it moves to Completed
   and the count goes. In Settings pick a
   time zone whose local date differs from UTC right now
   (`Pacific/Kiritimati` when the UTC hour is 12 or later, else
   `Pacific/Pago_Pago`); My Day's day bar shows that local date and the
   second task is on that day's plan or still on tomorrow's accordingly.
   The step half of the Today rule is scenario 21.
5. **Overdue and the week strip.** Task due yesterday through the picker;
   My Day shows it under Overdue in error colour. Previous week, next week,
   pick a date in the dialog, **Today** returns; the picked day's bar text
   follows each move. A later day still carries the task under Overdue and
   its FAB adds a task due that day; a past day shows only what was
   completed and its FAB adds a task due today. Making the task repeat drops
   its due date, so it leaves Overdue.
6. **Recurring task.** Task set to repeat Daily. My Day shows it in the
   anytime section, never under Overdue. Complete it: Completed section.
   Next day in the strip shows it pending again. Its panel shows Repeat,
   Until and Remind and "Done 1 time · 1 in a row". A second task repeats
   from yesterday (custom start): yesterday reads Skipped, never Overdue.
   Custom repeats follow a moved start (weekly, monthly), save interval,
   unit and weekdays, and yearly; Until sets the end date and going back to
   one-time drops repeat and end, also after reopening.
7. **Goal with linked tasks.** New goal; two tasks linked through the task
   panel's Goal row; complete one: goal page lists one open task and
   "Completed (1)". Star the goal's
   remaining task: it appears under Starred on My Day but not in the plan.
   My Day's row names its goal and the goal itself is no row. Set status
   Achieved: Goals lists it under Achieved; the task's Goal row still names
   it but offers no goal in progress. A second goal can be chosen, cleared
   and chosen again; deleting it unlinks the task on its list and on My
   Day. An achieved goal's page still lists its task.
8. **Reference list and field types.** Reference list; item with five
   fields, one per display kind: Text, Link (renders a link with `href`),
   Path (renders as code), Quantity (normalised), Automatic with a URL value
   (detected as a link). Reorder two fields by their drag handles, edit one,
   remove one. Reload: order and kinds persist. Delete the item.
9. **Reorder lists and move a task.** Area with two lists; reorder them in
   the area panel by drag; the board follows after reload. Move a task to
   the other list through the task panel's List row; it shows there and the
   source list's count drops.
10. **Share a list, join, remove, and the API says no.** The owner's card
    shows no shared marker until **Create invite link** (a QR code, "30 min
    left", a link carrying the XXX-XXX code, "Shared · 1 person"); **End link
    now** before anyone joins unshares it. The other user opens the list's
    join link, which asks for the list's code, and joins with it from a
    settled app; Back skips the join page, and their My Day lists the shared
    task due today. With no areas of their own, Areas lands on Shared with
    me (its phone chip current, only the card's Edit, Back to area leading
    there); the owner has no such chip and the card says "Shared · 2
    people". Sent to the list's snapshots view, the member gets read-only
    details; Members names the owner with no invite, and offline Leave is
    off while filing stays on. Leaving empties Shared with me; five wrong
    codes (API) close the link and the owner reads why; a new link is
    joined (a code in the link joining at once is a skipped BUG step: an
    opening sync can drop the list's tasks). The member files the list under
    their own area (Back follows), sees only Add task in the FAB with a Goal
    row in its draft, completes a task and links a shared task to their own
    goal. Offline the owner can't remove, stop or renew; ending the link
    keeps the member and the marker. The owner removes the member: the list
    is gone for them, the API sync lacks it and `CompleteTask` on its other
    task comes back `accepted: false`.
11. **Public snapshot.** A task added just before publishing goes into the
    1 day link, whose QR code opens it. A visitor without an account, in a
    Polish browser, reads the frozen copy (owner, progress, to-do and done
    sections, a description, a step done by the owner without a checkbox)
    and ticks a task and a step; a reference list's snapshot (published
    through the API) shows an item's field and ticks. The owner sees one
    mark on a task ticked on two snapshots and on a task with only a step
    ticked; the panel's Dismiss clears the task's and the reference item's
    marks. A signed-in visitor gets Open PSPad; List snapshots starts empty,
    then lists both newest first with expiry and opening dates; offline a
    row opens its saved copy with ticks paused, one never opened asks to
    connect, and an unreachable server falls back to the saved copies.
    After Revoke a tick is refused and unticks, and a reload says "This
    snapshot has expired or never existed."
12. **Budget month.** Budgets starts empty; a new budget waits for a name
    and opens on this month's overview. An expense in PLN and an income in
    EUR with a typed rate sum in PLN; switch month back and forth. Last
    month's entries from another device make the usual month: expected
    month end, pace, "Food already past a usual month", where money went and
    the five most recent entries, each opening its entries. Entries list by
    day with a caption and narrow by kind, search, category (also in an
    empty month) and sort by time recorded. An entry edits and deletes in
    its panel; a new expense starts in the default currency with the latest
    rate, takes NBP's (stubbed) and creates a category; adding in a past
    month dates it on its last day. Each view has its address, the month row
    shows on Entries and Balance only, an old address still opens its view,
    the phone FAB offers expense or income, and the overview starts this
    month's balance.
13. **Balance month.** Balance: a first month offers Start; add a cash
    holding and an investment holding in EUR; net worth equals the sum in
    PLN, holdings group by type and the 12-month table shows them. With NBP
    down the "At NBP today" figure stays away and is not asked again; a
    holding renames, retypes and is removed in its panel, a new one suggests
    the latest rate and takes NBP's. With NBP answering, the figures value
    holdings at today's rate once per currency; offline they hide and the
    NBP button is off, online they return. Deleting the month asks first.
    Once another device adds last month's balance, starting this month
    copies its holdings and the overview shows the net worth. NBP answers
    come from a `context.route` stub; the service worker is blocked so
    going offline reaches the connectivity probe.
14. **Offline capture and sync.** Create a list with one task; go offline;
    the footer says offline; add a task and complete the first; go online;
    **Sync now**. A second signed-in context of the same user shows both
    changes after its first sync.
15. **Search, statistics, theme, sign out.** Create and complete a task;
    `/search?q=` finds it; Statistics shows Done today at 1; Settings:
    theme Dark sets `html[data-theme="dark"]` and survives a reload; Sign
    out with confirmation lands on Welcome.
16. **Live updates.** Owner and a member each hold the list open in their
    own context, each with its `/api/live` stream confirmed open. The member
    adds a task and the owner's page shows it; the owner adds one and the
    member's page shows it; the member completes it and the owner's page
    shows Completed · 1. A second context of the owner adds a task and both
    other pages show it. The owner removes the member: the member's page
    loses "Shared with me". Every wait is 20 s, well
    under the 60 s poll, so a pass proves the nudge, not the fallback; no
    page clicks Sync now.
17. **Scheduled day.** A fresh day shows "Schedule empty for today." over a
    folded axis: the current hour between bands. A task due today with a time 09:00–10:30 and one
    due today without a time: My Day lists the first under Scheduled (count 1) and
    the second under "Today, any time"; the timeline block carries both
    times, and the Scheduled row reads 09:00–10:30. At phone width the
    Schedule tab reads "Schedule · 1" and swaps the task list for the
    timeline. Ticking the block's checkbox takes it off the timeline and out of
    Scheduled, the schedule reads empty again, and Completed · 1 lists it.
    Tomorrow's empty schedule names its date and a free slot there opens
    "New task" due Tomorrow for an hour from that time. Clicking the first
    empty half hour today opens "New task" with that start time.
18. **Language.** Settings shows English; choosing Polski reloads the app
    into Polish (Ustawienia, Mój dzień) and a reload keeps
    it. A second context of the same user signs in and lands in Polish
    without touching Settings, since the language is stored on the user.
    The Polish Inbox reads "Nic jeszcze nie dodano." and Budgets reads
    "Nie masz jeszcze budżetów."; a budget from another device shows Polish
    amounts, labels and plurals.
    Choosing English brings the first page back to English.
19. **Step rename.** A task with one step: edit the step's name in the
    task panel and press Enter; close and reopen the panel, then reload:
    the new name persists. Clearing the name and blurring puts the old
    name back, and so does Escape after typing a new one (issue #199).
20. **Add from My Day.** My Day's FAB opens "New task" due Today with Add
    task disabled; two draft steps and a name still leave it disabled until
    the List row picks a list; the List row reads "No list", Enter adds
    nothing and Escape reverts a draft step's new name. Saving puts the task
    under "Today, any time" with `0/2` steps, and the list shows it. A task
    made due today through the API shows on My Day without a reload.
21. **Step deadline.** A task with two steps: set the second step's
    deadline to Tomorrow from its date control. The list's card shows
    `↳ Order boxes · <date>`; My Day today does not list the task, and
    tomorrow (through the week strip's date dialog) lists it under any
    time. A second task with one step due Today and one due Tomorrow is
    under any time today and under Overdue tomorrow, not under any time.
    Step rows under a task show their date, not a relative word. Back on
    today, the second task has two steps due today: ticking "Get new photos
    taken" checks only that step and keeps the task on the day; ticking
    "Book an appointment" says it moved to Tomorrow, and tomorrow then lists
    it under any time. Ticking the first task's row completes it (issue
    #202, `adr/0078`).
22. **List filter, sort and calendar.** Two High-priority tasks due
    tomorrow and one plain task: filtering on High hides the plain one,
    sorting by Name orders the two A → Z, and both survive a reload with
    the badge on 1. The Calendar view puts both on tomorrow's cell;
    clicking it lists them as rows, and ticking one leaves one. Cleared,
    the month says one task has no date and "Show them in List" lists just
    that one. Filter chips toggle and count on the badge, a goal of the
    list's tasks is a chip, nothing matching says "No tasks match." and
    keeps the open count, each active group is one closable chip, and
    Clear keeps the sort (issue #198, `list-filter-and-calendar-design.md`).
23. **Task panel.** An open task names its area and list and its created
    date and shows only the rows that apply. Rename (Enter creates nothing),
    star, due date by quick pick and calendar, Remind presets, hint and a
    custom lead time, a time that hides once the due date is cleared,
    priority, a rendered description with the row's notes marker, a list
    picker without reference lists that moves the task to another area, and
    a delete that asks first.
24. **New task draft.** A new task in a list waits for a name and offers no
    list, delete, remind or description; closing forgets draft steps. Steps,
    star, due date, reminder, priority and time land on the task Enter adds.
    A repeating draft goes back to one-time, drops a due date before its
    custom start and keeps its rule, end date and reminder.

25. **Area board cards.** An empty task card offers "Add one" and its
    header Add task opens "New task · <area> › <list>". Tasks sent through
    the API show on their card without a reload: a card previews five, counts
    all seven, and "Show all 7" opens the list; completed, deleted and ended
    tasks stay off it. Ticking a repeat on a day it skips saves nothing and
    ticking a ticked repeat unticks today, both after a reload (the stale
    tick the first leaves is a skipped BUG step). A collapsed card stays
    collapsed across a reload. The card menu edits the list; deleting an
    area or a list warns how many lists and tasks go with it.
26. **List page.** Tasks from the API appear without a reload, other
    lists' and deleted tasks never; completed and ended ones sit under
    Completed. Ticking an ended repeat or a skipped day saves nothing, a
    ticked repeat unticks today; completing one of two same-named tasks
    leaves the other open.
27. **Reference items.** An empty reference list offers Add item but no
    task, filter or view toggle; the new-item panel shows Labels,
    Description and its list. Draft labels keep their order and kind,
    Enter in the label row does not add the item, and the server stores
    labels, kinds and description; the item is added without opening (its
    labels missing from the panel until a reload is a skipped BUG step).
    The panel renames and stars, its List row offers reference lists only
    and moves the item to another area; a new item can go to another list,
    and a closed draft is forgotten.
28. **Goals, the goal panel and a goal page.** The empty state opens "New
    goal", which waits for a name and keeps a due date picked first. Goals
    from the API appear without a reload, in progress ordered by due date,
    undated last, an overdue one marked. Rows count done tasks (an ended
    repeat counts), the row menu offers the two closing statuses without
    opening the goal, and Not achieved gets its own section. The panel
    renames as you type, sets and clears the due date, saves the status,
    and Delete asks first. A goal page shows status, "2 of 3 done", the due
    date with days left, a Progress chart (recurring tasks not counted) and
    a Pace card; rows name their list but not the goal, another goal's
    tasks stay off, and a task opens on the page. An overdue goal says how
    late it is; an empty one says "No tasks linked" and "No open tasks."
    with no chart. Deleting the goal asks first and returns to Goals; its
    address then says it is gone. Deleting a list takes its tasks off the
    goal; the FAB opens a new goal.
29. **Areas index and chips.** At phone width every area is a chip in
    order with the open one marked current, and New area opens the panel.
    `/areas` opens the remembered area, falls back to the first once it is
    deleted (its chip gone), and once another device deletes every area it
    offers a new one.
30. **Reference rows.** A reference card adds an item without opening it,
    previews five of seven with "Show all 7" (its count reading "open
    tasks" is a skipped BUG step), stars a row to the top and opens
    a row. The board counts lists, open tasks and items, and deleting the
    list or the area warns what goes with it. On the list, starred items
    come first, then by position; labels are chips in order, a quantity
    normalised, a link opening in a new tab, a path copied to the
    clipboard.
31. **List panel.** An empty task list names its kind and area and offers
    its first task from the FAB. The panel names the list, its kind and area, renames as
    you type and moves the list to another area. Members and Public
    snapshots open their own views whose back arrow returns to the details.
    Deleting from the panel stays on the board there and goes to the area
    from the list's own screen; a deleted or unknown list says it is gone.
32. **Budgets and budget settings.** Budget cards sum this month in PLN,
    show the latest net worth up to this month or offer to start it, and
    archived budgets wait behind Show archived. Settings list both kinds of
    categories with their use; add, rename (a clash says to merge), merge
    and remove them, and a category opens its entries. Rename renames the
    budget. An archived budget reads only: no adding, editing, starting,
    importing or category actions, its stats still show, and Restore
    brings quick add back. An unknown budget says so.
33. **Budget history: stats and CSV import.** An empty budget's stats read
    as dashes with no forecast, ranking or balances and a zeroed monthly
    table. Entries from another device average over the months that have
    them, forecast next month, rank where money goes and fill the
    per-month, per-category and net-worth tables (a month without a balance
    reads as dashes). Presets pick their range, picking one again keeps it,
    Custom shows the picker, and a ranked or forecast category opens its
    entries. Settings: the entries fixture previews error rows, new
    categories and rate sources, retries a missing NBP rate, asks before
    leaving and imports; the balances fixture says when a column is missing,
    groups rows under their month and imports; Entries and Balance show the
    imported data. An unknown import kind says so.
34. **Share a budget.** An unshared budget has no members button; settings
    offer sharing from "Only you", and the panel creates a budget invite. The
    other user's budget link with a wrong code says so above the entry card
    and drops the code; an incomplete code can't join, a typed wrong one
    keeps the input, and a lower-case code with a dash goes on Enter; a
    dropped connection offers Retry, which joins and opens the budget. The
    member sees "Shared by", no Rename or Archive but category Add, and a
    members panel with the owner and no invite; Budgets lists their own
    budget first, then the shared one saying who shared it. The owner's head
    count opens the members, the card reads "Shared · 2 people", and offline
    nothing changes who is in. Archived, the panel can't invite and the
    member reads who archived it with no Restore. The member leaves (back to
    Budgets), rejoins from a link with its code, then runs out of join
    attempts and is told to wait. Removing the member takes the budget from
    them; Stop sharing ends the link.

## 5. Timing

- `fullyParallel: true`, four workers locally and in CI, one test per file
  so every scenario is its own worker slot.
- Per test timeout 90 s; expect timeout 10 s; the suite target is under
  five minutes (ADR-0079). A scenario that cannot fit splits, it never widens the
  timeout.
- Retries: 0 locally, 1 in CI; a retry is a flake to fix, reported in the
  HTML report.
- Scenarios 10, 11, 14, 16, 18 and 34 are the heaviest (two or three contexts);
  12, 13, 25, 27, 28, 29, 31 and 32 are the heaviest single-context ones. A scenario
  that runs past a minute at two workers on a two-core machine splits;
  data a step only needs to exist is arranged through `anotherDevice`,
  not clicked through the UI.

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
  package.json  package-lock.json  tsconfig.json  playwright.config.ts  env.ts  global-setup.ts
  fixtures/      keycloak.ts (admin API)  accounts.ts (account fixtures)
  helpers/       sign-in.ts  nav.ts  panels.ts  areas.ts  lists.ts  tasks.ts  inbox.ts  today.ts  dates.ts
                 settings.ts  goals.ts  references.ts  sharing.ts  snapshots.ts  sync.ts  api.ts  budgets.ts
  scenarios/     01-first-sign-in.spec.ts … 34-budget-sharing.spec.ts
```

One helper module per screen family, one scenario per file, no shared
mutable state between files. CSV fixtures are built in the import scenario
so their dates follow the current month. `README.md` in the folder says how
to run it locally, including from the official Playwright container when
the host lacks Chromium's libraries.

## 8. Docs and records

- `specs/testing-spec.md` gains an **End-to-end** category pointing here.
- `README.md` Development section gets the three commands.
- `AGENTS.md` §3 table gets the row; §4 gets AD-18 for the ADR.
- `adr/0077` records the decision and the alternatives (Playwright for
  .NET inside the solution; API-only end-to-end tests; storage state reuse).

## 9. Keeping the suite current

A pull request that adds or changes what a user can do adds a scenario or
extends the one that already walks that screen; a pull request that changes
markup or copy a scenario reads fixes the scenario in the same change. The
`e2e` CI job gates deploy, so a red scenario blocks the merge either way.
Scenario numbers never change; a new one takes the next number. Helpers
locate by role, label, text and the `pspad-*` classes the UI spec names, so
renaming one of those is a suite change too. Nav links carry a count once
the user has items, so `navLink` finds them by `href`; a link found by its
name alone misses when the count appears and a `goto` fallback reloads the
page, which is a different flow from clicking.

Since live updates (ADR-0076) every command a device sends echoes back as a
nudge whose pull re-renders the open panel. Helpers that close a panel or
edit inside one retry inside `toPass`; a new helper that types into a panel
should do the same.

UI test-driven work starts with the failing end-to-end step; pages and layout have no bUnit tests (ADR-0079).

## 10. Risks

- **Drag and drop** (scenarios 8 and 9) rides on MudBlazor's HTML5 drag
  events; Playwright's `dragTo` drives them in Chromium. If a handle
  refuses, the scenario falls back to keyboard or the step is reported as
  a UI gap, not papered over.
- **Keycloak start** takes 20–40 s with realm import; the global setup's
  180 s wait covers it, and the compose `--wait` only covers services with
  a health check. The first logins after the import are the fragile part,
  hence the warm-up sign-in (§2).
- **Clock edges.** Tests that depend on "today" compute it in UTC at the
  start and would be wrong across midnight UTC; a run that straddles it is
  rerun, not patched.
- **Closing drawers and fading menus.** A panel's overlay lingers while it
  closes and the FAB menu's items are transparent with pointer events off
  while it is shut, so the helpers wait for the drawer's closed state and
  the menu's open class instead of visibility. A MudBlazor select's popover
  also lingers while it closes, so a helper waits for no open popover
  before opening the next.
