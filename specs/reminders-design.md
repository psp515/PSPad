# Reminders Design

A task can notify its people on a phone or desktop, a set number of days
before its due day, at a chosen time, through Web Push, even when the app is
closed. Decision: `adr/0092`. Builds on `specs/recurrence-lead-time-design.md`
(the existing look-ahead row, renamed here) and `specs/sharing-spec.md`.

## 1. Scope

In:

- One optional reminder per task, off by default: "{N} days before (0 = on
  the day) at {HH:mm}", read in each recipient's stored time zone.
- One-time tasks with a due date; repeating tasks, before each pending
  occurrence.
- On a shared list, every member and the owner are notified; any of them may
  change or clear the reminder.
- Per-device opt-in from Settings, a test push, opening the task from the
  notification.
- A server-side scheduler and VAPID keys inside the existing `api`
  container.

Out:

- Reminders without a due date or repeat, on steps or step deadlines.
- More than one reminder per task, snooze, actions on the notification
  ("Done").
- Daily digests, motivation messages, e-mail.
- Per-person reminders on shared tasks.

## 2. Domain (`PSPad.Module.Tasks/Tasks`)

### 2.1 Value

```csharp
public sealed record TaskReminder(int DaysBefore, TimeOnly At)
```

- `DaysBefore` 0–99; otherwise "A reminder must be between 0 and 99 days
  before." `At` is a wall-clock time without a zone.
- `TaskReminder.Of(daysBefore, at)` validates, as `LeadTime.Of` does.

### 2.2 Fields

`TodoTask` gains `Reminder: TaskReminder?` (null = off, the default) and
`ReminderSetAt: DateTimeOffset?`, the moment it was last set or changed.

### 2.3 Command

Folder `Tasks/SetReminder/`.

**`SetTaskReminder(CommandId, UserId, TaskId, TaskReminder? Reminder)`**

- Rejects a missing or deleted task ("That task does not exist.") and an
  actor who isn't the owner or a member of the task's list, as every task
  command does.
- Rejects an invalid reminder (§2.1).
- Equal to the current reminder: emits nothing.
- Otherwise `TaskReminderSet(TaskId, UserId, At, Reminder?)`; the fold sets
  `Reminder` and `ReminderSetAt = At`. `null` clears.
- Shared command: works offline, syncs, last write wins per task (AD-5).
- Setting a reminder on a task with no due date and no repeat is allowed; it
  stays dormant (§2.4).

### 2.4 When a reminder fires

For one occurrence day `D` and one recipient with time zone `Z`, the moment
is `(D − DaysBefore) at At` in `Z`, converted to UTC.

| Task | Fires |
|------|-------|
| One-time with a due date | once, for the due date |
| Repeating | for every occurrence the rule yields, if that occurrence is not done |
| Done (or the occurrence done) before the moment | never |
| No due date and no repeat | never; the reminder is kept and called dormant |
| Deleted, or its list deleted | never |
| Moment earlier than `ReminderSetAt` | never (no burst on setting a past moment) |

- An overdue one-time task gets its one reminder and nothing more.
- Daylight saving: a local time the spring change skips fires at the first
  valid minute after it (02:30 → 03:00); a time the autumn change repeats
  fires at its first occurrence.
- `ReminderRule.MomentsBetween(TodoTask task, TimeZoneInfo zone,
  DateTimeOffset from, DateTimeOffset to)` (in `Today/`, beside `TodayRule`,
  pure) returns `(DateOnly Occurrence, DateTimeOffset Moment)` pairs with
  `from < Moment <= to`. It is the only place the rule lives.

### 2.5 The existing look-ahead row

`LeadTime` and `SetTaskLeadTime` are unchanged. Its row's label changes from
"Remind me" to **"Show ahead"** (icon `Visibility`); "Remind me" is the new
reminder row (§4.1).

## 3. Scheduler and delivery (`PSPad.Module.Reminders`, server only)

### 3.1 Module

