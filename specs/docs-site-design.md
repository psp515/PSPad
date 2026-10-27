# Docs site — design

Status: Built. Issue #110. No ADR: the site's
stack (Astro on GitHub Pages, `docs/`) and the self-hoster-facing rules in
`AGENTS.md` §6 are unchanged; this is content, structure and a small
library that assembles self-host stacks.

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
- A self-host page that is a guided walkthrough: pick Quick Test or VPS
  Hosting, then one step at a time, with every file assembled for the
  choices made and every command copyable.
- README trimmed to point at the site.

Out:

- Renaming the product. The name stays PSPad; the tagline changes.
- Any change to the application, its manifest or its icons.
- Any change to what `docker/compose.yaml` runs. It is the development
  stack and the e2e stack; self-hosters never touch it. The profiles,
  `compose.cloudflare.yaml`, `COMPOSE_PROFILES`, `MONGO_CONNECTION_STRING`
  and `CLOUDFLARE_TUNNEL_TOKEN` added in the first round of this work are
  removed again; the `docker/` folder matches `main` apart from the
  comment in `mongo-app-user.js`.

## 2. Voice

First person, the maintainer speaking. Short sentences. Every page opens
with what the reader gets, not with how it is built. Technology names
appear on Self-host only, and only where the reader has to type them.
"Docker Compose", never "Docker Compose v2".

The one-paragraph pitch, reused on Home, in the README and as the site
description:

> PSPad is my personal notepad: money, tasks, ideas and the things my day
> keeps reminding me about, in one place, on my own server. It is a test
> project built with AI. Getting Things Done got me started, and I use it
> every day and plan to keep using it. Questions: psp515.com.

## 3. Home (`/`)

### 3.1 First screen

From 820px up the hero fills the viewport below the sticky header
(`min-height: calc(100dvh - header height)`) and nothing of the next section
shows until the reader scrolls; on a phone it flows naturally with the cue
after the screenshot. In it:

- Headline: *Money, tasks, ideas and your day, in one notepad you host.*
- The pitch paragraph from §2.
- One line in the accent colour: *Works offline. Opens with your data and
  syncs when you are back.*
- Two buttons: **Self-host it** and **See the features**.
- One screenshot: My Day on a phone, light or dark following the site theme.
- At the bottom edge, centred, a scroll cue: the words *See what it offers*
  over a chevron, gently bobbing, linking to `#offers`. It is the only
  animation that runs without scrolling.

### 3.2 Below the fold

"What it offers" (`#offers`), five blocks. The first four are a two-line
summary and a screenshot, alternating sides on desktop and stacked on a
phone. The fifth is a full-width band with an icon and no screenshot.

| Block | Screenshot | Summary source |
|---|---|---|
| My Day | My Day, desktop | feature `today` |
| Inbox | Inbox with items, phone | feature `inbox` |
| Areas and lists | an area with lists, desktop | feature `areas` |
| Budgets | budget overview, desktop | feature `budgets` |
| Works offline | none | feature `offline` |

Then "Run it yourself": *Your own VPS, a few files, about thirty minutes.* and a
**Self-host it** button. Nothing else; the "how I host it" line is gone.

### 3.3 Scroll reveal

