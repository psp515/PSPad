# Reference lists and descriptions — design

Status: Built. Decisions of record:
[ADR-0047](../adr/0047-reference-items-are-their-own-aggregate.md),
[ADR-0048](../adr/0048-descriptions-are-markdown.md).

Build order: **before** the GitHub integration
(`specs/github-integration-design.md`), which relies on task descriptions
and on list kinds.

Two features, one spec, because they share the Markdown description:

1. **Descriptions.** Every task gets a Markdown description.
2. **Reference lists.** A list kind whose items are never done. Each item is a
   name, a description and an ordered set of labelled fields, e.g. a filament
   (colour, amount left, nozzle temperature), a Word document on OneDrive (a
   link), or a folder on disk (a path).

## 1. Scope

In:

- `TaskList.Kind`: `Tasks` (today's lists) or `Reference`, chosen at creation,
  never changed.
- `ReferenceItem`, a new aggregate: name, Markdown description, star,
  position, ordered fields (`Label`, `Value`, optional display hint).
- Field display kinds: Text, Link, Path, Quantity (number with unit).
  Detected from the value unless the user picks one; display-only, stored as
  text.
- `TodoTask.Description` (Markdown).
- A list-kind icon wherever a list is shown.
- Markdown shown rendered by default, edited in raw form on demand.

Out:

- Colour and date display kinds (display-only, can be added later without
  storage change).
- A per-list field schema / table view (belongs to the print domain,
  subsystem 7).
- Converting a list between kinds.
- Opening local paths — browsers block `file://`; paths are shown and copied.
- Statistics for reference items.
- Attachments, images in descriptions beyond Markdown links.

## 2. Domain — `PSPad.Module.Tasks`

### 2.1 `TaskList.Kind`

- `public enum ListKind { Tasks, Reference }` in `Lists/ListKind.cs`.
- `CreateTaskList` gains `ListKind Kind = ListKind.Tasks` as its last
  parameter; `TaskListCreated` gains `ListKind Kind = ListKind.Tasks` as its
  last parameter. Old events and documents without the field read as `Tasks`.
- No command changes the kind.
- `TaskList.RequireAcceptsTasks(list, userId)` — rejects a `Reference` list
  ("That list holds references, not tasks.").
- `TaskList.RequireAcceptsReferences(list, userId)` — rejects a `Tasks` list
  ("That list holds tasks, not references.").

### 2.2 Where each thing may live

| Operation | Target list must be |
|---|---|
| `CreateTask`, `MoveTaskToList` | `Tasks` |
| `OrganiseInboxItem` | `Tasks` — **an Inbox item only ever becomes a task** |
| `CreateReferenceItem`, `MoveReferenceItemToList` | `Reference` |

The handlers of those commands call the matching `Require…` on the target
list, next to where they call `TaskList.Require` today.

### 2.3 `TodoTask.Description`

- `Description` (string, default `""`).
- `SetTaskDescription(CommandId, UserId, TaskId, string Description)` →
  `TaskDescriptionSet(…, string Description)`. Trailing whitespace trimmed;
  empty means no description; unchanged text emits nothing. No length rule in
  the domain.

### 2.4 `ReferenceItem`

Folder `References/`, namespace `PSPad.Module.Tasks.References`.

| Member | Type |
|---|---|
| `ListId` | `Guid` |
| `Name` | `string` |
| `Description` | `string` |
| `Starred` | `bool` |
| `Position` | `int` |
| `Fields` | ordered `ReferenceField(Guid Id, string Label, string Value, string? Display, int Position)` |

Commands and events:

| Command | Event | Rule |
|---|---|---|
| `CreateReferenceItem(ItemId, ListId, Name, Position)` | `ReferenceItemCreated` | name required; list must be `Reference` |
| `RenameReferenceItem(ItemId, Name)` | `ReferenceItemRenamed` | name required |
| `SetReferenceItemDescription(ItemId, Description)` | `ReferenceItemDescriptionSet` | as §2.3 |
| `StarReferenceItem(ItemId, Starred)` | `ReferenceItemStarred` | |
| `MoveReferenceItemToList(ItemId, ListId)` | `ReferenceItemMovedToList` | target must be `Reference` |
| `DeleteReferenceItem(ItemId)` | `ReferenceItemDeleted` | |
| `AddReferenceField(ItemId, FieldId, Label, Value, Display?)` | `ReferenceFieldAdded` | label required; value may be empty; appended |
| `EditReferenceField(ItemId, FieldId, Label, Value, Display?)` | `ReferenceFieldEdited` | label required; field must exist |
| `MoveReferenceField(ItemId, FieldId, ToIndex)` | `ReferenceFieldsReordered` | dense positions (`Ordering/Positions`) |
| `RemoveReferenceField(ItemId, FieldId)` | `ReferenceFieldRemoved` | densify after |

Every command carries `CommandId` and `UserId`; ownership and "no longer
exists" rejections follow `TodoTask.Require`. `Display` is an opaque string:
the domain stores whatever the client sends (`text`, `link`, `path`,
`quantity`, or null for "detect").

All are ordinary wire commands (AD-3): the client runs them offline against
its replica.

### 2.5 Cascade

`TaskListCascade.Delete` also deletes the list's live `ReferenceItem`s, in the
same commit (ADR-0042). `DeleteTaskListHandler` and `DeleteAreaHandler` take
`IDocumentStore<ReferenceItem>`. A write into a deleted list is rejected as
for tasks.

### 2.6 Untouched

Today rule, Starred section of My Day, goals, recurrence, Statistics — none
reads `ReferenceItem`. No Statistics projection is added.

## 3. Storage and sync

- Collection `referenceitems` (aggregate type name, lower case, plural — as
  `todotasks`). Added to `MongoIndexes.AggregateCollections` (`userId, seq`)
  plus an index on `userId, listId`.
- `TaskList.kind` and `TodoTask.description` ride existing documents; missing
  fields read as defaults. No backfill.
- `SyncReader` reads `referenceitems` inside its snapshot transaction and
  returns `Documents["referenceitems"]`.
- Client: `SyncService`'s collection → type map gains
  `["referenceitems"] = typeof(ReferenceItem)`. The IndexedDB replica keeps
  every document in one `documents` store indexed by `type, userId`, so no
  schema version change. `IDocumentStore<ReferenceItem>` over the replica is
  the existing generic `ReplicaDocumentStore<T>`.
- Account deletion (ADR-0034) already sweeps every collection by `userId`.
- `DomainEventCatalogue` discovers the new events by assembly scan.

## 4. UI

### 4.1 List kinds

- `ListDetailPanel` (new list): a `MudToggleGroup` "Tasks" / "Reference",
  default Tasks, above the name field. Absent when renaming.
- Icon per kind wherever a list appears — `ListCard` / `ThingCard` on the
  area board, the `ListPage` header and search results (the sidebar lists
  areas, not lists): `Checklist` for Tasks,
  `LibraryBooks` for Reference (GitHub lists later:
  `Icons.Custom.Brands.GitHub`). One helper, `ListIcon.For(TaskList)`.

### 4.2 Reference list page

- `ListPage` for a `Reference` list renders `ReferenceRow`s instead of
  `TaskRow`s: name, then up to the first two fields as a caption
  ("Colour: black · Left: 350 g"), star on the right, no checkbox. Starred
  first, then by `Position`.
- FAB menu: "Add item", "Rename list", "Delete list" (same shape as today).
- `ListCard` for a Reference list: the add button creates an item.
- Search (`ReplicaSearch`) includes item names; a hit opens the list with the
  item's panel.

### 4.3 `ReferenceItemPanel`

Same slot as `TaskDetailPanel`.

- Name (editable) and star.
- Description — `MarkdownField` (§4.5).
- Fields — `MudList`; each row shows the label and the value rendered by
  kind:

| Kind | Rendering |
|---|---|
| Link | `MudLink`, new tab |
| Path | monospace + `MudIconButton` "Copy" (clipboard JS interop), snackbar "Copied" |
| Quantity | "350 g" |
| Text | multi-line text |

  Clicking a row edits it in place: label, value, and a `MudSelect` kind
  (Automatic, Text, Link, Path, Quantity). Drag to reorder; "Remove" per row;
  "Add field" at the end.
- Actions: move to another Reference list (picker lists Reference lists
  only), delete (`ConfirmDialog`).

### 4.4 Kind detection

`FieldDisplay.Detect(string value)` — pure, one place, used when `Display` is
null:

| Match | Kind |
|---|---|
| `^https?://` | Link |
| `^[A-Za-z]:[\\/]`, `^\\\\`, `^/`, `^~/` | Path |
| `^-?\d+([.,]\d+)?\s*[^\d\s]\S*$` | Quantity |
| otherwise | Text |

### 4.5 `MarkdownField`

Shared by tasks, reference items, and later mirrored GitHub tasks.

- Default: **view mode** — rendered Markdown in a `MudPaper`, an edit icon in
  the corner. Empty shows a muted "Add a description"; clicking it enters
  edit mode.
- **Edit mode** — `MudTextField` `Lines="8"` `AutoGrow`, "Save" / "Cancel".
  Save sends the component's command.
- `ReadOnly` parameter: view mode only, no edit icon (for mirrored tasks).
- Rendering: **Markdig** (BSD-2-Clause, GPL-3 compatible) with
  `DisableHtml()` — raw HTML is escaped. Links whose scheme is not `http`,
  `https` or `mailto` render as plain text. Task-list and pipe-table
  extensions on; rendered checkboxes are disabled.

### 4.6 Tasks

`TaskDetailPanel` shows a `MarkdownField` for the task's description under
the name. `TaskRow` shows a small `Notes` icon in its meta row when a task has
a description.

### 4.7 Pickers

- Task list pickers (`TaskDetailPanel`, `InboxItemPanel`) show `Tasks` lists
  only.
- The reference item's move picker shows `Reference` lists only.

## 5. Testing

Unit — `PSPad.Module.Tasks.Tests`:

- `TaskList.Kind` from create; default `Tasks`; the two `Require…` helpers.
- `ReferenceItem`: every command, every rejection (empty name, empty label,
  missing field, someone else's item, deleted item).
- Field positions stay dense after move and remove.
- Handlers: no item into a Tasks list, no task into a Reference list, no
  Inbox item into a Reference list, moves only between lists of the right
  kind.
- Cascade: deleting a list or area deletes its reference items in one commit.
- `SetTaskDescription`: trims, clears, emits nothing when unchanged.

Integration — `PSPad.Api.Tests`:

- `ReferenceItem` Mongo round trip with fields and display hints.
- A `TaskList` document without `kind` loads as `Tasks`.
- Sync returns `referenceitems` from a marker.
- Offline round trip: reference commands from the outbox land.
- Account deletion clears `referenceitems`.
- Cascade through `/api/commands`.

bUnit — `PSPad.App.Tests`:

- `FieldDisplay.Detect` table (`/etc` → Path, `3 pcs` → Quantity,
  `abc` → Text, `https://x` → Link, `C:\x` → Path, `\\nas\x` → Path).
- `MarkdownField`: view by default, edit toggle, save sends the command, raw
  HTML escaped, `javascript:` link rendered as text, `ReadOnly` hides edit.
- `ReferenceRow`, `ReferenceItemPanel`: rendering per kind, copy path, add and
  edit a field.
- `ListDetailPanel` kind toggle; `ListIcon` in sidebar and card.
- Pickers filter by kind.

## 6. Docs and records

- ADR-0047 — reference items are their own aggregate, not a task kind.
- ADR-0048 — descriptions are Markdown, rendered client-side, raw HTML off.
- `AGENTS.md`: §3 in-scope gains reference lists and descriptions, and "list
  types beyond plain" leaves Out; §5 AD-8 lists `ReferenceItem`; §6 unchanged
  layout, new folder noted.
- `specs/backend-spec.md`: aggregates, commands, collections, sync.
- `specs/ui-spec.md`: list kinds and icons, reference list page, item panel,
  `MarkdownField`.
- `docs/src/pages/features.astro` and landing page: reference lists and
  descriptions.
