# Docs site — design

Status: Proposed. Issue #110. No ADR: the site's stack (Astro on GitHub
Pages, `docs/`) and the self-hoster-facing rules in `AGENTS.md` §6 are
unchanged; this is content, structure and one compose override.

## 1. Problem and scope

The docs site at `psp515.com/pspad` reads like a changelog. The landing page
has twelve cards and a hand-drawn mock, the features page has twenty
paragraph-long cards, and the install page is 360 lines that mix the first
sign-in with proxy header semantics and a keyfile warning from the git
history. The README repeats the feature list a third time, so every shipped
feature is written three times and drifts. Nothing shows the real UI.

In:

- Three pages: **Home**, **Features**, **Self-host**. The old `install/`
  URL redirects to `self-host/`.
- One feature list in one data file, read by Home and Features.
- Real screenshots, captured by a repeatable script against the e2e stack.
- A self-host guide with options (bundled or hosted MongoDB, bundled or own
  Keycloak, reverse proxy or Cloudflare Tunnel) and the maintainer's own
  deployment as a worked, copyable example.
- README trimmed to point at the site.

Out:

- Renaming the product. The name stays PSPad; the tagline changes.
- Any change to the application, its manifest or its icons.
- Changing what `docker/compose.yaml` runs by default. The only edit to it
  is profiles on `mongo` and `keycloak` (§5.3); a first run is unchanged.
  The maintainer's deployment ships as an override file beside it.

## 2. Voice

First person, the maintainer speaking. Short sentences. Every page opens
with what the reader gets, not with how it is built. Technology names
appear on Self-host only, and only where the reader has to type them.

The one-paragraph pitch, reused on Home, in the README and as the site
description:

> PSPad is my personal notepad: money, tasks, ideas and the things my day
> keeps reminding me about, in one place, on my own server. It is a test
> project built with AI. Getting Things Done got me started, and I use it
> every day and plan to keep using it. Questions: psp515.com.

## 3. Home (`/`)

First screen, above the fold:

- Headline: *Money, tasks, ideas and your day, in one notepad you host.*
- The pitch paragraph from §2.
- Two buttons: **Self-host it** and **See the features**.
- One screenshot: My Day on a phone, light or dark following the site theme.

Second screen, "What it offers most", four blocks, each a two-line summary
and a screenshot, alternating sides on desktop and stacked on a phone:

| Block | Screenshot | Summary source |
|---|---|---|
| My Day | My Day, desktop | feature `today` |
| Inbox | Inbox with items, phone | feature `inbox` |
| Areas and lists | an area with lists, desktop | feature `areas` |
| Budgets | budget overview, desktop | feature `budgets` |

Third screen: "Run it yourself" with one sentence (one compose file, your
own VPS, about ten minutes), a button to Self-host, and a line linking the
Cloudflare Tunnel section as "how I host it".

Removed: the twelve cards, the CSS mock, the scroll cue, the hero-reveal
header animation, "How it goes". The sticky header stays.

Footer on every page: *PSPad is free software, GPL v3. Source on GitHub.
Questions: psp515.com.*

## 4. Features (`/features/`)

### 4.1 One data file

`docs/src/data/features.ts` exports an ordered array of:

```ts
interface Feature {
  id: string            // stable key, used by Home to pick highlights
  title: string         // two to four words
  does: string          // one sentence: what the user gets
  how: string           // one sentence: how it behaves, the rule that matters
  status: 'built' | 'planned'
}
```

Home reads the same file and picks `today`, `inbox`, `areas`, `budgets` by
id; a missing id fails the build. The Features page renders built features
first, then planned. The `in-progress` status is dropped: a feature is on
the page when it is usable, and the recurrence card that currently carries
it is built.

### 4.2 The list

Built, in this order. Each entry is the `does` sentence; the `how` sentence
is written at implementation time from the current UI and the specs, and
kept to one line.

1. **My Day** — Everything due today or earlier, from every area, with
   timed tasks first.
2. **Inbox** — Capture anything in one tap and sort it out later.
3. **Areas and lists** — Work, home, side projects, each with its own lists
   in your order.
4. **Tasks and steps** — Due dates, priorities, a goal and ordered steps on
   every task.
5. **Descriptions** — Markdown notes on every task and item, offline too.
6. **Repeats** — Daily to yearly, with a lead time, and never overdue.
7. **Goals** — One goal across areas, with the tasks that serve it.
8. **Reference lists** — Lists of things you look up, not do, with typed
   fields.
9. **Budgets** — Expenses, incomes and monthly balances in PLN and 28 other
   currencies.
10. **Spreadsheet import** — Bring a budget's history in from two CSV files.
11. **Share a list or budget** — A 30-minute invite link; whoever joins
    works with you, offline included.
