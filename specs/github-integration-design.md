# GitHub integration — design

Status: approved design, not yet built. Decisions of record:
[ADR-0045](../adr/0045-each-integration-is-its-own-module.md),
[ADR-0046](../adr/0046-github-app-read-only-tokens-outside-the-log.md).

First external sync (AGENTS.md §9, item 5). Each user connects their own
GitHub account; chosen repositories become lists holding the repository's
open issues and pull requests. GitHub owns what an issue *is*; PSPad owns how
the user *plans* it.

## 1. Scope

In:

- Many users per instance, each connecting their own GitHub account.
- Read-only mirror of **all open issues and pull requests** of repositories
  the user picks in PSPad.
- One managed area **GitHub** per user; one list per connected repository.
- Local planning on mirrored tasks: due date, priority, star, goal, steps —
  so an issue reaches Today like any task.
- Closing on GitHub closes or removes the task (§4.3).
- Background polling, interval configurable per instance, plus a manual
  refresh.

Out:

- Any write to GitHub (closing, commenting, creating issues from PSPad or the
  Inbox).
- Webhooks — a self-hosted instance is usually not reachable from GitHub.
- Filters per repository ("only mine"), moving repository lists to other
  areas.
- Personal access tokens as a fallback auth path.
- Full reconciliation of issues deleted on GitHub (§5.5).

## 2. Authentication — GitHub App

One GitHub App per instance, registered once by the self-hoster, with
**read-only** repository permissions: Issues: read, Pull requests: read,
Metadata: read. "Request user authorization (OAuth) during installation" is
on. Callback URL: `https://<api host>/api/github/callback`. The callback is a
browser redirect, so a LAN-only address works.

Configuration (`docker/.env.example`, each with a `#` description):

| Variable | Meaning |
|---|---|
| `GITHUB_APP_CLIENT_ID` | GitHub App client id |
| `GITHUB_APP_CLIENT_SECRET` | GitHub App client secret |
| `GITHUB_APP_SLUG` | App slug, used to build the installation URL |
| `GITHUB_TOKEN_ENCRYPTION_KEY` | Base64 AES-256 key encrypting stored tokens |
| `GITHUB_SYNC_INTERVAL_MINUTES` | Per-user polling interval, default `60` |

Missing any of the first four disables the integration: no endpoints, no
worker, and Settings hides the GitHub section.

### 2.1 Connect flow

1. Client calls `POST /api/github/connect` (JWT). API stores a random
   `state` bound to the caller's `userId` (collection `github_oauth_states`,
   TTL 10 minutes) and returns
   `https://github.com/apps/{slug}/installations/new?state={state}`.
2. Browser goes to GitHub; the user installs the app on chosen repositories
   (or all) and authorizes it.
3. GitHub redirects to `GET /api/github/callback?code=…&state=…`. The call
   carries no JWT: `state` alone identifies the user and is consumed once.
   API exchanges `code` at `https://github.com/login/oauth/access_token` for
   an access token (8 h) and refresh token (6 months), stores the connection,
   and redirects to the client's `/settings/github`.

Invalid, expired or reused `state` → `400`, nothing stored.

### 2.2 Storage

`github_connections`, one document per user:

| Field | Notes |
|---|---|
| `userId` | owner, unique |
| `gitHubUserId`, `login` | shown in Settings |
| `accessToken`, `refreshToken` | AES-GCM ciphertext with nonce |
| `accessTokenExpiresAt`, `refreshTokenExpiresAt` | |
| `status` | `Active` or `NeedsReconnect` |
| `nextSyncAt`, `lastSyncedAt` | worker scheduling |

Not an aggregate. No events, no `seq`, never in the sync pull, never in the
client replica. Tokens therefore never enter the event log or the browser.

### 2.3 Refresh

Before any GitHub call, if the access token expires within 5 minutes, refresh
it (`grant_type=refresh_token`) and store the rotated pair. A failed refresh
or a `401` sets `NeedsReconnect` and stops that user's sync; mirrored data
stays. Settings and the GitHub area show a "Reconnect" prompt, which runs the
connect flow again.

### 2.4 Disconnect and account deletion

`DELETE /api/github`: revoke the token
(`DELETE /applications/{client_id}/grant`), delete `github_connections` and
`github_repositories` for the user, issue `DropMirroredArea` (cascades to
lists and tasks, ADR-0042). Uninstalling the app from the GitHub account is
the user's step; Settings links to it.

Account deletion (ADR-0034, ADR-0041) already sweeps every collection by
`userId`; it additionally attempts the token revoke first. A failed revoke is
logged and never blocks deletion.

## 3. Domain changes — `PSPad.Module.Tasks`

Generic, source-agnostic concepts; no GitHub type or string constant lives in
Tasks. The next integration reuses them.

- `ExternalRef` record on `TodoTask` (nullable): `Source` (`"github"`),
  `Key` (`owner/repo#42`), `Kind` (`Issue` | `PullRequest`), `Number`, `Url`,
  `Closed` (bool, for the row icon).
