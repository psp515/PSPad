# Architecture Decision Records

Format: [template.md](template.md). Every ADR carries a title, tags, a
decision date, a status (`Proposed` / `Active` / `Superseded by ADR-XXXX`),
the context that forced the decision, the decision itself, the alternatives
that were genuinely weighed, and the consequences — including the downsides.

New decision → copy `template.md` to `NNNN-kebab-case-title.md`, next
sequential number, status `Proposed` until acted on, then `Active`. Changing
your mind about a past decision never edits that file's Decision or
Consequences after the fact — write a new ADR that supersedes it, and update
the old one's status line to point at the new number.

## Index

| # | Title | Status | Date | Tags |
|---|-------|--------|------|------|
| [0001](0001-vertical-slice-folders-in-one-project.md) | Organize the backend as vertical-slice folders in one project | Superseded by [0010](0010-modular-monolith-three-modules.md) | 2026-09-11 | architecture, modularity |
| [0002](0002-cqrs-event-sourced-write-side.md) | Use CQRS with an event-sourced write side on Marten/PostgreSQL | Superseded by [0011](0011-aggregate-documents-are-truth.md) | 2026-09-11 | architecture, persistence, event-sourcing |
| [0003](0003-commands-are-the-shared-contract.md) | Commands are the shared client/server contract | Active | 2026-09-11 | architecture, offline, contracts |
| [0004](0004-tasks-module-compiles-to-wasm.md) | The Tasks module must compile to WebAssembly with zero infrastructure references | Active | 2026-09-11 | architecture, offline, purity |
| [0005](0005-offline-conflicts-last-write-wins.md) | Resolve offline conflicts last-write-wins per aggregate | Active | 2026-09-11 | architecture, offline, sync |
| [0006](0006-sync-is-delta-by-version.md) | Sync pulls deltas by a monotonic version marker, never a clock | Active | 2026-09-11 | architecture, offline, sync |
| [0007](0007-recurrence-as-template-and-occurrences.md) | Model recurrence as a template plus derived occurrences, never a rolling date | Active | 2026-09-11 | architecture, domain, recurrence |
| [0008](0008-aggregate-boundaries.md) | Aggregate boundaries — Area, TaskList, TodoTask, Goal, Inbox, User | Active | 2026-09-11 | architecture, domain, aggregates |
| [0009](0009-integration-tests-own-their-database.md) | Integration tests own a real, disposable MongoDB via Testcontainers | Active | 2026-09-12 | testing, persistence |
| [0010](0010-modular-monolith-three-modules.md) | Split the modular monolith into three separate module projects | Active (supersedes [0001](0001-vertical-slice-folders-in-one-project.md)) | 2026-09-12 | architecture, modularity |
| [0011](0011-aggregate-documents-are-truth.md) | Aggregate documents are truth; events are the log beside them | Active (supersedes [0002](0002-cqrs-event-sourced-write-side.md)) | 2026-09-12 | architecture, persistence, event-sourcing |
| [0012](0012-extract-ordering-into-its-own-module.md) | Extract element ordering into its own module, out of the Tasks domain | Proposed | 2026-09-12 | architecture, domain, ui, future-work |
| [0013](0013-responsive-shell-and-per-device-theme.md) | Keep sections and areas on separate navigation surfaces, and the theme per device | Superseded by [0014](0014-one-navigation-tree-at-every-width.md) | 2026-09-12 | ui, offline, identity |
| [0014](0014-one-navigation-tree-at-every-width.md) | One navigation tree at every width, revealed rather than rebuilt | Active (supersedes [0013](0013-responsive-shell-and-per-device-theme.md); Goals' placement superseded by [0017](0017-promote-goals-to-a-sidebar-row.md); History's placement superseded by [0020](0020-history-sidebar-row-and-settings-screen.md)) | 2026-09-12 | ui, offline, identity |
| [0015](0015-one-self-host-compose-stack.md) | One `.env`-driven self-host compose stack | Active | 2026-09-14 | deployment, security, identity |
| [0016](0016-documentation-site.md) | A documentation site in Astro, published to GitHub Pages | Active | 2026-09-14 | documentation, tooling, deployment |
| [0017](0017-promote-goals-to-a-sidebar-row.md) | Promote Goals to a permanent sidebar row | Active (supersedes Goals' placement in [0014](0014-one-navigation-tree-at-every-width.md); History's placement here superseded by [0020](0020-history-sidebar-row-and-settings-screen.md)) | 2026-09-15 | ui, domain |
| [0018](0018-purge-local-replica-on-user-switch.md) | Purge the local replica and outbox when the signed-in user changes | Active | 2026-09-15 | offline, sync, identity |
| [0019](0019-created-at-on-todo-task-and-burndown-backfill.md) | Backfill TodoTask.CreatedAt from TaskCreated, and bump seq so delta sync delivers it | Active | 2026-09-16 | persistence, sync, analytics |
| [0020](0020-history-sidebar-row-and-settings-screen.md) | History as a sidebar row, theme and sync status into a Settings screen | Active (amends [0014](0014-one-navigation-tree-at-every-width.md); supersedes History's placement in [0017](0017-promote-goals-to-a-sidebar-row.md)) | 2026-09-16 | ui, identity |
| [0021](0021-drop-structurally-rejected-outbox-commands.md) | Drop structurally rejected outbox commands instead of retrying forever | Active (refines [0005](0005-offline-conflicts-last-write-wins.md)) | 2026-09-16 | architecture, offline, sync, contracts |

Sourced from AGENTS.md §5 ("Architecture decisions") and the two design
specs in `specs/` that established and then revised them.
AGENTS.md stays the terse day-to-day reference (AD-1 … AD-9); this index is
where the reasoning and rejected alternatives behind each one live.