12. **Public snapshots** — A read-only link anyone can open and tick, until
    it expires.
13. **Statistics** — Charts and a heatmap built from what actually happened.
14. **Works offline** — Opens with your data and syncs when you are back.
15. **Live updates** — Other devices and list members see changes within a
    moment.
16. **English and Polish** — One choice in Settings, followed on every
    device.
17. **Made for a phone** — A bottom bar with Inbox, Areas, My Day, Goals
    and Budgets.
18. **Yours** — Your server, your Keycloak, your data, as many users as you
    invite.

Planned, unchanged: Habits, Annual plans, Reminders, GitHub issues, Cloud
storage, 3D printing, Checklists.

The "Adding a language" developer section leaves the Features page; it
already lives in `specs/localization-design.md` §7 and the README's
Development section links there.

### 4.3 Keeping it current

`AGENTS.md` §6 "Docs ship with the change" is updated to name
`docs/src/data/features.ts`. A user-facing capability changes that file in
the same pull request; nothing else on the site lists features.

## 5. Self-host (`/self-host/`)

Order: what you need, quick start, options, how I host it, maintenance.
Every section is reachable from a short table of contents at the top.

### 5.1 What you need

A Linux machine with Docker Compose, a domain if anyone but you will reach
it, and about ten minutes. MongoDB and Keycloak are bundled; both can be
yours instead (§5.3).

### 5.2 Quick start

The existing five steps, cut to what you type and what you see:

1. Clone.
2. Copy `docker/.env.example` to `docker/.env`. The environment table is
   rendered from the file as today, but the page explains only the four
   URLs; everything else keeps its default on a first run.
3. `docker compose -f docker/compose.yaml up -d`.
4. Create a user in Keycloak at `localhost:8080`, realm `psplace`.
5. Open `localhost:5001` and sign in.

The two warnings that matter on day one stay as callouts: the Keycloak
redirect URI when the app address changes, and the 20 to 30 seconds before
the first sync.

### 5.3 Options

Each option is one subsection: when you want it, what changes, the exact
lines.

**MongoDB: bundled or hosted.** Hosted means any MongoDB 8 replica set, for
example an Atlas M0 free cluster: create the cluster, a database user with
`readWrite` on one database, allow the VPS address, copy the
`mongodb+srv://` string. In `docker/.env` set `MONGO_CONNECTION_STRING` to
it and remove `local-mongo` from `COMPOSE_PROFILES`.

This needs one change to `docker/compose.yaml`: the `mongo` service gets
`profiles: [local-mongo]`, the `keycloak` service gets
`profiles: [local-keycloak]`, and the API's `depends_on` on them moves
into the same profiles' services. `docker/.env.example` ships
`COMPOSE_PROFILES=local-mongo,local-keycloak`, so a first run starts the
same four containers as today; dropping a name from that line leaves the
service out. The API's connection string is built from
`MONGO_CONNECTION_STRING` when set, else from `MONGO_PASSWORD` as today.

**Keycloak: bundled or your own.** Your own means an existing Keycloak with
the realm imported from `docker/keycloak/realm-psplace.json`, or any realm
with a public client `pspad-frontend` and an audience `pspad-backend`.
Point `KEYCLOAK_AUTHORITY` and `KEYCLOAK_INTERNAL_AUTHORITY` at it, set the
admin credentials the API uses for account deletion, and drop
`local-keycloak` from `COMPOSE_PROFILES`.

**Reaching it from the internet.** Two paths, each a short list:

- *Reverse proxy on the VPS* (nginx, Caddy): three hostnames, TLS at the
  proxy, `X-Forwarded-For` appended, HSTS at the proxy,
  `API_TRUSTED_PROXY_NETWORK` set to the Docker subnet, port 5000 closed.
- *Cloudflare Tunnel*: no open ports, `cloudflared` beside the stack,
  `API_CLIENT_IP_HEADER=CF-Connecting-IP`, HSTS in the Cloudflare
  dashboard. The worked example is §5.4.

The CSP and CORS callout stays, shortened to its one sentence: the browser
only talks to the origins in `.env`.

### 5.4 How I host it

The maintainer's deployment on a small Polish VPS (Mikrus), written as
steps a reader can follow with their own domain. Reconstructed from the
running containers; secrets never appear.

Shape:

- Three compose projects on one shared external Docker network `proxy`:
  `pspad` (app, api), `keycloak` (Keycloak 26 on Postgres 18, data under a
  bind mount, realm imported from a folder), `cloudflare` (`cloudflared`
  with a tunnel token).
- MongoDB is an Atlas M0 cluster. No `mongo` service.
- Nothing is published to the internet. App, api and Keycloak bind to
  `127.0.0.1` only; `cloudflared` reaches them by container name over
  `proxy`.
