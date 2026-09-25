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
| [0015](0015-one-self-host-compose-stack.md) | One `.env`-driven self-host compose stack | Active (realm and client naming amended by [0026](0026-one-shared-realm-for-every-self-hosted-app.md)) | 2026-09-14 | deployment, security, identity |
| [0016](0016-documentation-site.md) | A documentation site in Astro, published to GitHub Pages | Active | 2026-09-14 | documentation, tooling, deployment |
| [0017](0017-promote-goals-to-a-sidebar-row.md) | Promote Goals to a permanent sidebar row | Active (supersedes Goals' placement in [0014](0014-one-navigation-tree-at-every-width.md); History's placement here superseded by [0020](0020-history-sidebar-row-and-settings-screen.md)) | 2026-09-15 | ui, domain |
| [0018](0018-purge-local-replica-on-user-switch.md) | Purge the local replica and outbox when the signed-in user changes | Active | 2026-09-15 | offline, sync, identity |
| [0019](0019-created-at-on-todo-task-and-burndown-backfill.md) | Backfill TodoTask.CreatedAt from TaskCreated, and bump seq so delta sync delivers it | Active | 2026-09-16 | persistence, sync, analytics |
| [0020](0020-history-sidebar-row-and-settings-screen.md) | History as a sidebar row, theme and sync status into a Settings screen | Active (amends [0014](0014-one-navigation-tree-at-every-width.md); supersedes History's placement in [0017](0017-promote-goals-to-a-sidebar-row.md)) | 2026-09-16 | ui, identity |
| [0021](0021-drop-structurally-rejected-outbox-commands.md) | Drop structurally rejected outbox commands instead of retrying forever | Active (refines [0005](0005-offline-conflicts-last-write-wins.md)) | 2026-09-16 | architecture, offline, sync, contracts |
| [0022](0022-drawer-cleanup-settings-and-app-info-in-sidebar-area-fab.md) | Kill the dead account-menu arrow — Settings and App info become sidebar rows, area actions move to a FAB on the area page | Active (amends [0020](0020-history-sidebar-row-and-settings-screen.md); supersedes `ui-redesign-2-design.md` D6/D7 for area actions only) | 2026-09-17 | ui, identity, domain |
| [0023](0023-sidebar-search-removed-footer-added.md) | Pull local search out of the sidebar for now, give the drawer a footer | Active (amends [0022](0022-drawer-cleanup-settings-and-app-info-in-sidebar-area-fab.md); temporarily supersedes `ui-redesign-2-design.md`'s local-search decision) | 2026-09-17 | ui |
| [0024](0024-authentication-callback-route-off-appshell.md) | Keep the OIDC login-callback route off AppShell's layout | Active (consequence amended by [0027](0027-local-session-gates-the-app-not-the-access-token.md)) | 2026-09-17 | identity, sync, offline |
| [0025](0025-purge-on-any-user-mismatch-including-no-recorded-owner.md) | Purge local data on any signed-in/recorded-owner mismatch, including no recorded owner yet | Active (refines [0018](0018-purge-local-replica-on-user-switch.md)) | 2026-09-17 | offline, sync, identity |
| [0026](0026-one-shared-realm-for-every-self-hosted-app.md) | One shared Keycloak realm for every self-hosted application | Active (amends [0015](0015-one-self-host-compose-stack.md)) | 2026-09-21 | identity, deployment, architecture |
| [0027](0027-local-session-gates-the-app-not-the-access-token.md) | A durable local session, not the access token, decides whether the app opens | Active (amends [0024](0024-authentication-callback-route-off-appshell.md)'s consequence; sign-out amended by [0028](0028-sign-out-ends-the-keycloak-session-directly.md)) | 2026-09-22 | identity, offline, sync, ui |
| [0028](0028-sign-out-ends-the-keycloak-session-directly.md) | Sign-out ends the Keycloak session directly, and anonymous visitors land on a public welcome screen | Active (amends [0027](0027-local-session-gates-the-app-not-the-access-token.md)'s sign-out consequence) | 2026-09-23 | identity, offline, ui, security |
| [0029](0029-sync-revision-cascades-to-replica-backed-screens.md) | A sync revision cascades from AppShell so replica-backed screens redraw when data lands | Active (amended by [0030](0030-the-shell-waits-for-the-first-pull-on-a-device-holding-nothing.md)) | 2026-09-23 | ui, sync, offline |
| [0030](0030-the-shell-waits-for-the-first-pull-on-a-device-holding-nothing.md) | The shell waits for the first pull on a device holding nothing for this user | Active (amends [0029](0029-sync-revision-cascades-to-replica-backed-screens.md); mechanism corrected by [0031](0031-every-start-hands-back-its-own-pull.md)) | 2026-09-23 | ui, sync, offline, identity |
| [0031](0031-every-start-hands-back-its-own-pull.md) | Every Start hands back its own pull, because the shell mounts once signed out and once signed in | Active (amends [0030](0030-the-shell-waits-for-the-first-pull-on-a-device-holding-nothing.md); concurrency contract refined by [0032](0032-command-triggered-sync-coalesces-overlapping-runs.md)) | 2026-09-23 | sync, identity, ui, offline |
| [0032](0032-command-triggered-sync-coalesces-overlapping-runs.md) | Every accepted command triggers a sync, and overlapping runs coalesce | Active (refines [0031](0031-every-start-hands-back-its-own-pull.md)'s concurrency contract) | 2026-09-23 | sync, offline, ui, architecture |
| [0033](0033-one-fab-per-page-action-set.md) | One FAB per page's action set — zero, a plain FAB, or a FAB Menu | Active (amends [0022](0022-drawer-cleanup-settings-and-app-info-in-sidebar-area-fab.md)) | 2026-09-24 | ui |
| [0034](0034-account-deletion-bypasses-the-command-pipeline.md) | Account deletion bypasses the command pipeline for a generic cross-collection wipe | Active | 2026-09-24 | identity, persistence, architecture, security |
| [0035](0035-a-star-puts-a-task-on-today.md) | A star puts a one-off task on Today | Active (reverses AGENTS.md §10's star rule) | 2026-09-25 | domain, today |

Sourced from AGENTS.md §5 ("Architecture decisions") and the two design
specs in `specs/` that established and then revised them.
AGENTS.md stays the terse day-to-day reference (AD-1 … AD-9); this index is
where the reasoning and rejected alternatives behind each one live.