Every block below the hero starts invisible and shifted down 24px, and
transitions to its place when it enters the viewport, once. An
`IntersectionObserver` in the page adds a class; CSS does the rest. With
`prefers-reduced-motion: reduce` nothing moves and nothing is hidden, and
without JavaScript nothing is hidden either (the hidden state is applied
only under a `js` class the script sets on `<html>`).

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
}
```

Home reads the same file and picks `offline`, `today`, `inbox`, `areas`,
`budgets` by id; a missing id fails the build. The Features page renders
`features`, then `planned`.

### 4.2 The list

Built, in this order. `does` then `how`.

1. **Works offline** — Opens with your data and syncs when you are back. —
   Every screen says when it last updated. Pull down on a phone or press
   refresh on a computer. A device untouched for 7 days asks you to sign in
   again.
2. **My Day** — Everything due today or earlier, from every area, on one
   screen. — unchanged.
3. **Inbox** — Capture anything in one tap and sort it out later. — An item
   becomes a full task by setting list, date, priority, goal, steps, or just
   remove it.
4. to 18. — the remaining entries, unchanged and in their current order:
   Areas and lists, Tasks and steps, Descriptions, Repeats, Goals,
   Reference lists, Budgets, Spreadsheet import, Share a list or budget,
   Public snapshots, Statistics, Live updates, English and Polish, Made for
   a phone, Yours.

Planned, unchanged: Habits, Annual plans, Reminders, GitHub issues, Cloud
storage, 3D printing, Checklists.

### 4.3 Keeping it current

`AGENTS.md` §6 "Docs ship with the change" names
`docs/src/data/features.ts` for capabilities and
`docs/src/data/stack/` for anything a self-hoster runs (images,
environment variables, ports, services).

## 5. Self-host (`/self-host/`)

One page. At the top a one-line intro and a selector with two cards,
**Quick Test** and **VPS Hosting**. Below it a stepper: the step list on
the left (numbered, current step filled, done steps ticked), the current
step on the right, **Back** and **Next** under it. **Back** is absent on the
first step; on the last step **Next** reads **Done** and returns to the top of the page. On a phone the step list
is a row of numbered buttons; the panel's first line says which step it is. Clicking a step in the list
jumps to it.

State lives in the URL query (`?scenario=vps&step=5&kc=pg&mongo=atlas&expose=tunnel`)
through `history.replaceState`, so a step is linkable and reload keeps the
place. Defaults: `quick`, step 1, `h2`, `vps`, `tunnel`.

Every command and file sits in a block with a **Copy** button that writes
the block's text to the clipboard and reads *Copied* for a moment. Every
URL the reader must open is a link.

Without JavaScript the page renders Quick Test's steps and VPS Hosting's
steps one after another, all visible, with the default choices; the
selector and stepper only hide and show.

### 5.1 Quick Test, five steps

1. **What you need.** Docker on your computer (Docker Desktop on Windows or
   macOS, Docker Engine on Linux), access to the internet, ports 5000, 5001
   and 8080 free. Nothing is built; the published images are pulled.
2. **Get the files.** A folder with three files: `compose.yaml`, shown in
   full (§6, scenario `quick`), and two downloaded with `curl` from the
   repository's `main` branch: `keycloak/realm-psplace.json` and
   `mongo-entrypoint.sh`.
3. **Start.** `docker compose up -d --wait`, then the three local URLs: the
   app at 5001, `/health` on the API at 5000, Keycloak at 8080.
4. **Make yourself a user.** The realm has self-registration off, said
   plainly. Keycloak console at `localhost:8080/admin/`, `admin`/`admin`,
   realm `psplace`, Users → Add user, Credentials → Set password with
   Temporary off.
5. **Try it.** Open the app, sign in, three things to do first (an inbox
   item turned into today's task, a step on it, a budget with an expense in
   another currency). Then `docker compose down -v` to remove everything,
   and a button to switch to VPS Hosting.

### 5.2 VPS Hosting, eight steps

1. **What you need.** A Linux VPS with access to the internet, 2 GB of RAM
   and Docker Engine; a domain in Cloudflare with three names under it
   (app, API, Keycloak); about thirty minutes. The images are
   `ghcr.io/psp515/pspad-frontend:latest` and
   `ghcr.io/psp515/pspad-backend:latest`, rebuilt on every change.
2. **Choose.** Three choices, each two cards with one line of consequence:
   - Keycloak keeps its users in **a file** (`h2`) or **Postgres** (`pg`).
   - MongoDB runs **on the VPS** (`vps`) or **on MongoDB Atlas** (`atlas`).
   - The internet reaches it through **Cloudflare Tunnel** (`tunnel`) or
     **Cloudflare DNS** (`dns`).
3. **Cloudflare.** Tunnel: Zero Trust → Networks → Tunnels, create,
   copy the token, three public hostnames to `http://app:8080`,
   `http://api:8080`, `http://keycloak:8080`; Cloudflare creates the DNS
   records. DNS: three A records to the VPS address with proxy status
   *DNS only* so Caddy can obtain Let's Encrypt certificates, and ports 80
   and 443 opened on the VPS.
4. **MongoDB.** Atlas: free M0 cluster, database user `pspad` with
   `readWrite` on database `pspad`, allow the VPS address, copy the
   connection string in the form
   `mongodb+srv://pspad:<password>@<cluster>.mongodb.net/pspad?retryWrites=true&w=majority`.
   VPS: nothing to do except download `mongo-entrypoint.sh` beside the
   compose file.
5. **Files.** `compose.yaml` and `.env`, assembled for the choices (§6),
   plus `Caddyfile` for `dns`, plus the `curl` for the realm. The reader
   replaces `example.com` and every `CHANGE_ME`.
6. **Start and check.** `docker compose up -d --wait`, then the three public
   URLs to open and what each shows; `docker compose logs -f api` and
   `keycloak` when something is missing.
7. **First sign-in.** Keycloak console at the auth URL with the admin from
   `.env`; open Realm psplace, Clients, `pspad-frontend`, replace Valid
   redirect URIs with `https://<app>/*` and save (Web origins is `+`, which
   follows); add a user; open the app and install it from the
   browser for the offline app.
8. **Keep it running.** Upgrade: `docker compose pull && docker compose up
   -d --wait`. Backups for the chosen MongoDB (`mongodump` through
   `docker compose exec` for `vps`, `docker run --rm mongo:8.3.11 mongodump
   --uri` for `atlas`), for the chosen Keycloak store (`pg_dump` for `pg`,
   stop and tar the volume for `h2`), and one line on restoring.

### 5.3 Leaves the page

The flat sections of the first round: What you need, Quick start, Options,
How I host it, Maintenance, and the collapsed environment table rendered
from `docker/.env.example`. `EnvTable.astro` and `lib/repo.ts` are deleted
if nothing else uses them.

## 6. Stack assembly

`docs/src/data/stack/` is a small library the Self-host page and a check
script share. Plain TypeScript using only syntax Node 22 strips (no enums,
no parameter properties), so the script runs it directly.

