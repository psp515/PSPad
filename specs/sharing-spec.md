# Sharing — design

Status: Built. Issues: #103 (share a list with people who have an
account), #104 (public read-only snapshot for people who do not). Decisions
of record: ADR-0054, ADR-0055, ADR-0056 (§9). Live updates for the owner's
chips — a push rather than next sync — are issue #105, a separate spec.

Two features, one spec, because both start from the same list panel and both
end in the same drawer:

1. **Member sharing (#103).** The owner turns on an invite link. Any signed-in
   person who opens it becomes a member: the list syncs to them, works
   offline, and they edit its content as freely as the owner. The owner
   rotates the link, stops it, or removes a member at any time; a member can
   leave.
2. **Public snapshots (#104).** The owner publishes a frozen copy of a list
   under a link that anyone can open without an account, until an expiry the
   owner picks. Visitors cannot change the content, but they can tick a task,
   step or reference item as done; the tick shows to every visitor and reaches
   the owner as a chip on the real task — never as a completion. A signed-in
   visitor finds every snapshot they opened under **List snapshots** in the
   drawer.

## 1. Scope

In:

- Invite link per list (`Tasks` and `Reference` kinds), reusable, rotatable.
- Members: create, edit, tick, reorder, delete tasks, steps and reference
  items in the list. Owner-only: rename, delete, move to area, sharing,
  goals.
- "Shared with me" virtual area; members may file a shared list into one of
  their own areas instead.
- Shared tasks on the member's Today, in the member's time zone.
- Statistics: owner and actor each get a record.
- Public snapshots: frozen at creation, preset or custom expiry, several per
  list, revocable early.
- Snapshot marks: anonymous visitors tick or untick tasks, steps and
  reference items; the owner's task or item gets a "marked on a snapshot"
  chip through ordinary sync.
- List snapshots drawer tab for signed-in visitors.

Out:

- Sharing an area, a goal, or the Inbox.
- Approving joiners; per-member permissions; invites to a named account.
- Live public pages; editing a snapshot's content.
- Live push of changes (snapshot marks, member edits) to open clients — its
  own spec, `specs/live-updates-design.md`. Until then changes arrive on the
  next sync (poll, app start, reconnect, command).
- Who marked: marks are anonymous.
- Per-person completion of a shared recurring task (one `completedDays` for
  everyone).
- Notifications when someone joins or edits.

## 2. Domain — `PSPad.Module.Tasks`

### 2.1 `TaskList` sharing state

`TaskList` gains:

- `InviteToken` — `string?`, URL-safe, ≥128 bits of randomness, generated on
  the client. `null` = not shared by link. Members see it too and may forward
  it; that is the "anyone with the link" model, not a leak.
- `OwnerName` — the owner's display name, set by `ShareTaskList`, so members
  can show "Shared by …" without the owner's `User` document.
- `Members[]` — `ListMember(UserId, DisplayName, JoinedAt)`. `DisplayName` is
  taken from the joiner's token at join and not kept in step with later
  renames.
- `UserId` stays the owner, always.

`IsShared` = `Members` non-empty. A list with no token and no members is an
ordinary list.

### 2.2 Commands

| Command | Who | Offline | Effect / rejections |
|---|---|---|---|
| `ShareTaskList(listId, token, ownerName)` | owner | yes | Sets or rotates `InviteToken`. Rejects a blank or short token. Rotating never removes members |
| `StopSharingTaskList(listId)` | owner | yes | Clears `InviteToken`. Members stay. Emits nothing when already clear |
| `RemoveListMember(listId, memberId)` | owner | yes | Rejects a non-member |
| `LeaveTaskList(listId)` | member | yes | Rejects the owner and non-members |
| `JoinTaskList(listId, token, displayName)` | signed in | **no** | Server-only, from `POST /api/lists/join`. Rejects a deleted list, a wrong or cleared token. Owner or existing member: accepted, no event |

Events: `TaskListShared`, `TaskListSharingStopped`, `ListMemberRemoved`,
`TaskListLeft`, `TaskListJoined`.

### 2.3 Access rule

`ListAccess.Require(list, actorId)` accepts the owner or a member and rejects
everyone else; `TaskList.Require` (owner only) stays for list-level commands.

- **Owner or member** — every command on tasks, steps, recurrence,
  descriptions, stars, priorities, due dates, completion, occurrences, and on
  reference items and their fields; `CreateTask`, `CreateReferenceItem`,
  `OrganiseInboxItem` into the list. A handler that does not load the parent
  list today starts loading it.
- **Owner only** — `RenameTaskList`, `DeleteTaskList`, `MoveTaskListToArea`,
  the four sharing commands above, and `LinkTaskToGoal`.

### 2.4 Owner and actor

`DomainEvent` gains `ActorId` (defaults to `UserId`). Every event's `UserId`
is the **aggregate's owner**; `ActorId` is the command's `UserId`. Creation
events take the owner from the target list, so a member's `CreateTask` makes
a task owned by the list's owner, and `When(TaskCreated)` keeps setting
`UserId` from the event. Every `Decide` that today stamps events with
`command.UserId` changes to the aggregate's or the list's owner.

### 2.5 Moves across owners

`MoveTaskToList` and `MoveReferenceItemToList` reject a target list whose
owner differs from the source list's. `OrganiseInboxItem` into a list the
actor is a member of is allowed — the Inbox item becomes a task owned by the
list's owner; the Inbox stays the actor's.

### 2.6 Snapshot marks

`TodoTask` gains `SnapshotMarks[]` — `SnapshotMark(SnapshotId, StepId?,
MarkedAt)`, `StepId` null for the task itself. `ReferenceItem` gains
`SnapshotMarks[]` — `SnapshotMark(SnapshotId, null, MarkedAt)`. A mark is a
note for the owner, not a state change: it never completes, checks or ticks
an occurrence, never enters the Today rule, and Statistics ignores its events.

| Command | Who | Offline | Effect / rejections |
|---|---|---|---|
| `MarkTaskFromSnapshot(taskId, stepId?, snapshotId, marked)` | Sharing, as owner | **no** | Server-only; `/api/commands` rejects it. Adds or removes one mark. Rejects a deleted task or unknown step; no event when unchanged |
| `MarkReferenceItemFromSnapshot(itemId, snapshotId, marked)` | Sharing, as owner | **no** | Same, for reference items |
| `ClearTaskSnapshotMarks(taskId)` | owner or member | yes | Dismisses every chip on the task, steps included; no event when it carries none |
| `ClearReferenceItemSnapshotMarks(itemId)` | owner or member | yes | Same, for a reference item |

Events: `TaskSnapshotMarkSet`, `TaskSnapshotMarksCleared`,
`ReferenceItemSnapshotMarkSet`, `ReferenceItemSnapshotMarksCleared`.
`ActorId` = `UserId` (the owner); there is no account behind an anonymous
visitor.

Sharing drives Tasks only through these commands, the way integrations do
(`adr/0045`) — Tasks never learns Sharing exists. Clearing a chip touches only
the task; the snapshot keeps its tick for visitors.

### 2.7 Cascades

Unchanged (`adr/0042`). The owner deleting a list or its area deletes it for
everyone. A member deleting one of *their* areas never touches a shared list
they filed there — filing is a `ListView`, not `TaskList.AreaId` (§4).

## 3. Sync and offline

### 3.1 What a member pulls

`SyncReader` widens two filters; everything else keeps `userId == caller`:

- `tasklists`: `userId == caller` **or** `_members.userId == caller` (the private `_members` field keeps its name in BSON, like `_steps`).
- `todotasks`, `referenceitems`: `userId == caller` **or**
  `listId ∈ memberLists(caller)` **or** `previousListId ∈ memberLists(caller)`.
  `PreviousListId` is the list a task or item last left (set by
  `TaskMovedToList` / `ReferenceItemMovedToList`), so a row the owner moves
  out of a shared list reaches members once more, naming a list they do not
  hold; the client then drops it (§3.2). A row matched only through
  `previousListId` (caller neither owns it nor belongs to its current list)
  is sent as a stub — `id`, `userId`, `listId`, `previousListId`, `version`,
  `deleted`, `seq` and nothing else — so none of its later content reaches
  former members.

`events` in the response stay `userId == caller`; the client does not read
them.

New indexes: `tasklists {_members.userId: 1, seq: 1}`,
`todotasks`/`referenceitems` `{listId: 1, seq: 1}` and
`{previousListId: 1, seq: 1}`.

### 3.2 The membership set

Every sync response carries `memberListIds` — the lists the caller belongs to
and does not own. The client reconciles after writing the delta, but only
when the replica has a recorded owner — a device holding nothing yet for
this user would otherwise see every list as foreign and purge it:

- A list in the replica, not owned by the caller, not in `memberListIds` —
  purge it and its tasks and reference items. One rule for leaving, removal,
  the owner deleting the list, and the owner deleting their account (a raw
  wipe that leaves no tombstones).
- A list in `memberListIds` missing from the replica (another device of the
  same user joined) — the same pull asks again with `full=<listIds>` at the
  marker this pull just received (before it is written); the server returns those lists and all their live
  children regardless of `since`. A failed full pull
  (`HttpRequestException`) is swallowed rather than written to the marker —
  the list stays missing and the next sync's reconciliation retries it,
  instead of a flaky connection wedging every later delta pull behind it.
- A task or reference item not owned by the caller whose `ListId` names a
  list the replica does not hold — drop it. That is a row the owner moved
  out of a shared list into one the caller cannot see.

### 3.3 Join

`POST /api/lists/join {token}` — resolves the token to a list, runs
`JoinTaskList` through the normal pipeline server-side, and returns the list
and its live children. The joining device writes them into its replica at
once.

The client route `/join/{token}` requires sign-in (returning to the same URL
after it), calls the endpoint, and navigates to the list. Offline it shows
"Joining needs a connection" and a retry. A dead token shows "This invite
link no longer works".

### 3.4 Losing access with commands queued

A member removed while offline ships commands that `ListAccess` rejects; they
surface as rejections (AD-5) and the purge follows on the same pull.
Concurrent edits to one aggregate by two people keep the existing
per-aggregate version check: last write wins, the loser is told.

### 3.5 New collection

`listviews` (§4) is a new sync collection; `adr/0049`'s fingerprint takes
existing replicas through a one-time pull from zero. Membership adds no
collection.

## 4. Placement and Today

### 4.1 "Shared with me"

A virtual area: a client-side constant, not an `Area` aggregate, so nothing
can rename, delete or reorder it. Shown — last — in the sidebar's areas, the
phone's area chips and the areas index whenever at least one member list has
no placement. Its board is the usual `MasonryGrid` of those lists.

### 4.2 `ListView` — `PSPad.Module.Presentation`

The extension point `specs/modules-spec.md` names, built like `AreaView`:

- One per `(user, list)`, id `ListView.IdFor(userId, listId)`, collection
  `listviews`. Holds `AreaId?` — where this user files the list.
- `PlaceList(listId, areaId?)` creates or updates it; `null` returns the list
  to "Shared with me". Emits nothing when unchanged; rejects another user's
  view.
- Presentation never reads `TaskList`: the client offers placement only on
  member lists, and a view on a list the user owns is ignored when grouping.
  A view naming a deleted or unknown area falls back to "Shared with me".
- A filed list joins that area's `AreaView` order through `ReorderLists` and
  `Arranged.Sort`, like any other list.
- Leaving or losing a list orphans its view; account deletion sweeps it.

Theming fields stay out until list theming is built.

### 4.3 Owner's side

Shared lists stay in the owner's area with a shared marker and member count
on the card and in the sidebar.

### 4.4 Today

The client projection and `GET /api/today` include tasks from member lists,
same Today rule, the **viewer's** time zone. Star, priority and
`completedDays` are on the task, so they are shared: ticking today's
occurrence of a shared recurring task ticks it for every member.

## 5. Statistics

### 5.1 Two records per shared event

`ActorId == UserId` — one record, as today. Otherwise one event writes two
records: one under the owner's `userId`, one under the actor's. Records gain
`role: Owner | Actor`.

### 5.2 Record identity

`statistics_records` changes `_id` from the event `seq` to `"{seq}:{userId}"`
and gains a `seq` field. The feed pages by `seq`; index `{userId: 1, seq: -1}`
replaces `{userId: 1, _id: -1}`. `statistics_inbox_records` is unchanged — the
Inbox is never shared, so it never gets an actor copy or a composite id.

### 5.3 Rebuild

`statistics_state` gains `projectionVersion`. At startup, a version older than
the code's drops the `statistics_*` collections, then (only once the drop
succeeded) resets `lastProcessedSeq` to 0 and records the version, and replays the whole log through the existing replay path (`adr/0036`). No
migration code; the log is the source.

### 5.4 What the actor's record carries

- `goalId` null — goals are the owner's.
- Labels for the owner's list resolve by aggregate id, not `userId`, so the
  member's feed names the list.
- **Outstanding** counts `Owner` records only; a member completing the
  owner's tasks must not drive their own line negative.
- Completions, opened, the heatmap, by-goal (owner side) and the feed count
  both.

## 6. Public snapshots — `PSPad.Module.Sharing`

### 6.1 Module

New, server-side module `PSPad.Module.Sharing`, referencing `Abstractions`
and `Tasks` — the second module-to-module edge, same direction and reason as
Statistics' (it reads Tasks' aggregate shapes; a rename must break the
build). Guards: `Tasks` never references `Sharing`; `Sharing` never
references `Statistics`, `Presentation`, `Identity` or `Contracts`.

