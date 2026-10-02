# Modules Spec

Standing rules for what each `src/modules/` project owns, what it must never
absorb, and where it grows next. A rulebook, not a history: it states the
boundaries as they are now. For *why* a boundary exists, follow the ADR each
section cites. Where this spec and an ADR disagree, the ADR wins.

Reference rules and their guard tests live in `specs/backend-spec.md` §1;
this file is about responsibility, not wiring.

---

## PSPad.Module.Tasks

**Purpose.** The GTD domain — what the user's data *is*.

**Owns.** `Area`, `TaskList`, `TodoTask` (steps inside), `Goal`, `Inbox`,
`ReferenceItem` (fields inside); recurrence, the Today rule, container-delete
cascades. Collections `areas`, `tasklists`, `todotasks`, `goals`, `inboxes`,
`referenceitems`.

**Must not.** Reference anything but `PSPad.Abstractions` (AD-4). Carry
per-user presentation — list order, colour, icon, collapsed state — on a
domain aggregate: that is Presentation's (`adr/0051`). Know that Statistics
or Presentation exist.

**Still carrying presentation (debt).** `Area.Position`, `Step.Position`,
`InboxItem.Position`, `ReferenceItem.Position` and `ReferenceField.Position`
predate Presentation and use `Ordering/Positions`. Each moves to a
Presentation view in its own issue; until then, don't add new ones.

---

## PSPad.Module.Presentation

**Purpose.** How one user sees their data, never what the data is
(`adr/0051`). Per user even when the underlying data is shared.

**Owns.** `AreaView` — one per `(user, area)`, id `AreaView.IdFor(userId,
areaId)`, holding `Order` of the area's lists; `ReorderLists`;
`Ordering/Arranged.Sort` (stored order first, then creation date, then id).
`ListView` — one per `(user, list)`, id `ListView.IdFor(userId, listId)`,
holding `AreaId?`: where that user files a shared list they are a member
of; `PlaceList` sets it, `null` returns the list to "Shared with me". A
missing view, or one naming a deleted or unknown area, falls back to
"Shared with me" the same way a missing `AreaView` falls back to creation
order. Collections `areaviews`, `listviews`.

**Must not.** Reference any module — commands carry what the view needs
(e.g. the displayed order), so the module never reads a Tasks aggregate.
Hold domain facts: deleting a view must never lose data, only a preference.
Require a view to exist — every screen works with none, falling back to
creation order.

**Extension points.**

- `ListView` fields beyond `AreaId`: the list's colour and icon (list
  theming), and the order of its steps or reference items once those
  positions leave Tasks. Add fields here, not on `TaskList`. Not built.
- The sidebar's area order — a user-level view, replacing `Area.Position`.
  Not built.
- A new view follows `AreaView`'s shape: deterministic id from user +
  subject, created by its first command, `Arranged.Sort`-style fallback so a
  missing view is never an error, and its collection added to sync
  (`adr/0049` handles existing replicas).

---

## PSPad.Module.Statistics

**Purpose.** Read-side records projected off the domain event log
(`adr/0036`, `adr/0037`, `adr/0038`, `adr/0039`).

**Owns.** `statistics_records`, `statistics_inbox_records`,
`statistics_labels`, `statistics_state`; tiles, charts, heatmap, feed.

**Must not.** Write domain documents or become the source of truth for
anything but itself. Be referenced by Tasks. Presentation events
(`ListsReordered`) are not projected — a preference is not activity.

**Extension points.** Habit-progress charts once habits exist.

---

## PSPad.Module.Sharing

**Purpose.** Public, account-free access to a frozen copy of a list
(`adr/0056`). Everything a signed-in member needs — `ListAccess`,
membership, `ListView` placement — stays in `PSPad.Module.Tasks` and
`PSPad.Module.Presentation`; this module exists only for the visitor who
has no account at all.

**Owns.** `ListSnapshot` (frozen `Tasks[]`/`Items[]`, each entry carrying
its own mark), `SnapshotBuilder`, `SnapshotPublishing`, `SnapshotMarking`,
`SnapshotVisit`. Collections `list_snapshots`, `snapshot_visits`.

**Must not.** Be referenced by `PSPad.Module.Tasks` — the edge runs one way,
Sharing reads Tasks' aggregate shapes to build a copy, never the reverse.
Reference `PSPad.Module.Statistics`, `PSPad.Module.Presentation`,
`PSPad.Infrastructure`, MongoDB or ASP.NET Core directly — it stays a pure,
server-side model like Tasks itself, hosted by `PSPad.Api`'s own adapters
(`PSPad.Api.Snapshots`). Write a Tasks document except through the two
server-only commands (`MarkTaskFromSnapshot`, `MarkReferenceItemFromSnapshot`)
that `adr/0054`'s `IServerOnlyCommand` mechanism was built for. Put a
snapshot in the event log or sync protocol — it is online-only, frozen at
publish time, and useless to a device that only has its own replica.

**Extension points.** Live chips on the owner's side instead of next-sync —
issue #105, a separate spec.

---

## PSPad.Module.Identity

**Purpose.** Who the user is: `User`, time zone, display name, first-sign-in
provisioning.

**Owns.** `users`.

**Must not.** Hold per-screen presentation (that is Presentation) or
authentication mechanics (Keycloak/JWT live in `PSPad.Infrastructure` and
`PSPad.Api`).

---

## Future modules

Each external integration is its own module (`adr/0045`, Proposed). Habits
and annual plans extend Tasks' occurrence and goal models (AGENTS.md §9)
rather than starting new modules unless a boundary here says otherwise.
