# End-to-end tests — design

Status: Built. Issue #107. Decision of record:
`adr/0077-end-to-end-tests-drive-the-compose-stack-with-playwright.md`.
Extends `specs/testing-spec.md`, which keeps the unit and integration rules.

## 1. Problem and scope

Unit tests cover the domain and pure client rules; integration tests cover
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
- Phone layouts beyond single steps (scenario 17 checks My Day's tabs and
  01 the bottom bar, top bar and account drawer at phone width),
  cross-browser runs, visual regression, load.
- Any `data-testid` in the application. Selectors use roles, labels, text
  and the existing `pspad-*` classes. A flow that cannot be driven that way
  is a separate change to the UI, with its own review.

Budget: **under five minutes per shard in CI with four workers**, excluding the
image build; CI runs the suite as two shards (§5).

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
Each lives in `test/e2e/helpers/` next to what it drives.

**Locators.** Helpers and steps locate by role, label or text
(`getByRole`, `getByLabel`, `getByText`, `toHaveAccessibleName`,
`toHaveAccessibleDescription`), never by a `pspad-*` or `mud-*` class
(ADR-0080). Where nothing a user perceives tells an element apart, the
component gets an accessibility attribute, never a test id: the open detail
panel is the `complementary` landmark holding a Close button; task
and reference rows are `article`s named by the item; step, label and
area-list rows and the schedule's timeline blocks are `group`s named by
their text; My Day's sections and
the goals page's sections are `region`s named by their heading; the page
heading is described by its subtitle (`aria-describedby`); an overdue row
carries a visually hidden "Overdue"; the timeline's now line is an `img`
named "Now, HH:mm". Text added this way comes from the feature's `.resx` in
English and Polish. The panel's primary button is found by its name (Save,
Add …, Convert to task); popover menus are the unnamed `menu`s (the FAB menu
is named Actions); select options are `option`s.