Owns `ListSnapshot`, `SnapshotBuilder.Build(list, tasks, items, now,
expiresAt)` (pure) and `SnapshotVisit`. Not commands, not in the event log, not
synced: a public link is useless until it reaches someone, so creation is
online-only HTTP, like `PUT /api/me/timezone`.

### 6.2 `list_snapshots`

`_id` (GUID), `token` (unique index, ≥128-bit URL-safe, distinct from the
id), `userId` (owner), `listId`, `kind`, `name`, `createdAt`, `expiresAt`,
`tasks[]` (`id`, `name`, `done`, `dueOn`, `priority`, `starred`,
`description`, `marked`, `markedAt`, `steps[]` (`id`, `name`, `done`,
`marked`, `markedAt`)) or `items[]` (`id`, `name`, `description`, `starred`,
`marked`, `markedAt`, `fields[]` (`label`, `value`, `display`)), in display
order. `id` is the source task, step or item id, so a mark can find it.
Content is frozen; only `marked`/`markedAt` change after creation.

Excluded: goals, recurrence rules, area, members, anything outside the list.
A repeating task shows as open with its name and description.

TTL index on `expiresAt` (`expireAfterSeconds: 0`); reads also check
`expiresAt > now`, since TTL deletion lags.

### 6.3 Who and how long