```ts
// options.ts
export type Scenario = 'quick' | 'vps'
export type KeycloakStore = 'h2' | 'pg'
export type MongoHost = 'vps' | 'atlas'
export type Exposure = 'tunnel' | 'dns'
export interface Choices { scenario: Scenario; kc: KeycloakStore; mongo: MongoHost; expose: Exposure }
export const quick: Choices
export function combinations(): Choices[]   // quick + the 8 vps combinations
export function key(c: Choices): string     // 'quick' | 'vps-h2-vps-tunnel' …

// assemble.ts
export function compose(c: Choices): string
export function env(c: Choices): string     // '' for quick
export function caddyfile(c: Choices): string  // '' unless expose === 'dns'
```

What the assembled files contain:

- `name: pspad`. Services `api` and `app` from the GHCR `latest` images,
  `restart: unless-stopped`. The API's environment is the same set
  `docker/compose.yaml` passes today. Quick publishes 5000, 5001 and 8080;
  VPS publishes nothing except Caddy's 80 and 443.
- `mongo` (image `mongo:8.3.11`, entrypoint, keyfile and data volumes,
  healthcheck) unless `atlas`, in which case the API reads
  `MONGO_CONNECTION_STRING` from `.env`.
- `keycloak` (`quay.io/keycloak/keycloak:26.8.1`): `start-dev
  --import-realm` for quick, `start --import-realm` with
  `KC_HTTP_ENABLED`, `KC_HOSTNAME_STRICT` and `KC_PROXY_HEADERS: xforwarded`
  for VPS; `KC_DB*` pointing at `postgres` for `pg`, a `keycloak-data`
  volume for `h2`. The realm folder is bind-mounted read-only.
- `postgres` (`postgres:18.6-alpine3.24`, healthcheck, volume) for `pg`.
- `cloudflared` (`cloudflare/cloudflared:2026.9.1`, `TUNNEL_TOKEN` from
  `.env`) for `tunnel`; `caddy` (`caddy:2.11.2-alpine`, the Caddyfile,
  a data volume) for `dns`.
- VPS declares the default network with a fixed subnet `10.77.0.0/24` so
  `.env` ships `PROXY_NETWORK=10.77.0.0/24` and the API's
  `Proxy__KnownNetworks__0` needs no inspection step.
  `Proxy__ClientIpHeader` is `CF-Connecting-IP` for `tunnel` and
  `X-Forwarded-For` for `dns`.
- `.env` for VPS: the four public URLs on `example.com`, the Keycloak admin,
  then only the secrets the choices need (`MONGO_PASSWORD` or
  `MONGO_CONNECTION_STRING`, `POSTGRES_PASSWORD`, `CLOUDFLARE_TUNNEL_TOKEN`),
  every secret `CHANGE_ME`, each with a one-line comment.

Images are pinned to the same versions `docker/compose.yaml` uses, and a
test (§8) fails when they drift.

The page renders all nine combinations at build time, each file in a
`<pre data-stack="<key>">`, and the client script shows the one matching
the choices. No templating runs in the browser.

`docs/scripts/check-stack.mjs` writes every combination to a temporary
folder (compose, `.env` with the `CHANGE_ME` values, Caddyfile, an empty
`keycloak/` folder and `mongo-entrypoint.sh`) and runs
`docker compose config --quiet` on each; a non-zero exit fails it. The
docs workflow runs it before `npm run build`; `package.json` exposes it as
`npm run check-stack`.

## 7. Screenshots

`test/e2e/screenshots.spec.ts`, run as `npm run screenshots` in `test/e2e`
against the e2e stack. It is a Playwright test file in its own project in
`playwright.config.ts` (`screenshots`, not matched by `npm test`), so it
reuses the account fixture, `signIn`, and the helpers in `test/e2e/helpers`.

One test seeds a fresh user with sample content in English: two areas with
two lists each, eight tasks including one timed, one overdue, one recurring
and one with steps, three inbox items, one
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
the day of capture so "today" reads as today. The PNGs are committed. A
`Shot` component renders both files as lazy images and CSS shows the one
matching the site theme, the same `prefers-color-scheme` and `data-theme`
rules `theme.css` already uses for colours.

The script is documented in `test/e2e/README.md` and runs on demand only;
CI does not run it.

## 8. README

The About section becomes the pitch from §2 and the five highlight titles
with one line each, then a link to the Features page. The Documentation
section lists the three pages. Development is unchanged except for a link
to `specs/localization-design.md` for adding a language.

## 9. Testing and acceptance

- `npm run build` in `docs/` passes; the docs workflow already runs it on
  every pull request.
- `npm run check-stack` in `docs/` passes: all nine combinations render
  through `docker compose config`.
- `docker/` matches `origin/main` except the comment in
  `mongo-app-user.js`: `git diff origin/main -- docker/` lists that file
  only.
- The Quick Test compose, with its three ports remapped, starts on the
  maintainer's box, the API reports `Healthy`, and `down -v` removes it.
- `/install/` responds with a redirect to `/self-host/`.
- Home: with JavaScript at 1280×800 the hero's bottom edge is at the fold; the next section's heading is below it.
