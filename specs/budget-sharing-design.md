# Budget Sharing — design

Status: Built. Issue #121. Decision of record: `adr/0066`. Builds on
`specs/money-design.md` (the module), `specs/sharing-spec.md` (list
sharing, whose mechanics this copies) and `adr/0054`, `adr/0057`,
`adr/0059`, `adr/0063`.

A household keeps one budget together: every member records expenses and
incomes, keeps the monthly balance and edits categories, offline as freely
as the owner. The owner invites with the same link plus code that lists
use; joining, sharing, ending a link, removing and leaving go straight to
the server.

## 1. Scope

In:

- One invite per budget: link + 6-character code, 30 minutes, closed by five
  wrong codes, replaceable by a new invite (members stay).
- Members: everything on entries, categories and balance months, offline.
- Owner only: rename, archive, restore, invite, end the link, remove a member.
- A member leaves at any time.
- Shared budgets in the member's sidebar Budgets group and on `/budgets`,
  with a shared marker on both sides.
- Account deletion: a member's deletion removes their membership; an
  owner's deletion removes the budget for everyone.

Out:

- Who entered or edited an entry or holding. Nothing is shown and no
  document field records it. The event log still carries `ActorId`, stamped
  by `MongoUnitOfWork` for every event whose owner is not the caller.
- Per-member holdings ("mine" vs "ours"). Separate budgets cover personal
  money; a holding name ("Anna – savings") covers the rest.
- Public snapshots of a budget. Never: money is too sensitive for an open
  link (`specs/money-design.md` §1).
- Handing ownership to a member. The owner deleting their account deletes
  the budget for everyone.
- Per-member permissions, approving joiners, invites to a named account,
  notifications.
- Statistics: Money has no projection, so nothing changes there.

## 2. Domain — `PSPad.Module.Money`

### 2.1 Invite rules move to Abstractions

Money references `PSPad.Abstractions` only (`adr/0059`), and the invite
rules live in `PSPad.Module.Tasks.Lists`. They move, unchanged in
behaviour, to `PSPad.Abstractions/Invites/`:

- `InviteCodes` (alphabet, length, `Normalize`, `IsWellFormed`, `Format`).
- `InviteCheck` (`Unknown`, `WrongCode`, `Expired`, `Open`).
- `Invites` — `Life` (30 minutes), `MostWrongCodes` (5), `ShortestToken`
  (22), `IsLiveAt(token, code, expiresAt, now)`, `Check(storedToken,
  storedCode, expiresAt, token, code, now)` with the constant-time compare,
  and `RequireToken` / `RequireCode` for the share commands.

`TaskList` delegates to them; its fields and BSON stay as they are.
`TaskList.InviteLife` and `TaskList.MostWrongCodes` stay as aliases until
their callers move. The App's `InviteToken` and `InviteCode` helpers switch
their `using` and gain a budget link (§4.1).

### 2.2 `Budget` sharing state

`Budget` gains, with the same meaning and rules as on `TaskList`
(`specs/sharing-spec.md` §2.1):

- `InviteToken?`, `InviteCode?`, `InviteExpiresAt?`, `WrongCodes`. Members
  never receive them through sync (§3.1).
- `OwnerName?` — set by `ShareBudget`, so members show "Shared by …".
- `Members[]` — `BudgetMember(UserId, DisplayName, JoinedAt)`, private
  `_members` in BSON. `DisplayName` comes from the joiner's token and is cut
  to `ContentLimits.Name`.
- `IsShared` = `Members` non-empty or an invite token is set.
- `HasMember(userId)`, `Admits(userId)` (owner or member), `ClosedByWrongCodes`, `IsInviteLiveAt(now)`,
  `CheckInvite(token, code, now)` over `Invites`.
- `UserId` stays the owner, always (`specs/money-design.md` §3.2).

### 2.3 Commands

All five are `IServerOnlyCommand`s behind their own endpoints (§5), never in
the outbox (`adr/0063`). `/api/commands` rejects them.

| Command | Who | Effect / rejections |
|---|---|---|
| `ShareBudget(budgetId, token, code, ownerName)` | owner | Sets or rotates token and code, restarts the 30-minute clock, zeroes `WrongCodes`, stores `OwnerName`. Rejects a short token, a malformed code, and an archived budget ("This budget is archived."). Never removes members |
| `StopSharingBudget(budgetId)` | owner | Clears token, code and expiry. Members stay. No event when already clear |
| `RemoveBudgetMember(budgetId, memberId)` | owner | Rejects a non-member |
| `LeaveBudget(budgetId)` | member | Rejects the owner and non-members |
| `JoinBudget(budgetId, token, code, displayName)` | signed in | Rejects an archived budget, a wrong or cleared token ("This invite link no longer works.") and an expired invite ("This invite has expired."). A wrong code with the right token commits `BudgetInviteCodeRejected` instead of rejecting, so the count survives; the fifth closes the invite. A full budget (`MembersPerBudget`) is rejected. Owner or existing member: accepted, no event |

