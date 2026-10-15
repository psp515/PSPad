# Live updates — design

Status: Built (issue #105). The reasoning is in
`adr/0075-open-clients-get-a-sync-nudge-over-sse.md`.

## 1. Problem and scope

A change made elsewhere reaches an open client only on its next sync: the
60-second poll, app start, reconnect, or a command of its own (ADR-0032).
Sharing makes the gap visible: a visitor ticks a book on a public snapshot
and the owner's open app shows the chip up to a minute later; a member edits a
shared list another member has open; one user works on two devices.

In:

- The server tells every open client whose data a commit touched that it
  should sync. The message carries no data. The client runs its existing
  guarded `SyncNowAsync()`.
- Snapshot marks (`specs/sharing-spec.md` §6.4) are the first consumer, and
  every other commit uses the same path.

Out:

- Pushing data. Sync stays the only way data arrives (AD-6).
- Live updates on the public snapshot page. Visitors are anonymous and the
  page is a frozen copy; they reload.
- Nudges for writes outside the command pipeline: account deletion
  (ADR-0034) and startup backfills.
- More than one API instance. The fan-out is in memory (§8).

A missed nudge costs nothing but the old poll latency. The 60-second poll
keeps running unchanged.

## 2. Transport

Server-Sent Events over a plain HTTP GET, `GET /api/live`, inside the
authorized `/api` group. No SignalR, no WebSocket, no third-party package on
either side.

- The browser's `EventSource` cannot send an `Authorization` header, so the
  client reads the response as a stream through `HttpClient` with
  `SetBrowserResponseStreamingEnabled(true)`, through the same
  `SessionAuthorizationHandler` every other call uses. The token never goes
  into a URL.
- The server writes the frames itself; the format is small enough that a
  framework abstraction buys nothing and hides flushing and headers.

### 2.1 Wire format

UTF-8, `\n` line endings, each frame ended by a blank line:

| Frame | Bytes | When |
|---|---|---|
| ready | `event: ready\ndata: ready\n\n` | first, as soon as the stream opens |
| sync | `event: sync\ndata: sync\n\n` | a commit touched this user's data |
| heartbeat | `: ping\n\n` | after 25 s without another frame |

`ready` makes the response headers leave at once, so the client's 10-second
stalled-request guard never fires on a healthy stream, and it tells the client
the connection is up (§5.2).

### 2.2 Response headers

```
Content-Type: text/event-stream
Cache-Control: no-cache, no-transform
X-Accel-Buffering: no
```

`no-transform` stops intermediaries from compressing the stream (compression
buffers it). `X-Accel-Buffering: no` turns nginx's proxy buffering off for this
response only. The endpoint also calls
`IHttpResponseBodyFeature.DisableBuffering()` and flushes after every frame.

### 2.3 Stream lifetime

The server ends the stream at the earliest of:

- the access token's `exp` claim;
- 15 minutes after it opened (`Live:MaxStream`);
- the client going away (`RequestAborted`);
- the host stopping (`ApplicationStopping`), so a shutdown is not held up by
  open streams.

The client reconnects at once after a clean end, with a fresh token from the
usual refresh path. A token revoked mid-stream therefore keeps receiving
data-free nudges for at most the token's remaining life; every sync it
triggers still goes through normal authorization.

### 2.4 Limits

- At most 10 open streams per user (`Live:MaxStreamsPerUser`); the 11th gets
  `429`. Ten covers several devices with a few tabs each.
- Opening a stream costs one token from the user's global `Api` bucket
  (ADR-0062), like any request. Reconnect backoff (§5.3) keeps a failing
  client well under it.

## 3. Proxies and Cloudflare

The stack serves plain HTTP; the self-hoster's own proxy sits in front of the
API (`docs/src/pages/install.astro`, "Putting it behind a proxy"). The headers
and heartbeat in §2 are chosen so stock proxy settings carry the stream.

**Cloudflare (proxied DNS or Tunnel): no change required.** Cloudflare streams
`text/event-stream` unbuffered. It drops an origin connection silent for 100 s
(error 524); the 25 s heartbeat keeps it far below that. Browsers talk HTTP/2
to Cloudflare, so a stream does not use up the per-host connection limit. Only
if a Cache Rule caches the API hostname, exclude `/api/live` (better: never
cache the API).

**nginx in front of the API: no change required with stock directives.**
Change only if the config has one of these:

| If your config has | Change it to |
|---|---|
| `proxy_read_timeout` below 30s | ≥ 60s, at least for `location /api/live` |
| `proxy_ignore_headers X-Accel-Buffering` | remove it, or add `proxy_buffering off;` in `location /api/live` |
| `proxy_cache` on the API | `proxy_cache off;` for `/api/live` |
| `gzip_types` including `text/event-stream` | remove it |
| TLS without `http2` and no Cloudflare in front | `http2 on;` — on HTTP/1.1 each open tab holds one of the browser's six connections to the API |

An explicit block that does not rely on the response headers:

```nginx
location /api/live {
    proxy_pass http://<api-upstream>;
    proxy_buffering off;
    proxy_cache off;
    proxy_read_timeout 1h;
    proxy_http_version 1.1;
    proxy_set_header Connection "";
}
```

**Caddy: no change required.** It flushes `text/event-stream` immediately.

`install.astro` carries this section verbatim. An integration test runs the
stream through `nginxinc/nginx-unprivileged:1.31.6-alpine` with a stock
`proxy_pass` block (§7). The Cloudflare path is checked by hand on a real
deployment before merge.

## 4. Fan-out

### 4.1 Hook

`MongoUnitOfWork.CommitAsync` calls
`ICommitObserver.CommittedAsync(actorId, changes, CancellationToken.None)`
after `CommitTransactionAsync` returns and after the domain event dispatch,
for every committed unit of work, including unlogged-only ones (snapshot
marks, ADR-0064). The event pump (ADR-0036) is not the source: it never sees
unlogged events, so it would miss the first consumer. `CommittedChange` is
`(Aggregate Aggregate, IReadOnlyList<DomainEvent> Events)`.

A throwing observer is caught and logged at Error, the same way as the
dispatcher: the transaction already committed and the command stays
accepted. A replayed command id (the `already` branch) commits nothing and
notifies nobody.

`ICommitObserver` lives in `PSPad.Abstractions`; Infrastructure still
references no module. The Api's implementation, `LiveNudges`, resolves the
audience and hands it to `LiveConnections`.

### 4.2 Audience

`LiveAudience` computes the set of users to nudge for one commit. It mirrors
`SyncReader`'s visibility rules; a change to who can read an aggregate in sync
must change the audience too.

| Aggregate | Audience |
|---|---|
| any | the actor, and the aggregate's `UserId` (owner) |
| `TaskList` | + its current members, + `MemberId` of any `ListMemberRemoved` in the commit |
| `TodoTask`, `ReferenceItem` | + owner and members of the list `ListId` names, and of `PreviousListId` |
| `Budget` | + its current members, + `MemberId` of any `BudgetMemberRemoved` in the commit |
| `MoneyEntry`, `BalanceSnapshot` | + owner and members of the budget `BudgetId` names |

List and budget lookups for dependents go through `IContainerMembers` (one
Mongo read per kind per commit, deleted containers included so a cascade
still reaches members). `Guid.Empty` is never in the audience.

Accepted gaps, recorded in ADR-0075: `TaskListSharingStopped` and
`BudgetSharingStopped` carry no member list, so former members learn on their
next poll; a member who leaves is the actor and is nudged anyway.

### 4.3 Connections

`LiveConnections` is a singleton map `userId → open streams`. Each stream owns
a bounded channel of capacity 1 with `BoundedChannelFullMode.DropWrite`: any
number of nudges between two reads collapse into one `sync` frame. `Nudge`
never blocks a commit.

The device that sent a command also gets its own echo and runs one extra
pull. That is harmless and not filtered.

## 5. Client

### 5.1 Parts

- `ILiveStream` / `LiveStreamClient` — opens `GET api/live` with
  `HttpCompletionOption.ResponseHeadersRead` and browser streaming on, through
  `ServerReachabilityHandler`, `SessionAuthorizationHandler` and
  `StalledRequestHandler`. Non-success status throws `HttpRequestException`.
- `SseReader` — turns the byte stream into frame names: `ready`, `sync`, or
  `heartbeat` for a comment line. Handles frames split across reads and
  `\r\n`.
- `IPageVisibility` / `BrowserPageVisibility` — `document.visibilityState`
  through `wwwroot/js/visibility.js`.
- `LiveUpdates` — the loop below. Started by `AppShell` after
  `Coordinator.Start()` for a signed-in session; disposed with the scope.

### 5.2 Loop

1. Wait while offline (`IConnectivity.IsOnline`) or hidden
   (`IPageVisibility.IsVisible`); either changing wakes the loop.
2. Open the stream. On `ready` and on every `sync`, call
   `ISyncTrigger.SyncNowAsync()` without awaiting it. `ready` covers anything
   missed while disconnected.
3. Silence: no frame for 60 s means the stream is dead; drop it and reconnect.
4. Going offline or hidden cancels the open stream.
5. A clean end after `ready` (the server closed the stream) reconnects at
   once and resets the failure count. A stream that ends without reaching
   `ready`, an exception, a non-success status, or silence is a failure and
   waits the backoff first.

### 5.3 Backoff

`min(60 s, 1 s × 2^(failures−1))`, multiplied by a jitter factor in
`[0.5, 1.0)`. Reset to zero after any stream that reached `ready`.

## 6. Configuration

`Live` section in the API's configuration, defaults in code, not surfaced in
`docker/.env.example` (nothing a self-hoster needs to tune):

| Key | Default |
|---|---|
| `Live:Heartbeat` | `00:00:25` |
| `Live:MaxStream` | `00:15:00` |
| `Live:MaxStreamsPerUser` | `10` |

No new service, port or environment variable.

## 7. Testing

Unit (`[UnitTest]`):

- `SseFrames` writes the exact bytes of §2.1.
- `LiveStreamEnd` picks the earlier of `exp` and the maximum.
- `LiveConnections`: a nudge reaches only that user's streams; nudges collapse;
  the cap; disposing a subscription removes it.
- `LiveAudience` for every row of §4.2, with a fake `IContainerMembers`.
- `SseReader`: frames, comments, split chunks, CRLF.
- `LiveUpdates`: `ready` and `sync` trigger a sync; failures back off and cap
  at 60 s; a clean end reconnects without waiting; offline and hidden pause
  and resume; silence reconnects.

Integration (`[IntegrationTest]`, Testcontainers):

- `MongoUnitOfWork` calls the observer after commit, with unlogged-only
  commits too, and a throwing observer leaves the command committed.
- `/api/live`: headers and `ready`; a user's own command nudges their stream;
  a member's edit nudges the owner; a snapshot mark nudges the owner; a
  removed member is nudged; a stranger gets only heartbeats; the stream ends
  at `Live:MaxStream`; the eleventh stream gets 429.
- Through nginx: `ready`, a `sync` after a command, and heartbeats keep a
  stream alive past a short `proxy_read_timeout`, all with a stock
  `proxy_pass` block.

## 8. Consequences for later work

A second API instance needs a backplane before live updates work across
instances; MongoDB change streams on the replica set are the obvious one and
would also cover writes outside the command pipeline. Until then a nudge
reaches only streams on the instance that committed; the poll still delivers.