New module `src/modules/PSPad.Module.Reminders`, referenced by `Api` only.
It references `Abstractions`, `Tasks` (tasks, lists, `ReminderRule`) and
`Identity` (`User.TimeZone`, `User.Language`). `App` never sees it.

Like Sharing and Statistics, the module is pure (no MongoDB, ASP.NET Core,
`System.Net.Http`, Hosting or Infrastructure; an architecture test guards
it): it holds the tick's logic, the documents, the texts and its ports
(`IReminderSource`, `IReminderClaims`, `IPushSubscriptionStore`,
`IPushSender`, `IVapidKeyStore`). `src/PSPad.Api/Reminders/` holds the
adapters: the `BackgroundService`, the Mongo stores, the Web Push sender and
the endpoints; indexes go in `MongoIndexes`.

### 3.2 Collections

| Collection | Document | Indexes |
|------------|----------|---------|
| `pushsubscriptions` | `{Id, UserId, Endpoint, P256dh, Auth, DeviceLabel, CreatedAt, LastSuccessAt?}` | unique `Endpoint`; `UserId` |
| `sentreminders` | `{TaskId, Occurrence, UserId, ClaimedAt, Attempts, State}` (`State`: `Sent`, `Retry`, `Failed`) | unique `(TaskId, Occurrence, UserId)`; `ClaimedAt` |
| `vapidkeys` | one document, fixed id: `{PublicKey, PrivateKey, CreatedAt}` | — |

None of them syncs to the client or appears in the replica. The `todotasks`
collection gains a partial index on `reminder` (filter `$type: object`; tasks store `reminder: null`, so a sparse index would narrow nothing).

### 3.3 The tick

`ReminderScheduler` (Api), a `BackgroundService` on a 60-second
`PeriodicTimer`, runs `ReminderTick.RunAsync(from, now)` (module) in a
scope, with `now` from `IClock`. An exception in a tick is logged and the
loop goes on; it never stops the host.

1. Window `(from, now]`: `from` is the previous tick's `now`; on the first
   tick after start, `now − 1 h`.
2. Load live tasks with `Reminder != null` and their lists.
3. Recipients: the task's list owner (`TaskList.UserId`) and its members;
   every task belongs to a list. Load each recipient `User` once per tick.
4. `ReminderRule.MomentsBetween` per task and recipient.
5. Claim: insert `{TaskId, Occurrence, UserId, State = Sent, Attempts = 1}`
   into `sentreminders`. A duplicate key means another tick or API instance
   owns it: skip. No lease or lock.
6. Send to each of the recipient's subscriptions (§3.4).
7. Retries: claims in `Retry` with `ClaimedAt` within the last hour are sent
   again, `Attempts + 1`; after 3 attempts the state becomes `Failed`.
8. Delete `sentreminders` older than 7 days.
9. Log one line: sent, skipped, retried, failed. Never log a payload.

Nothing is held in memory between ticks except the VAPID key pair.

### 3.4 Sending

- Payload (JSON, encrypted per device, RFC 8291): `title` = the task name;
  `body` = "Due today" / "Due tomorrow" / "Due in {N} days" in the
  recipient's language (`adr/0075`; a resx in the Reminders module read
  through `ResourceManager` for `User.Language`, the first server-side
  text that is localized); `url` = `{Reminders:AppBaseAddress}/?task={TaskId}`;
  `tag` = the task id.
- VAPID JWT (RFC 8292) signed with the stored private key; `sub` =
  `Reminders:Contact`, or `Reminders:AppBaseAddress` when empty and it is
  `https`; otherwise the JWT has no `sub` (Apple may refuse such pushes, so
  the install guide asks for `REMINDERS_CONTACT`).
- Configuration (`ReminderOptions`, section `Reminders`): `AppBaseAddress`
  (compose passes `${APP_BASE_ADDRESS}`) and `Contact` (compose passes
  `${REMINDERS_CONTACT:-}`).
- Headers: `TTL` = seconds until the end of the occurrence day in the
  recipient's zone, at most 86400, at least 60; `Topic` = the task id
  without dashes; `Urgency: normal`.