`StopSharingBudget`, `RemoveBudgetMember` and `LeaveBudget` work on an
archived budget: they only take access away.

Events: `BudgetShared`, `BudgetSharingStopped`, `BudgetInviteCodeRejected`,
`BudgetJoined`, `BudgetMemberRemoved`, `BudgetLeft`. Each carries the
owner's `UserId`; the member's id rides in the event (`MemberId`).

`ArchiveBudget` also ends a live invite: `When(BudgetArchived)` clears
token, code and expiry, so nobody joins a read-only budget. Restoring does
not bring the link back.

Folders follow the grouped-by-operation rule:
`Budgets/{Share,StopSharing,Join,RemoveMember,Leave}/`, plus
`Budgets/BudgetMember.cs`.

### 2.4 Access

`BudgetAccess` splits in two:

- `BudgetAccess.To(budget, actorId)` — the owner **or a member**; anyone else
  reads "That budget does not exist.", like a missing budget. It gates every
  entry, category and balance command (`AddCategory`, `RenameCategory`,
  `MergeCategory`, `RemoveCategory`, `RecordExpense`, `RecordIncome`,
  `EditEntry`, `DeleteEntry`, `RecategoriseEntry`, `StartBalanceMonth`,
  `AddHolding`, `EditHolding`, `RemoveHolding`, `DeleteBalanceMonth`).
- `BudgetAccess.Owned(budget, actorId)` — the owner only, same rejection
  text. It gates `RenameBudget`, `ArchiveBudget`, `RestoreBudget`,
  `ShareBudget`, `StopSharingBudget` and `RemoveBudgetMember`.

`LeaveBudget` and `JoinBudget` check membership themselves.

Every event a member's command emits already takes `UserId` from the budget
(`MoneyEntry.cs`, `BalanceSnapshot.cs`), so entries and snapshots a member
creates belong to the owner. A test pins this for each creating command.

### 2.5 Concurrency

Server saves are version-checked and retried (`adr/0063`); nothing extra.
Two members editing one entry offline: last write wins, the loser is told
(AD-5). A member removed while offline ships commands that `BudgetAccess`
rejects; they surface as rejections and the purge follows on the same pull.
The rename-versus-record category edge (`specs/money-design.md` §3.3) now
also happens between people; the answer is the same: merge.

### 2.6 Limits

`ContentLimits.MembersPerBudget = 100`, checked by `JoinBudget`; the join
endpoint answers its usual `404`. A row in `specs/content-limits-design.md`.

## 3. Sync and offline

### 3.1 What a member pulls

`SyncReader` widens three filters, like lists (`specs/sharing-spec.md` §3.1):

- `budgets`: `userId == caller` **or** `_members.userId == caller`. A budget
  the caller does not own is projected without `InviteToken`, `InviteCode`,
  `InviteExpiresAt` and `WrongCodes`, as lists are (`adr/0057`).
- `moneyentries`, `balancesnapshots`: `userId == caller` **or**
  `budgetId ∈ memberBudgets(caller)`. Entries and snapshots never change
  budget, so no `previousBudgetId` stub is needed.
- `moneypreferences` stays `userId == caller`.

New indexes: `budgets {_members.userId: 1, seq: 1}`,
`moneyentries {budgetId: 1, seq: 1}`, `balancesnapshots {budgetId: 1, seq: 1}`.

### 3.2 The membership set

`SyncResponse` gains `MemberBudgetIds` — budgets the caller belongs to and
does not own. The client reconciles after writing the delta, under the same
"replica has a recorded owner" guard as lists:

- A budget in the replica, not owned by the caller, not in
  `MemberBudgetIds` → purge it and its entries and snapshots. One rule for
  leaving, removal and the owner's account deletion.
- A budget in `MemberBudgetIds` missing from the replica (another device
  joined) → the same pull asks again with `full=<ids>`; `full` accepts list
  and budget ids alike and the server returns each with all its live
  children. A failed full pull is swallowed and retried next sync.
- An entry or snapshot not owned by the caller whose budget the replica does
  not hold → drop it.

No new collection; existing replicas need no pull from zero.

### 3.3 Join