Owner only. Expiry presets 1 day, 7 days, 30 days, or a date picked up to one
year ahead; the server rejects anything outside (now, now + 365 days].
Snapshots are taken from **server** state, so the client flushes its outbox
first and the action is disabled offline.
Deleting the list does not revoke its snapshots: each keeps serving its
frozen content until it expires or is revoked (`adr/0056`).

### 6.4 Marks

`POST /api/public/snapshots/{token}/marks` with `{entryId, stepId?, marked}`,
anonymous, rate-limited like the read. Any visitor may tick or untick any
entry; the token is the guard and the snapshot expires.

1. Sharing sets `marked`/`markedAt` on the snapshot entry and saves. Unknown
   entry or expired snapshot → 404; unchanged → 204, nothing further.
2. It then runs `MarkTaskFromSnapshot` or `MarkReferenceItemFromSnapshot`
   through the server pipeline with the owner as `UserId` and a fresh
   `CommandId`. A rejection (the task was deleted since) is logged and
   ignored — the snapshot's tick stands.

The owner's devices see the chip on their next sync; no push (live updates
are their own spec).

### 6.5 `snapshot_visits`

`_id` = `"{userId}:{snapshotId}"`, `userId`, `snapshotId`, `token`, `name`,
`expiresAt`, `visitedAt`. TTL on `expiresAt`. Written when a signed-in user
opens a snapshot (including their own).