**Guard.** `EndToEndLocatorGuard` (`test/PSPad.TestInfrastructure`, run by
`EndToEndLocatorGuardTests` in `PSPad.App.Tests`, so CI's unit job runs it)
scans `test/e2e/scenarios` and `test/e2e/helpers` and fails on a
`.pspad-…` or `.mud-…` selector, `toHaveClass` or `classList`. The
exceptions live in `test/e2e/locator-allowlist.txt`, one per line as
`file: line pattern — reason`; an entry that matches nothing fails too.
Today the list holds only MudTimePicker's clock, which has no role or name.

Dates: `DueDateRow` quick picks cover Today and Tomorrow; any other day goes
through the picker dialog (Pick a date…, then the day button of the
`MudDatePicker`, found by its number, stepping months with the picker's
"Previous month (…)" and "Next month (…)" buttons until the header button
names the target month).

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
attempts (`tryJoin`) to close a link by wrong codes or reach the join limit
(10's member also joins through it, see §4 entry 10),
and 11 publishes a second snapshot and ticks it as an anonymous visitor
(`publishSnapshotOn`, `markOnSnapshot`).

### 3.5 Offline

`context.setOffline(true)` drops `navigator.onLine` and fails every fetch
the page makes; the client's `BrowserConnectivity` then probes the API,
fails, and the status footer and sync stamp show offline. Chromium's offline
emulation does not reach requests a service worker makes, and the PWA's
worker proxies the probe, so the offline scenario's context is created with
`serviceWorkers: 'block'`; so are the contexts that go offline in 13, 10
(owner and member), 11 (owner and signed-in reader) and 34 (owner only; its
member stays online). Reloading while offline would need that worker's
cache, so no scenario reloads offline; the one page 11 must open offline,
a snapshot never opened before, is reached by in-app navigation
(`openInApp`, Blazor's `navigateTo`). The worker also hides the page's API
requests from `page.route`, so 01, 14 and 15, which answer or hold API
calls themselves (a failed `/api/me` at sign-in, a held first pull, a
step-up challenge on account deletion, an unreachable server, a silent
server on reload, statistics offline, an unsynced change at sign-out), block
it too.

## 4. Scenarios

One spec file per scenario, one `test` per file, named by number so the
report reads in order. Every scenario is a whole journey: it creates what it
needs, checks the outcome in the UI, and where the server matters, checks
through the API. All names carry the account's hex suffix so a leak between
tests would be visible, not silent.

1. **First sign-in and the shell.** A visitor sent to Welcome from `/inbox`
   sees the headline, the GTD and docs links, a preview of My Day and a Log
   in link carrying the return address, with no boot splash and no live
   stream; signing in from there in a second browser lands on the Inbox.
   An identity-provider failure and a sign-in whose `/api/me` answers 503
   land back on Welcome, signed out, with a warning shown once per arrival
   (an in-app return to Welcome drops it); logging in again holds the app (brand loader, no My Day)
   until the first pull lands, then shows the seeded areas. My Day says
   "Nothing planned for today." with no Overdue or Starred section, and the
   Inbox says "Nothing captured.". The sidebar names the user and lists My
   Day, Inbox, Goals, Statistics, List snapshots, Budgets, the areas,
   Settings and App info in that order with a clock and "PSPad · GPL v3";
   it counts an Inbox capture from another device, lists an active budget
   but not an archived one, drops an area deleted elsewhere, collapses
   Areas to "Areas · 2", and New area and New budget open their panels in
   place. App info shows the version, the licence and both links; an
   unknown address says "Not found". At phone width the bottom bar holds
   Inbox (with its badge), Areas, My Day, Goals and Budgets, marks the
   current one and none on Settings; a touch pull on My Day syncs (a CDP
   touch drag); the top bar names the screen, a list
   with its area subtitle and a Back to area link, carries Sync now with
   the stamp, and forgets a list that is gone; the avatar opens the account
   drawer (Statistics, List snapshots, Settings, App info, footer), which
   closes on navigation and when the window grows to desktop. Settings
   shows the account, time zone `Etc/UTC` and a Change password link to
   Keycloak's account console in a new tab. Deleting the account wants the
   email typed (any case), a fresh sign-in when the API asks for one (a
   Keycloak password prompt) after which the dialog reopens empty, keeps
   the dialog open on a failure, says an administrator must remove the
   sign-in when the API reports Keycloak was not cleaned up, then lands on
   Welcome with no return address, and the account can no longer log in.
   The first sign-in under load is scenario 35. Settings and the account's
   deletion moved to scenario 36 (round 3) so each half fits a minute at
   two workers.
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
   tasks"; the Add task row under the tasks opens a new task from the
   keyboard. Deleting the list warns how many tasks go with it and lands on
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
   My Day shows it under Overdue in error colour. Today is the current and
   pressed day with no Today button, the strip names its ISO week, and a
   touch swipe turns the week and back. Previous week, next week,
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
   The Repeat menu offers Yearly on today and no Never. Custom repeats
   follow a moved start (weekly, monthly), save interval,
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
   remove one. Reload: order and kinds persist. Delete the item. Copying a
   path (or failing to) and following a link leave the row closed; a
   `javascript:` value set to Link stays text; the field editor offers
   Remove, takes a dropped link, says a dropped file has no path and waits
   for a label. The add row moves from label to value on Enter, adds only on
   Enter with a label, and takes a dropped URI list or text. Cancel in the
   editor not closing it is a skipped BUG step.
9. **Reorder lists and move a task.** Area with two lists; reorder them in
   the area panel by drag; the board follows after reload. Move a task to
   the other list through the task panel's List row; it shows there and the
   source list's count drops.
10. **Share a list, join, remove, and the API says no.** The owner's card
    shows no shared marker until **Create invite link** (a QR code, "30 min
    left", a link carrying the XXX-XXX code, "Shared · 1 person"); **End link
    now** before anyone joins unshares it. The other user opens the list's
    join link, which asks for the list's code; typing it opens the list with
    the tasks it already had, and My Day lists the one due today.
    The member's My Day lists the shared task due today. With no areas of
    their own, Areas lands on Shared with me (its phone chip current, no FAB,
    only the card's Edit, Back to area leading there); the owner has no such
    chip and the card says "Shared · 2 people". Sent to the list's snapshots
    view, the member gets read-only details; Members names the owner with no
    invite, and offline Leave is off while filing stays on. The member files
    the list under their own area (Back follows), sees only Add task in the
    FAB with a Goal row in its draft, completes a task and links a shared
    task to their own goal, then leaves, which empties Shared with me. Five
    wrong codes (API) close the link and the owner reads why; a new link
    with the code in it rejoins the member at once, the list opening with its
    open tasks and the completed one. Offline the owner can't remove, stop or
    renew; ending the link keeps the member and the marker. The owner
    removes the member: the list is gone for them, the API sync lacks it and
    `CompleteTask` on its other task comes back `accepted: false`. Stop
    sharing on a list with a member in it has no step (34 covers the budget's
    separate implementation).
11. **Public snapshot.** A task whose push is still held when Publish is
    pressed goes into the 1 day link (publish syncs first), whose QR code
    opens it. A visitor without an account, in a
    Polish browser, reads the frozen copy (owner, progress, to-do and done
    sections, a description, a step done by the owner without a checkbox)
    and ticks a task and a step, with no way to edit the description; a
    reference list's snapshot (published
    through the API) shows an item's field and ticks. The owner sees one
    mark on a task ticked on two snapshots and on a task with only a step
    ticked, whose step reads "Marked on a snapshot"; the panel's Dismiss clears the task's and the reference item's
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
    copies its holdings and the overview shows the net worth. The month strip
    marks the current month, rests This month on it, names January's year,
    reads a month with a balance as "has entries", and its sheet opens on the
    shown year, picks a month of another year and jumps back. NBP answers
    come from a `context.route` stub; the service worker is blocked so
    going offline reaches the connectivity probe.
14. **Offline capture and sync.** Create a list with one task; go offline;
    the footer says offline; add a task and complete the first. Settings
    holds "2 pending", says "Offline · updated just now", and keeps Sync now,
    Sign out (with the reason) and Delete account off. Online, Sync now
    drains it to "Everything is synced." and sign-out is back. A second
    signed-in context of the same user shows both changes after its first
    sync. With `/api/sync` failing, Sync now raises the "Can’t reach the
    server" belt, the footer reads "Server unreachable" and the stamp
    "Couldn't update"; once it answers, the belt's Retry clears them. With
    `/api/me` and `/api/sync` silent, a reload on a list address with a
    task opens the list, the task panel and the stored name. An area
    renamed offline while another device deletes it comes back refused:
    the belt's Details opens Settings, which lists "That area no longer
    exists." until Discard this change; Dismiss closes the belt. With
    `page.clock` installed, fast-forwarding past UTC midnight moves an open
    My Day to the next day.
15. **Search, statistics, settings, sign out.** Create and complete a task
    linked to a goal; another device adds a completed, then deleted task and
    an Inbox capture. `/search?q=` finds the task; its result opens the
    panel, a reload keeps it, Back drops it. Statistics shows Done today and
    Last 7 days at 2, 30 days chosen, a bar per goal and one for no goal,
    this week's capture in the screen-reader table, 30 heatmap days without
    month captions, today's day announcing "2 completed", the shade key, and
    a readout that a tap fills and a second tap clears; 90 days adds month
    captions. The record feed opens on demand, a record opens its task, a
    deleted task's record goes nowhere. `/history` leads to Statistics (an
    empty page there is a skipped BUG step, PR #225); with the statistics
    API down a reload shows the offline note and the cached numbers.
    Settings: theme System/Light/Dark, Dark sets `html[data-theme="dark"]`;
    accent swatches toggle `aria-pressed`, the custom colour dialog cancels
    or applies; EUR as default currency; the time zone search narrows; all
    survive a reload. The Sync card says everything is synced; with
    Keycloak's revocation endpoint down, Sign out keeps the user signed in
    and says so. With `/api/commands` failing, a new task stays pending;
    Sign out asks about "1 unsynced change", Cancel keeps it, confirming
    drops it (the API never gets it) and lands on Welcome; Log in then asks
    Keycloak for the password again.
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
    empty half hour today opens "New task" with that start time. Tasks from
    another device: three overlapping ones share lines that tick one at a
    time, a two-hour block names its list, a half-hour one does not, a late
    one reads (+1), and a band unfolds its hours.
18. **Language.** Settings shows English and lists English and Polski;
    choosing Polski reloads the app
    into Polish (Ustawienia, Mój dzień) and a reload keeps
    it. A second context of the same user signs in and lands in Polish
    without touching Settings, since the language is stored on the user.
    The Polish Inbox reads "Nic jeszcze nie dodano.", the Statistics heatmap
    labels days "zrobiono" with a Polish hint and key, and Budgets reads
    "Nie masz jeszcze budżetów."; a budget from another device shows Polish
    amounts, labels and plurals.
    Choosing English brings the first page back to English.
19. **Step rename.** A task with one step: edit the step's name in the
    task panel and press Enter; close and reopen the panel, then reload:
    the new name persists. Clearing the name and blurring puts the old
    name back, and so does Escape after typing a new one (issue #199). A
    blank step is not added, and a removed step stays gone.
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
    #202, `adr/0078`). A past step deadline reads Yesterday and clears, an
    unset one offers no Clear date, and a ticked step or a repeating task
    takes none. At a viewport that leaves the My Day row just above 560px,
    a timed task with a long name and a step due in three days keeps at
    least 120px of its name, its details narrow instead, and the due-step
    mark keeps min(details, 140px) (issue #222; that the mark is still
    clipped by the details is a skipped BUG step).
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
    With every task dated the month has no undated line. Today is pressed
    when the month opens; a day with three tasks shows two
    in the list's order and "+2 more" with a repeat, which shows from its
    start; a day's row names its goal; every day is labelled.
23. **Task panel.** An open task names its area and list and its created
    date and shows only the rows that apply. Rename (Enter creates nothing),
    star, due date by quick pick and calendar, Remind presets, hint and a
    custom lead time, a typed time (shorthand, a hint for unreadable text,
    an end equal to the start refused, an end past midnight hinted (+1), an
    emptied end or start, the clock left untouched or with only the hour
    picked, Clear time), a time that hides once the due date is cleared,
    priority, a rendered description with the row's notes marker (Escape
    clears a blank one; a click or Edit opens the raw text, Escape or no
    change leaves it, a link or a selection keeps the view; another device's
    or name change not reaching the open panel is a skipped BUG step), a list
    picker without reference lists that moves the task to another area, and
    a delete that asks first.
24. **New task draft.** A new task in a list waits for a name and offers no
    list, delete, remind or description; closing forgets draft steps. Draft
    steps remove, rename, keep their name when emptied and keep a deadline
    through a rename; a repeating draft offers no step deadline. Steps,
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
    labels, kinds and description, a draft label edits and removes; the
    item is added without opening (its
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
    Public snapshots names the expiry it publishes (a preset or a picked
    date), warns when publishing or revoking fails, reads days left, ticks
    and entries, hides the QR code on its toggle, says live links need a
    connection when the server is unreachable and disables Publish offline
    (only after a redraw: a skipped BUG step).
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
    leaving, counts an unticked row out of the import and imports; the balances fixture says when a column is missing,
    groups rows under their month and imports; Entries and Balance show the
    imported data. An unknown import kind says so.
34. **Share a budget.** An unshared budget has no members button; settings
    offer sharing from "Only you", and the panel creates a budget invite. The
    other user's budget link with a wrong code says so above the entry card
    and drops the code; an incomplete code can't join, a typed wrong one
    keeps the input, and a lower-case code with a dash goes on Enter; a
    dropped connection offers Retry, which joins and opens the budget, and
    Back skips the join page. The member sees "Shared by", no Rename or
    Archive but category Add, and a members panel with the owner and no
    invite; Budgets lists their own budget first, then the shared one saying
    who shared it. The owner's head count opens the members, the card reads
    "Shared · 2 people", and offline nothing changes who is in. Removing the
    member takes the budget from them; a code in the link brings them back
    at once. Archived, the panel can't invite and the member reads who
    archived it with no Restore. The member leaves (back to Budgets),
    rejoins from a link with its code, then runs out of join attempts and is
    told to wait. Stop sharing with the member still in ends the link and
    takes the budget from them.

35. **First sign-in under load.** While another user's API client saves
    an area four times in parallel every 350 ms, three new accounts log in
    from Welcome one after another, each in a fresh browser, and each lands
    on My Day, never back on Welcome (`UserProvisioner` retries a write
    conflict).

36. **Account settings and deletion.** The second half of scenario 1: from
    Settings with the account's time zone and Change password link, a
    snapshot the account opened reads offline and its statistics stay on
    the device; deleting the account walks the email check, the fresh
    sign-in, a failure and Keycloak's leftover, lands on Welcome with no
    return address, leaves no snapshot or statistics behind, and the
    account can no longer log in.

## 5. Timing

- `fullyParallel: true`, four workers locally and in CI, one test per file
  so every scenario is its own worker slot.
- Per test timeout 90 s; expect timeout 10 s; the suite target is under
  five minutes per shard in CI with four workers (ADR-0080); CI splits the suite
  into two shards (`--shard=N/2`), each with its own compose stack. A scenario that cannot fit splits, it never widens the
  timeout.
- Retries: 0 locally, 1 in CI; a retry is a flake to fix, reported in the
  HTML report.
- Scenarios 10, 11, 14, 16, 18, 34 and 35 are the heaviest (two or more contexts);
  01, 12, 13, 15, 25, 27, 28, 29, 31 and 32 are the heaviest single-context ones. A scenario
  that runs past a minute at two workers on a two-core machine splits;
  data a step only needs to exist is arranged through `anotherDevice`,
  not clicked through the UI.

## 6. CI

An `e2e` job in `.github/workflows/ci.yml`, after `build-and-unit-test`, runs as
a matrix of two shards; each shard does the following on its own runner and
stack:

1. `docker compose -f docker/compose.yaml --env-file docker/.env.example -p
   pspad-e2e up -d --build --wait`.
2. `npm ci` and `npx playwright install --with-deps chromium` in
   `test/e2e`.
3. `npm test -- --shard=N/2`.
4. On failure upload `test/e2e/playwright-report` and the traces (`trace:
   on-first-retry`) as `playwright-report-N`, and print the stack's logs.
5. Always `docker compose -p pspad-e2e down -v`.

`deploy` adds `e2e` to its `needs` and so waits for both shards. Dependabot gets an `npm` entry for
`/test/e2e`, weekly, grouped minor and patch, like the others.

## 7. Layout

```
test/e2e/
  package.json  package-lock.json  tsconfig.json  playwright.config.ts  env.ts  global-setup.ts
  fixtures/      keycloak.ts (admin API)  accounts.ts (account fixtures)
  helpers/       sign-in.ts  nav.ts  panels.ts  areas.ts  lists.ts  tasks.ts  inbox.ts  today.ts  dates.ts
                 settings.ts  goals.ts  references.ts  sharing.ts  snapshots.ts  sync.ts  api.ts  budgets.ts
  scenarios/     01-first-sign-in.spec.ts … 36-account-settings-and-deletion.spec.ts
  locator-allowlist.txt  (class locators the guard lets through, with reasons)
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
locate by role, label and text (§3.3), so renaming a label, a heading or an
accessible name is a suite change too. Nav links carry a count once
the user has items, so `navLink` finds them by `href`; a link found by its
name alone misses when the count appears and a `goto` fallback reloads the
page, which is a different flow from clicking.

Since live updates (ADR-0076) every command a device sends echoes back as a
nudge whose pull re-renders the open panel. A helper may retry locating,
opening or waiting inside `toPass` (closing a panel, opening a menu or an
editor); it never retries a user's edit or a command: the edit happens once
and the step then waits for its outcome, so a lost edit fails the step
instead of being typed again.

UI test-driven work starts with the failing end-to-end step; pages, layout and components have no bUnit tests (ADR-0080).

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
  while it is shut, so the helpers wait for the panel's Close button to be
  gone (Playwright's click then waits out the fading overlay) and for a FAB
  item to take a trial click, instead of visibility. A MudBlazor select's
  popover also lingers while it closes, so a helper waits for no `option`
  before opening the next.
