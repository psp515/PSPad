# List ordering and the masonry area board — design

Status: Built. Issue #96. Decision of record:
[ADR-0051](../adr/0051-presentation-module-per-user-views.md), which
supersedes [ADR-0012](../adr/0012-extract-ordering-into-its-own-module.md).

A user orders the lists inside an area to prioritise them, and the area board
draws its list cards without the empty bands a row grid leaves under short
cards.

## 1. Scope

In:

- A new module, `PSPad.Module.Presentation`: how a user sees their data,
  never what the data is.
- One aggregate in it, `AreaView` — per `(user, area)`, holding the order of
  that area's lists.
- `ReorderLists`, the one command, sent from a **Lists** section inside
  `AreaDetailPanel`.
- `TaskList.Position` removed; `TaskList.CreatedAt` added and backfilled.
  Lists with no stored order fall back to creation date.
- `MasonryGrid<TItem>`, a column-split board layout on MudBlazor's
  breakpoints, used by `AreaBoard`.

Out:

- `ListView` (per-user list colour, icon, step/item order). Named in
  `specs/modules-spec.md` as the next extension point, not built.
- Moving `Area`, `Step`, `InboxItem`, `ReferenceField` or `ReferenceItem`
  positions into the module. They keep `Position` until their own issue.
- Drag and drop. Pinning a list to a column.
- Moving a list to another area by reordering.

## 2. Domain — `PSPad.Module.Presentation`

References `PSPad.Abstractions` only; compiles to WASM (AD-3, AD-4). Nothing
references it except `PSPad.Api`, `PSPad.App` and the test projects.
Architecture guards: `Tasks`, `Statistics`, `Identity` and `Infrastructure`
never reference `Presentation`; `Presentation` references no module.

```
PSPad.Module.Presentation/
  Ordering/Arranged.cs               display-order function
  AreaViews/AreaView.cs              aggregate
  AreaViews/Reorder/ReorderLists.cs
  AreaViews/Reorder/ListsReordered.cs
  AreaViews/Reorder/ReorderListsHandler.cs
```

**`AreaView`** — `Id`, `UserId`, `AreaId`, `Order: IReadOnlyList<Guid>`.
`AreaView.IdFor(userId, areaId)` derives a deterministic `Guid` from both
ids, so the server and every offline replica address one document and
last-write-wins (AD-5) settles concurrent reorders instead of two views
competing.

**`ReorderLists(CommandId, UserId, AreaId, Order, ListId, ToIndex)`** →
`ListsReordered(AggregateId, UserId, At, AreaId, Order)`.

- `Order` is the order the client currently displays, lists without a stored
  position included. The module therefore never reads `TaskList`, and the
  handler needs no cross-module query.
- The handler loads `AreaView.IdFor(UserId, AreaId)`; a missing view is
  created by the first reorder.
- `Decide` rejects: an empty `AreaId`; a `ListId` not in `Order`; a view that
  belongs to another user; `Order` holding duplicates.
- A move that leaves the order unchanged emits nothing.

**`Arranged.Sort(elements, order, id, createdAt)`** — pure, shared by every
screen that shows lists in area order:

1. elements whose id is in `order`, in `order`'s sequence;
2. then the rest, by `createdAt` ascending, then by id;
3. ids in `order` that match no element (deleted lists, lists moved to
   another area) are ignored.

Deleting or moving a list never touches the view, so there is no cascade.
A list moved to another area appears last there, by creation date.

## 3. Changes to `PSPad.Module.Tasks`

- `TaskList.Position` removed from the aggregate, `CreateTaskList` and
  `TaskListCreated`. Stored `position` fields on existing documents are
  ignored — no migration (ADR-0051).
- `TaskList.CreatedAt` (`DateTimeOffset?`) set from `TaskListCreated.At`;
  `MongoBackfill` fills it for existing lists from their created event.
- `Ordering/Positions.cs` stays in Tasks: `Step`, `InboxItem`,
  `ReferenceField`, `ReferenceItem` and `Area` keep their `Position` fields
  and still use it. `AreaView` reorders its own `Order` in a few lines rather
  than reference Tasks; the duplication goes when those positions move over.
- Every client screen that ordered lists by `Position` (task/reference/inbox
  panels' list pickers, `AreaBoard`) orders them through one client helper,
  `ListOrder.Arrange(lists, views)`, which groups by area and applies
  `Arranged.Sort`.

## 4. Storage and sync

- Collection `areaviews`: `{ _id, userId, areaId, order, version, seq,
  deleted }`, indexed `{userId, seq}` like the others. Events go to the shared
  `events` log (AD-2).
- `SyncReader` and the client `SyncService` map gain `areaviews`; a replica
  that has never seen it pulls it from zero once (ADR-0049).
- Account deletion (ADR-0034) sweeps `areaviews`.
- Area deletion leaves the view orphaned; nothing reads it, account deletion
  sweeps it. Not added to the ADR-0042 cascade.
- Statistics ignores `ListsReordered`.

## 5. UI

### Masonry area board

`Components/MasonryGrid<TItem>` — `Items`, `ItemTemplate`, `Key`.

- Column count from `IViewport`: 1 below `sm`, 2 at `sm`/`md`, 3 at `lg`,
  4 from `xl` — the ui-spec §2 breakpoints. Re-renders on breakpoint change.
- Item *i* goes to column *i mod n*, slot *i div n*
  (`MasonryGrid.Columns(items, n)`, pure). The first row is always the top
  *n* lists; a phone shows the order top to bottom.
- Markup: `MudGrid Spacing="4"` → one `MudItem` per column with the §2
  `xs/sm/lg/xl` sizes → `MudStack Spacing="4"` of cards. No new CSS.
- DOM order is column-major — an explicit exception to ui-spec §2's
  row-flow rule, for `MasonryGrid` only.
- Used by `AreaBoard` only. The empty state stays a single card.

### Lists section in `AreaDetailPanel`

Existing areas only, under the Name field:

- `PanelSection` **Lists** with a muted count, one row per list in
  `Arranged.Sort` order: `ListIcon.For(list)`, the name, then a
  `DragIndicator` handle at the end. Rows reorder by drag only
  (`MudDropContainer` + `MudDropZone AllowReorder`); reference-item fields
  got the same drag reordering in place of their up/down buttons.
- No lists → muted "No lists yet.".
- A drop sends `ReorderLists` with the displayed order and the zone index;
  saves as it changes, no Save. Drops while one is in flight are ignored.
- The **Edit area** FAB item's tooltip becomes "Edit area & order lists".

## 6. Testing

- `PSPad.Module.Presentation.Tests` [Unit]: `AreaView.Decide`, `IdFor`,
  `Arranged.Sort`.
- `PSPad.Module.Tasks.Tests` [Unit]: `TaskList.CreatedAt`; `Position`
  expectations removed.
- `ArchitectureTests` [Unit]: the reference guards in §2.
- `PSPad.Api.Tests` [Integration]: `ReorderLists` round trip through the
  command endpoint and `/sync`; idempotent resend; account deletion wipes
  `areaviews`; backfill sets `createdAt` on a list.
- `PSPad.App.Tests` [Unit, bUnit]: `MasonryGrid.Columns`; `MasonryGrid`
  renders per viewport; `AreaDetailPanel` Lists section order, disabled
  ends, sent command; `AreaBoard` arranged order.