- `TaskList.ExternalSource` (nullable): `Source`, `Key` (repository id as
  string), `DisplayName` (`owner/repo`).
- `Area.Managed` (nullable source name): set on the area an integration owns.

### 3.1 Field ownership on a mirrored task

| Field | Owner | User command |
|---|---|---|
| Name | GitHub | `RenameTask` rejected |
| Completion | GitHub | `CompleteTask`, `ReopenTask` rejected |
| Existence | GitHub | `DeleteTask` rejected |
| List, recurrence | — | `MoveTaskToList`, `SetRecurrence` rejected |
| Due date, priority, star, goal, steps | user | accepted |

### 3.2 Lists and areas

- `CreateTask` into a mirrored list — rejected. Organizing an Inbox item into
  a mirrored list — rejected.
- `RenameTaskList`, `MoveTaskListToArea`, `DeleteTaskList` on a mirrored list
  — rejected.
- `DeleteArea` on a managed area — rejected. `RenameArea` — accepted.
- Moving a normal task into a mirrored list — rejected.

### 3.3 Server-only commands

New marker `IServerOnlyCommand : ICommand` in `PSPad.Abstractions`.
`CommandCatalogue` excludes every type implementing it, so none can arrive
over `/api/commands`; they still go through `Decide` in Tasks, one rule set.

| Command | Effect | Event |
|---|---|---|
| `CreateMirroredArea` | managed area | `AreaCreated` (with `Managed`) |
| `CreateMirroredList` | list with `ExternalSource` | `TaskListCreated` (with source) |
| `MirrorTask` | create, update title/ref, or restore a dropped task with cleared planning fields | `TaskCreated` / `MirroredTaskUpdated` / `MirroredTaskRestored` |
| `CloseMirroredTask` | complete | `TaskCompleted` |
| `ReopenMirroredTask` | reopen | `TaskReopened` |
| `DropMirroredTask` | delete | `TaskDeleted` |
| `RenameMirroredList` | follow repository rename | `TaskListRenamed` |
| `DropMirroredList` | delete list, cascades | `TaskListDeleted` |
| `DropMirroredArea` | delete area, cascades | `AreaDeleted` |

Reusing `TaskCompleted`, `TaskReopened` and `TaskDeleted` means Statistics
counts merged PRs and completed issues with no change. Folders follow
AGENTS.md §11 (`Tasks/Mirror/MirrorTask.cs`, …).

The client runs the same `Decide`, so offline edits to a mirrored task's
title are rejected locally too. New fields ride the existing documents, so
delta sync and the replica need no mechanism change.

## 4. Sync — `PSPad.Module.GitHub`

### 4.1 Repository selection (Settings, online only)

- `GET /api/github/repositories` — repositories from the user's app
  installations (`GET /user/installations`,
  `GET /user/installations/{id}/repositories`), each flagged
  connected / not connected / inaccessible.
- `POST /api/github/repositories/{repoId}` — store a link in
  `github_repositories` (`userId`, `repoId`, `fullName`, `listId`, `etag`,
  `lastSyncedAt`, `status`), issue `CreateMirroredArea` if the user has none,
  `CreateMirroredList`, and sync that repository immediately.
- `DELETE /api/github/repositories/{repoId}` — `DropMirroredList`, delete the
  link.

Keyed by `repoId`, never name: a renamed or transferred repository keeps its
list, and `RenameMirroredList` follows `fullName`.

### 4.2 Worker

`GitHubSyncWorker : BackgroundService`, ticking every minute. Picks
connections with `status = Active` and `nextSyncAt <= now`, one user at a
time, then sets `nextSyncAt = now + GITHUB_SYNC_INTERVAL_MINUTES`. An
exception for one user is logged and never stops others.

Per connected repository:

- First sync: `GET /repos/{owner}/{repo}/issues?state=open&per_page=100`,
  all pages. No historical import.
- Later: `…?state=all&since={lastSyncedAt}`, with `If-None-Match: {etag}` on
  the first page. `304` → nothing to do, costs no rate limit.

The issues endpoint returns pull requests too (`pull_request` present,
`pull_request.merged_at` for merged).

### 4.3 Mapping

Task id = UUIDv5(`userId`, `repoId`, `number`). Command id = UUIDv5(task id,
command name, GitHub `updated_at`), so a retried run replays into existing
idempotency (`processed_commands`).

| GitHub | Task | Command |
|---|---|---|
| open | none | `MirrorTask` (create) |
| open | exists, title or ref changed | `MirrorTask` (update) |
| open | completed | `ReopenMirroredTask` |
| open | dropped | `MirrorTask` (restore, planning fields cleared) |
| closed, `state_reason = completed`, or PR merged | open | `CloseMirroredTask` |
| closed, `state_reason = not_planned`, or PR closed unmerged | exists | `DropMirroredTask` |
| closed | none | nothing |

Commands run through the same `ICommandHandler<T>` and unit of work as wire
commands, one DI scope each, so each lands in its own transaction with its
events and `seq` bump. The client picks the result up through normal delta
sync.