- Push service answers:
  - `2xx`: done; the subscription's `LastSuccessAt` is set.
  - `404`, `410`: the subscription is deleted.
  - `429`, `5xx`, timeout: the claim becomes `Retry` (§3.3 step 7); a
    retry sends only to the user's subscriptions with no `LastSuccessAt` at
    or after the claim's `ClaimedAt`.
  - other `4xx`: `Failed`, logged with the status code.
- Library: `Lib.Net.Http.WebPush` 3.3.1 (MIT), used only by the Api
  adapter.

### 3.5 VAPID keys

On start, if `vapidkeys` has no document, the API generates a P-256 key pair
and inserts it under the fixed id. A duplicate key (another instance won)
means it reads the stored pair. The pair is loaded once and kept in memory.
Losing the document invalidates every subscription; devices must turn
notifications on again.

### 3.6 HTTP

All require the signed-in user.

| Method and path | Does |
|-----------------|------|
| `GET /api/push/key` | the VAPID public key (base64url) |
| `PUT /api/push/subscriptions` | upsert by `Endpoint` for the caller; an endpoint owned by another user is moved to the caller |
| `DELETE /api/push/subscriptions` | body `{Endpoint}`; deletes it if the caller owns it |
| `POST /api/push/test` | body `{Endpoint}`; pushes "PSPad notifications work" to that subscription of the caller only |

A subscription's endpoint must be an absolute `https` URL with a DNS host
name (no IP literal), at most 2048 characters. When sending, the push
client refuses to connect to loopback, private, link-local, unique-local or
unspecified addresses after DNS resolution, so a stored endpoint can't make
the server call an internal service.

Account deletion removes the user's `pushsubscriptions` and `sentreminders`.

### 3.7 Service worker

`service-worker.js` and `service-worker.published.js`:

- `push`: `showNotification(title, { body, tag, data: { url }, icon })`.
- `notificationclick`: close; focus an open PSPad window and navigate it to
  `url`, or open a new window at `url`.

## 4. Screens (`PSPad.App`)

### 4.1 Remind me row (`Components/ReminderRow.razor`)

A `PropertyRow` (`pspad-task-reminder`, icon `NotificationsNone`, label
"Remind me") after Due/Until, in the Add and Edit task panels.

- Off: "Off" in the muted colour, no ✕.
- Set: "On the day at 09:00", "1 day before at 18:00", "{N} days before at
  {HH:mm}"; a ✕ sends `SetTaskReminder(null)`.
- Dormant: the value struck through and "Needs a due date".
- Repeating task: caption "Before each occurrence".
- Tap opens a `MudMenu`: On the day, 1 day before, 2 days before, 1 week
  before (7 days), each at 09:00, then Custom…. A task with a `Time` uses one
  hour before its start for the "On the day" preset (clamped at 00:00).
- Custom… opens a `MudDialog`: `MudNumericField` 0–99 ("days before", 0
  reads "On the day") and `MudTimePicker` (24-hour by culture); Save disabled
  until valid.
- Add panel: the draft's reminder is sent as `SetTaskReminder` after the due
  date, as the lead time is.
- When this device has notifications off, the row shows the caption
  "Notifications are off on this device · Turn on", linking to the Settings
  card. The reminder still reaches the user's other devices.

### 4.2 Show ahead row

`LeadTimeRow`: label "Show ahead", icon `Visibility`; presets, dialog and
texts otherwise unchanged.

### 4.3 Settings: Notifications card

A `MudPaper` card (`pspad-notifications`) between Application settings and
Sync. A small JS module (`wwwroot/js/push.js`) wraps `PushManager` and
`Notification.permission`; `State/PushState` picks the card's state.

| State | Shows |
|-------|-------|
| Unsupported | "This browser can't show notifications." On iOS outside the Home Screen: "Add PSPad to your Home Screen to get reminders." |
| Off | "Reminders are sent to the devices you turn on here." and **Turn on for this device** |
| Denied | "Notifications are blocked for PSPad in your browser settings." |
| On | "On for this device", **Send test**, **Turn off** |
| Offline | buttons disabled, "Needs a connection" |