### 6.6 Public page

Client route `/public/snapshot/{token}`, anonymous, outside `AppShell` like `/welcome`:
the list name, "Snapshot from {date} · expires {date}", tasks (done ones
struck through, steps beneath) or reference items (fields as in
`ReferenceRow`), Markdown via `MarkdownField` `ReadOnly`. Every task, step and
item has a checkbox for its mark — distinct from "done" (which is frozen
owner state): a marked entry shows a "Marked" chip. Toggling is optimistic
and reverts with a snackbar on failure; offline the checkboxes are disabled. Expired or unknown
token: one "This snapshot has expired or never existed" screen — the two are
deliberately indistinguishable. If a local session exists, the page also
records the visit and caches the snapshot in IndexedDB (`snapshots` store,
not a sync collection) for offline reading.

### 6.7 List snapshots tab

A sidebar row (desktop, after Statistics) and an `AccountDrawer` row (phone,
before Settings): **List snapshots**, route `/snapshots`. Lists
visits newest first with name and expiry; opens `/public/snapshot/{token}`. Online it
fetches the list; offline it shows the cached ones. Expired entries drop out
on both sides.

## 7. UI

All in the list's side panel (`ListDetailPanel`, `adr/0052`), new
**Sharing** section:

Owner:

- Invite link: a switch, off by default. Turning it on runs `ShareTaskList`
  and shows the URL `{origin}/join/{token}` with Copy and **New link**
  (rotate, confirm); turning it off runs `StopSharingTaskList` — the switch
  is the "stop link" control, there is no separate button.