`POST /api/budgets/join {token, code}` resolves the token to a live,
unarchived budget, runs `JoinBudget` server-side and returns the budget and
its live entries and snapshots, which the joining device writes at once.
Rate limiting shares the `join` policy with lists: 10 attempts per user per
30 minutes across both kinds. Uniform `404`; `410` only when token and code
both match an expired invite; `429` over the limit.

### 3.4 Online-only membership changes

Share, end link, remove and leave call their endpoints and then sync
(`adr/0063`); offline their controls are disabled with "Sharing changes
need a connection." Entries, categories and balance stay offline-capable for
everyone.

## 4. UI

Mirrors list sharing (`specs/ui-spec.md`, "Sharing opens from the list
panel"), with the budget's own entry point.

### 4.1 Shared components

`InviteLinkCard` and the people list stop depending on `TaskList`:

- `InviteCard` takes an `InviteState(Token, Code, ExpiresAt,
  ClosedByWrongCodes, MemberCount)`, a `LinkFor(token, code)`, `Thing` ("list" /
  "budget"), an `Invitation` text, an optional `BlockedReason` and two
  callbacks, `OnShare(token, code)` and `OnEnd`, each returning the rejection
  or `null`. Its four states (None, Live, Expired, Closed) and copy stay; the
  None text names the thing. `InviteLinkCard` stays as the list's wrapper.
- `SharePeople` — the **People · N** list: owner row with **Owner** chip,
  member rows with initials avatar (`AvatarColor`), "You", join date for the
  owner, and a remove icon when `OnRemove` is bound.
- `ListMembersView` and the new `BudgetMembersView` compose the two.
- `InviteCode.BudgetJoinLinkFor` builds the budget form:
  `{base}/budgets/join/{token}#code={code}`.

### 4.2 Entry point

- **`BudgetPage` header row**: when the budget `IsShared`, a `People`
  `MudIconButton` with a member-count `MudBadge` sits before the settings
  gear (`aria-label` "Members, N people"). It opens `BudgetMembersPanel` via
  `?members={budgetId}`.
- **Settings tab**: a **Sharing** card under the Budget card — one row
  "Members" with secondary text "Only you", "Nobody joined yet", "N joined",
  or "N joined · link M min left", and a chevron. It opens the same panel.
  For a member the Budget card is replaced by "Shared by {OwnerName}. Only
  {OwnerName} can rename or archive it.", and the Sharing card still opens
  the panel.
- **`BudgetMembersPanel`**: a `DetailPanel` (`adr/0052`) titled "Members",
  full width on a phone, holding `BudgetMembersView`.

### 4.3 `BudgetMembersView`

An offline banner "Sharing changes need a connection." when offline; every
control is disabled offline.

- **Owner.** `InviteLinkCard` with the budget wording — None reads "Only
  you use this budget" and "It works for 30 minutes — whoever opens it in
  that time sees every entry and balance and can add and edit them." Then
  `SharePeople` with remove icons (confirm "Remove {name} from this
  budget?"), then **Stop sharing · remove everyone** (confirmed: "The invite
  link stops working and everyone else loses {budget}.") while there are
  members or a live invite. For an archived budget the invite card shows
  "Restore the budget to invite people." instead of its create buttons;
  removing and stopping stay available.
- **Member.** A "Shared by {OwnerName}" card with a "You can edit" chip,
  `SharePeople` without remove icons, "Only {OwnerName} can invite
  people.", and **Leave budget** (`Color.Error`, confirm "Leave {budget}? It
  disappears from your devices.", navigates to `/budgets`). A member never
  sees the link or code.

### 4.4 Where a shared budget shows

- **Sidebar Budgets group**: the user's own active budgets in creation
  order, then budgets shared with them by join date, each with a small
  `People` icon after the name; then **+ New budget**.
- **Phone**: the Budgets tab lands on `/budgets`; nothing else changes.
- **`/budgets`**: own cards first, then shared ones, in the same orders.
  `BudgetCard` shows a tinted `People` chip with the head count (owner
  included; tooltip "Shared · N people") for an owner's shared budget, and a "Shared by {OwnerName}"
  caption for a member. The subtitle counts both: "N budgets · M archived ·
  totals in PLN".
- **Archived shared budget**: archived for everyone, under the existing
  "Show archived" switch. A member's banner reads "{OwnerName} archived this
  budget. It is read-only."; Restore lives only on the owner's Settings tab.
- **Default currency** stays per user: each person's panels preselect their
  own `MoneyPreferences`.

### 4.5 Join page

`JoinPage` gains a second route, `/budgets/join/{Token}`, and tells the
kinds apart by that path. The budget form reads "The person who shared the budget has
a 6-character code.", the button **Join budget**, and success navigates to
`/budgets/{id}`. Offline, expired, invalid and throttled states are the
list's.

## 5. HTTP surface

| Method | Path | Auth | Purpose |
|---|---|---|---|
| `POST` | `/api/budgets/join` | user | `{token, code}` → `{budgetId, documents}`. Uniform `404`, `410` expired, `429` |
| `POST` | `/api/budgets/{id}/share` | owner | `{token, code}`; `204`, `404` unknown or not the owner, `409` with the rejection |
| `DELETE` | `/api/budgets/{id}/share` | owner | End the link. `204` / `404` |
| `DELETE` | `/api/budgets/{id}/members/{memberId}` | owner | `204` / `404` / `409` |
| `POST` | `/api/budgets/{id}/leave` | member | `204` / `404` |
| `GET` | `/api/sync?since=&full=` | user | `full` takes list and budget ids; response adds `memberBudgetIds` |

Endpoints live in `PSPad.Api/Endpoints/BudgetEndpoints.cs`, shaped like
`ListEndpoints` (one `404` for missing and not-allowed). The App gets
`IBudgetSharingApi` beside `IListSharingApi`.

**Account deletion** (`adr/0034`): the owner's budgets, entries and
snapshots already go by `userId`. The sweep adds a `$pull` of the caller
from every other owner's `budgets._members`, stamping each touched budget
with a fresh `seq` from `counters`. Members lose an owner's budgets through
§3.2.

No new service, port, secret or env var.

## 6. Testing

Every class tagged `[UnitTest]` or `[IntegrationTest]`.

- **Unit, Abstractions:** `Invites.Check` and `IsLiveAt` (moved tests from
  `TaskList`), `InviteCodes`.
- **Unit, domain (`Budget`):** each sharing command's happy path and
  rejections; archived budget rejects `ShareBudget` and `JoinBudget` but
  not stop, remove or leave; archive clears a live invite; five wrong codes
  close it; `MembersPerBudget`; owner-only commands reject a member;
  member-allowed commands accept a member and stamp the owner's `UserId`
  (record, start month, add holding, add category, rename cascade).
- **Unit, components (bUnit):** `BudgetMembersView` owner vs member, offline
  disabling, archived owner card; `InviteLinkCard` with budget wording;
  `SharePeople`; header Members button only when shared; member Settings
  shows "Shared by" instead of rename/archive; `/budgets` ordering and
  markers; `JoinPage` budget route.
- **Integration (Testcontainers MongoDB):**
  - Join through `/api/budgets/join`: success returns children; wrong code
    counts; expired `410`; archived `404`; shared rate limit with list joins.
  - Share/stop/remove/leave endpoints: `204`, `404` for non-owner,
    `/api/commands` rejects the server-only commands.
  - Member sync: budget without invite secrets, entries and snapshots of
    member budgets, `memberBudgetIds`, `full=` backfill.
  - A member's `RecordExpense` through `/api/commands` lands with the
    owner's `userId` and the member's `actorId` on the event.
  - Removed member's queued command is rejected.
  - Account deletion: member pulled from `budgets._members` with a new `seq`;
    owner deletion wipes the budget.
- **App sync:** reconcile purges a budget gone from `MemberBudgetIds` and
  its children; backfills a missing one.
- **Guards:** Money still references `Abstractions` only.

## 7. Delivery slices

1. Invite rules to Abstractions; `TaskList` delegates; list tests green.
2. `Budget` sharing state, the five commands, `BudgetAccess.Owned`,
   archive clears the invite, limits.
3. Endpoints, rate limit, sync filters, `memberBudgetIds`, `full=`,
   indexes, account sweep.
4. Client: `IBudgetSharingApi`, reconcile, generalised `InviteLinkCard` and
   `SharePeople`, `BudgetMembersView` and panel, header and Settings entry,
   sidebar and `/budgets` markers, join route.
5. Docs: `features.astro`, landing page, `specs/ui-spec.md`,
   `specs/backend-spec.md`, `specs/money-design.md` §9.

## 8. Docs and records

- **ADR-0066** — budgets are shared by membership; invite rules move to
  Abstractions. Extends `adr/0054`, `adr/0057`, `adr/0059`, `adr/0063`. Mockups: the "PSPad Budget Sharing"
design canvas (https://claude.ai/artifact/WZJqhSGw6eqrqHTg391UAg).
- `AGENTS.md`: AD-12 covers budgets; §3 points here.
- `specs/money-design.md` §9 points here; `specs/content-limits-design.md`
  gains `MembersPerBudget`.
- On delivery: `specs/backend-spec.md` (commands, endpoints, sync, sweep),
  `specs/ui-spec.md` (members panel, join route, markers),
  `docs/src/pages/features.astro` and the landing page.