- Turn on: in the same tap, request permission, `subscribe` with the key
  from `GET /api/push/key` (`userVisibleOnly: true`), then
  `PUT /api/push/subscriptions`.
- Turn off: `unsubscribe`, then `DELETE /api/push/subscriptions`.
- Sign-out on a device turns it off first when online.

### 4.4 Opening from a notification

`?task={id}` opens the task panel through the existing route; a task that is
gone shows the panel's existing not-found handling.

### 4.5 Strings

English and Polish: `TasksStrings` (rows), `SettingsStrings` (card), and the
Reminders module's resx (notification texts).

## 5. Self-hosting and docs

- No new container, port or required variable.
- `docker/.env.example`: `REMINDERS_CONTACT=` with a `#` description
  (contact sent to push services; empty uses `APP_BASE_ADDRESS`); the
  outbound-HTTPS comment lists `fcm.googleapis.com`, `*.push.apple.com` and
  `updates.push.services.mozilla.com`; `docker/compose.yaml` passes it to
  `api`.
- `docs/src/pages/install.astro`, a "Reminders" section: HTTPS on the client
  origin (except localhost); iPhone needs the Home Screen app, iOS 16.4+;
  keys live in Mongo, so a restored backup keeps devices subscribed and a new
  database needs notifications turned on again; delivery is best effort.
- `docs/src/data/features.ts`: the Reminders card becomes built ("Push
  reminders for tasks, a set number of days before, at your hour"); the
  landing page mentions it.
- `AGENTS.md`: §1 drops reminders from "Later"; §3 a row "Reminders, Web
  Push" → this spec; §4 AD-1 lists Reminders with `adr/0092`; §5 adds the
  module and its edges `Reminders`→`Tasks`, `Reminders`→`Identity`.
- `specs/backend-spec.md` (the `todotasks` fields, the new collections, the
  hosted service), `specs/modules-spec.md` (the edges), `specs/ui-spec.md`
  (the two rows, the Settings card), `specs/recurrence-lead-time-design.md`
  (a note that its row is now "Show ahead").

## 6. Testing

- Unit (`PSPad.Module.Tasks.Tests`): `TaskReminder` bounds; `Decide(
  SetTaskReminder)` sets, clears, no-op when equal, rejects 100, a missing
  task and a non-member; `ReminderRule.MomentsBetween`: one-time on the day
  and N days before, repeating with only pending occurrences, done before
  the moment, dormant, nothing before `ReminderSetAt`, two recipients in
  different zones, the skipped and the repeated DST hour, a window across
  midnight.
- Unit (`PSPad.Module.Reminders.Tests`): payload texts EN and PL, TTL, the
  answer-to-action mapping.
- App unit (`PSPad.App.Tests/State`): the reminder row text per state;
  `PushState` from support, permission and subscription. No component is
  rendered (`specs/testing-spec.md`).
- Integration (`PSPad.Api.Tests`, Testcontainers MongoDB, fake
  `TimeProvider`, an in-test fake push endpoint with real test keys): one
  POST at the moment, its VAPID JWT verifying against the stored key, its
  body decrypting to the payload, its TTL and Topic; two ticks or two
  schedulers over one window send once; `410` deletes the subscription;
  `503` retried and failed after 3 attempts; a restart catches up a moment
  30 minutes old but not 2 hours old; done before the moment sends nothing;
  a shared list sends to owner and member; the key generated once with two
  instances starting together; the subscription endpoints, including that a
  user can't delete another user's subscription.
- E2E (`test/e2e/scenarios/40-reminders.spec.ts`): set a reminder by preset
  and by Custom and see it after reload; clear it; dormant after removing
  the due date; the "Show ahead" label; the Settings card when permission is
  denied. The rename changes the existing scenarios 06, 23 and 24 and
  `helpers/today.ts` (`setCustomLeadTime`) from "Remind me" to "Show
  ahead". Subscribing for real is not covered: headless Chromium in the
  container can't reach FCM; the integration tests cover the send path.