- Members: name and joined date, Remove (confirm popup).
- Public snapshots: **Publish snapshot** with an expiry picker (chips 1 d /
  7 d / 30 d / Date); active snapshots with created/expiry, Copy and Revoke.

Member:

- "Shared by {OwnerName}", the member list read-only, the invite link with
  Copy, **Leave list** (confirm).
- Rename, delete, move-to-area and the Sharing controls hidden; **File under**
  an area picker (`PlaceList`) shown instead.
- Task panel hides the goal field. Move-to-list pickers offer only lists with
  the same owner.

Both roles:

- A task, step or reference item with snapshot marks shows a "Marked on a
  snapshot" chip in its row and panel; the panel offers **Dismiss**
  (`ClearTaskSnapshotMarks` / `ClearReferenceItemSnapshotMarks`). Completing
  the task is a separate, deliberate tap.

## 8. HTTP surface

| Method | Path | Auth | Purpose |
|---|---|---|---|
| `POST` | `/api/lists/join` | user | Join by token; returns list and children |
| `GET` | `/api/sync?since=&full=` | user | `full` = list ids to return whole; response adds `memberListIds` |
| `POST` | `/api/lists/{id}/snapshots` | owner | `{expiresAt}` → `{id, token, expiresAt}` |
| `GET` | `/api/lists/{id}/snapshots` | owner | Active snapshots of the list |
| `DELETE` | `/api/snapshots/{id}` | owner | Revoke |
| `GET` | `/api/public/snapshots/{token}` | **anonymous** | The snapshot, or 404 when unknown or expired |
| `POST` | `/api/public/snapshots/{token}/marks` | **anonymous** | `{entryId, stepId?, marked}` — tick or untick (§6.4) |
| `POST` | `/api/me/snapshot-visits` | user | `{token}` — record a visit |
| `GET` | `/api/me/snapshot-visits` | user | Visits, newest first, unexpired |

The anonymous endpoints get a fixed-window rate limit per IP; token entropy is
the real guard.

**Account deletion** (`adr/0034`) extends its sweep: `list_snapshots`,
`snapshot_visits` and `listviews` go by `userId`; then the caller is `$pull`ed
from every other owner's `tasklists.members`, each touched list stamped with
a fresh `seq` from `counters` so owners' replicas see it. Owners' lists vanish
for members through §3.2.

No new service, port or secret. `Sharing:PublicRequestsPerMinute` is a
configuration key with a code default (60), there for tests, not a
documented self-hoster setting. One self-hoster variable did come with the
final review: `API_BEHIND_PROXY` (maps to
`ASPNETCORE_FORWARDEDHEADERS_ENABLED`) so the public rate limit sees each
visitor's address behind a reverse proxy; `.env.example` and the install
docs cover it.