- Three public hostnames in the Zero Trust dashboard: `pspad.<domain>` to
  `app:8080`, `pspad-api.<domain>` to `api:8080`, `auth.<domain>` to
  `keycloak:8080`.
- API: `API_TRUSTED_PROXY_NETWORK` is the `proxy` subnet,
  `API_CLIENT_IP_HEADER=CF-Connecting-IP`, CORS allows the app origin.
- Keycloak: `KC_HOSTNAME=https://auth.<domain>`, `KC_HOSTNAME_STRICT=true`,
  `KC_HTTP_ENABLED=true`, `KC_PROXY_HEADERS=xforwarded`, `start
  --import-realm`.
- Images pinned to exact versions, as `docker/compose.yaml` already does.

Shipped files, so the example is copyable rather than retyped:

- `docker/compose.cloudflare.yaml`: an override that adds a `cloudflared`
  service reading `CLOUDFLARE_TUNNEL_TOKEN` from `.env` as an environment
  variable, not a command-line argument, joins every service to an external
  `proxy` network, removes the published ports, and leaves out `mongo`
  through the profile from §5.3.
- `docker/.env.example`: `CLOUDFLARE_TUNNEL_TOKEN` and
  `MONGO_CONNECTION_STRING`, with comments in the existing style.

Steps on the page:

1. Create the Atlas cluster and copy the connection string.
2. Create the tunnel in Zero Trust, copy its token, add the three public
   hostnames.
3. `docker network create proxy`.
4. Fill `.env`: the four public `https://` URLs, the connection string, the
   token, the proxy subnet.
5. `docker compose -f docker/compose.yaml -f docker/compose.cloudflare.yaml
   up -d`.
6. In Keycloak at `https://auth.<domain>`, set the client's redirect URI to
   the app URL, create the first user.
7. Turn on HSTS in Cloudflare, SSL/TLS → Edge Certificates.

Keycloak on Postgres as a separate project stays prose, with the five
`KC_*` values above, because the bundled Keycloak in `compose.yaml` runs in
development mode on its own store and that is enough for most self-hosters.

### 5.5 Maintenance

Backups, upgrading and restarting, and the optional database user, moved
from the install page unchanged in substance, under one heading at the end.
The two "worth knowing" warnings (Keycloak in development mode, the old
keyfile in git history) merge into one short callout under Backups.

## 6. Screenshots

`test/e2e/screenshots.ts`, run as `npm run screenshots` in `test/e2e`
against the e2e stack. It is a Playwright test file in its own project in
`playwright.config.ts` (`screenshots`, not matched by `npm test`), so it
reuses the account fixture, `signIn`, and the helpers in `test/e2e/helpers`.

One test seeds a fresh user with sample content in English: two areas with
two lists each, eight tasks including one timed, one overdue, one recurring
and one with steps, three inbox items, one goal with two linked tasks, one
budget with six entries this month. It then captures, with
`page.screenshot`, light then dark, into `docs/public/screens/`:

| File | View | Viewport |
|---|---|---|
| `my-day-phone-{light,dark}.png` | My Day | 390×844 |
| `my-day-{light,dark}.png` | My Day | 1280×800 |
| `inbox-phone-{light,dark}.png` | Inbox | 390×844 |
| `area-{light,dark}.png` | an area | 1280×800 |
| `budget-{light,dark}.png` | Budget overview | 1280×800 |

Theme is switched through Settings so the capture uses the app's own
palette. Names, amounts and dates are fixed strings; dates are relative to
the day of capture so "today" reads as today. The PNGs are committed. The
site shows the light or dark file following its own theme toggle, through
`<picture>` with a `prefers-color-scheme` source and a `data-theme`
override in CSS.

The script is documented in `test/e2e/README.md` and runs on demand only;
CI does not run it.

## 7. README

The About section becomes the pitch from §2 and the four highlight titles
with one line each, then a link to the Features page. The Documentation
section lists the three pages. Development is unchanged except for a link
to `specs/localization-design.md` for adding a language.

## 8. Testing and acceptance

- `npm run build` in `docs/` passes; the docs workflow already runs it on
  every pull request.
- `docker compose -f docker/compose.yaml -f docker/compose.cloudflare.yaml
  --env-file docker/.env.example config` renders without error; with
  `COMPOSE_PROFILES=` empty it has no `mongo` or `keycloak` service, and
  with the shipped `.env.example` it has both.
- The screenshot script runs once on the maintainer's box against the e2e
  stack and produces the ten files.
- `features.ts` has an entry for every card the Features page showed on
  `origin/main` at the time of writing, plus none that the app lacks.
- `/install/` responds with a redirect to `/self-host/`.