### 4.4 Errors and limits

| Condition | Response |
|---|---|
| `401`, failed refresh | `NeedsReconnect`, user's sync stops |
| `404` / `403` on one repository | link `Inaccessible`, list kept, shown in picker |
| `X-RateLimit-Remaining` near 0, `Retry-After`, secondary limit | `nextSyncAt` moved to the reset time |
| network failure | logged, retried at next interval |

### 4.5 Manual refresh

`POST /api/github/sync` sets `nextSyncAt = now` and wakes the worker;
throttled to once a minute per user. It returns when that user's run
finishes; the client then triggers its normal pull.

### 4.6 Known limitation

An issue deleted on GitHub never appears in a `since` query; its task stays
until the repository is disconnected. Accepted for now.

## 5. Module layout and boundaries

```
src/modules/PSPad.Module.GitHub/
  Connection/      connect, callback, refresh, disconnect, token cipher
  Repositories/    installation listing, link / unlink
  Sync/            worker, issue fetch, state → command mapping
  Endpoints/       /api/github/* route group
```

- References `PSPad.Abstractions`, `PSPad.Module.Tasks`,
  `PSPad.Contracts`. Not `PSPad.Infrastructure`, not MongoDB. Stores
  (`IGitHubConnectionStore`, `IGitHubRepositoryLinkStore`,
  `IGitHubOAuthStateStore`) are interfaces here, implemented over Mongo in
  `PSPad.Api`, as Statistics does.
- One integration, one module. Only GitHub-specific code lives here; the
  generic mirror concepts stay in Tasks and Abstractions for the next
  integration.
- Architecture guards: Tasks never references GitHub; GitHub never
  references Statistics or Infrastructure; every mirror command implements
  `IServerOnlyCommand`.
- `PSPad.Api` composes DI and registers endpoints and worker only when §2's
  configuration is complete. Indexes: `github_connections.userId` unique,
  `github_connections.nextSyncAt`, `github_repositories (userId, repoId)`
  unique, `github_oauth_states.createdAt` TTL.
- Wire DTOs (connection status, repository rows) in `PSPad.Contracts`.

## 6. UI

Follows `specs/ui-spec.md`; MudBlazor components only.

- **Settings → GitHub.** Not connected: "Connect GitHub". Connected: `@login`,
  last sync, "Refresh now", "Disconnect", then the repository list with a
  `MudSwitch` per row, an `Inaccessible` chip where relevant, and a "Manage
  access on GitHub" link. `NeedsReconnect`: warning and "Reconnect". Offline:
  "Needs a connection".
- **GitHub area.** No delete action in its FAB. `NeedsReconnect` banner under
  the header.
- **Repository list.** No add-task FAB; no rename, move or delete. "Refresh
  now" action.
- **Task row (lists, Today).** Issue or PR icon, open or closed, plus `#42`,
  in place of the checkbox. The icon opens the GitHub URL in a new tab.
- **Task details.** Title read-only; "Open on GitHub" link; date, priority,
  star, goal, steps editable.

## 7. Testing

Unit — `PSPad.Module.Tasks.Tests`:

- Every rejection in §3.1–3.2.
- Mirror commands: create, update, close, reopen, drop, restore with cleared
  planning fields.
- Today rule with a mirrored task due today, and with a due step.
- `CommandCatalogue` resolves no `IServerOnlyCommand`.

Unit — `PSPad.Module.GitHub.Tests` (new):

- §4.3's table, row by row.
- Deterministic task and command ids.
- Token cipher round trip; wrong key fails.
- Refresh threshold; rate-limit rescheduling. GitHub faked with an
  `HttpMessageHandler`, no network.

Integration — `PSPad.Api.Tests` (Testcontainers, GitHub faked):

- Callback: valid `state` stores a connection; reused or expired `state` →
  `400`.
- Full run: open → closed completed → reopened → not planned; documents,
  `TaskCompleted` in `events`, Statistics records.
- `304` issues no command.
- Disconnect removes connection, links, area, lists, tasks.
- Account deletion clears `github_connections` and `github_repositories`.
- A mirror command posted to `/api/commands` is rejected as unknown.
- Token plaintext never appears in `events` or in a sync pull.

bUnit — `PSPad.App.Tests`:

- Settings → GitHub in each state: not connected, connected,
  `NeedsReconnect`, offline.
- Mirrored task row shows the icon, not a checkbox.
- Mirrored list has no add FAB; GitHub area has no delete action.

Manual: one smoke run against a real GitHub App before merge.

## 8. Docs and records

- `docs/src/pages/install.astro`: "Register a GitHub App" — permissions,
  callback URL, the `GITHUB_*` variables.
- `features.astro` and landing page: GitHub lists.
- `specs/backend-spec.md` §10: GitHub integration leaves "out of scope";
  command pipeline gains `IServerOnlyCommand`.
- AGENTS.md: §2 subsystem 6 partially in; §5 AD-11; §6 new module and the
  second module-to-module edge.
- ADR-0045 and ADR-0046 move from `Proposed` to `Active` when built.