## 9. Docs and records

- **ADR-0054** — Lists are shared by membership on `TaskList`: owner/actor
  split on events, `ListAccess`, membership-set sync with `full=` backfill,
  `ListView` placement. Rejected: a `Sharing`-owned `ListShare` behind an
  access port; per-document reader lists. Extends `adr/0051`; amends
  `adr/0034` (sweep) and `adr/0042` (cross-owner moves).
- **ADR-0055** — Statistics records per owner and actor, `"{seq}:{userId}"`
  ids, versioned rebuild. Amends `adr/0037`.
- **ADR-0056** — Public snapshots: frozen copies in `PSPad.Module.Sharing`,
  online-only HTTP, TTL expiry, the second module edge; anonymous marks reach
  Tasks as server-only commands (rejected: Tasks subscribing to a Sharing
  event, which would reverse the module edge).
- `AGENTS.md`: §3 scope gains sharing and snapshots; §5 new AD-12 (sharing),
  AD-1 and §6 gain `PSPad.Module.Sharing`, AD-11 mentions `ListView`; §6
  documents the second module edge.
- `specs/backend-spec.md`: collections, indexes, commands, access rule, sync,
  HTTP, account deletion, statistics ids. `specs/ui-spec.md`: Sharing
  section, "Shared with me", `/join`, `/s`, `/snapshots`, drawer rows.
  `specs/modules-spec.md`: `ListView` built, Sharing module section, Tasks'
  "must not" unchanged.
- `docs/src/pages/features.astro` and landing page: shared lists and public
  snapshots.

## 10. Testing

Unit — `PSPad.Module.Tasks.Tests`:

- Every sharing command and rejection; join idempotent for owner and member;
  rotate keeps members.
- `ListAccess`: owner and member pass, stranger and removed member rejected,
  for each content command family; list-level commands and `LinkTaskToGoal`
  reject members.
- Owner/actor: a member's `CreateTask`, `OrganiseInboxItem`,
  `CreateReferenceItem` produce owner-owned aggregates with `ActorId` the
  member.
- Cross-owner moves rejected.
- Snapshot marks: mark and unmark a task, a step, an item; unchanged emits
  nothing; deleted task and unknown step rejected; marks never complete,
  check or affect `TodayRule.Plan`; `ClearTaskSnapshotMarks` /
  `ClearReferenceItemSnapshotMarks` by owner and member.

Unit — Presentation: `ListView` placement, unchanged emits nothing, another
user's view rejected. Statistics: one event → two records with roles; actor
record without goal; Outstanding ignores `Actor`. Sharing:
`SnapshotBuilder` content, ordering, exclusions, source ids; Statistics
ignores mark events.

Integration — `PSPad.Api.Tests`:

- Join over HTTP returns list and children; wrong and rotated tokens fail.
- Member sync sees owner's documents; `memberListIds`; `full=` backfill;
  removal, leave, list delete and owner account deletion each purge on the
  member's next pull.
- Member commands through `/api/commands` land with the owner's `userId`;
  stranger rejected.
- `GET /api/today` includes shared tasks in the member's zone.
- Statistics rebuild on version bump; two records per member event.
- Snapshot marks: anonymous tick lands on the snapshot and as a chip in the
  owner's next sync; untick removes both; deleted task keeps the snapshot
  tick; `/api/commands` rejects `MarkTaskFromSnapshot`; expired → 404.
- Snapshots: create, anonymous read, expired → 404, revoke, rate limit,
  visits, account deletion sweeps all sharing collections and pulls
  membership.
- Architecture guards for the Sharing edge.

bUnit — `PSPad.App.Tests`:

- "Shared with me" appears only with unplaced member lists; filing moves it.
- Sharing section per role; goal field hidden for members.
- Purge reconciliation on the replica.
- `/public/snapshot/{token}` renders tasks and items, mark toggles and revert on failure,
  expired screen; "Marked on a snapshot" chip and Dismiss; `/snapshots` online
  and cached.
