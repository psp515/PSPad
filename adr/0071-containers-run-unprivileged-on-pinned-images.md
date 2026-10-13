---
title: Containers run unprivileged on pinned images, package versions are central, and Dependabot keeps both current
tags: [architecture, security, containers, dependencies, storage]
date: 2026-10-13
status: Active
---

# ADR-0071: Containers run unprivileged on pinned images, package versions are central, and Dependabot keeps both current

> Status: Active

## Context

Issue #143 (OWASP A05/A06). The API image had no `USER` line and ran as
root; the client was served by `nginx:alpine`, whose master process runs as
root. The repo compose connected the API to MongoDB as the cluster root
user. Base images floated (`sdk:10.0`, `aspnet:10.0`, `nginx:alpine`,
`mongo:8`), the compose shipped Keycloak 26.0, which is out of support, and
nothing flagged new releases: each `.csproj` carried its own versions, and
two WebAssembly packages had drifted to 10.0.5 while every other framework
package was on 10.0.12.

## Decision

We will run every PSPad container as a non-root user: the API as the .NET
image's `app` user (`USER $APP_UID`), the client on
`nginxinc/nginx-unprivileged`, both on port 8080. In the client image the
served files stay root-owned; only the two files the entrypoint rewrites
(`appsettings.json`, `appsettings.json.gz`) belong to nginx.

We will pin every base image, and every image the repo compose pulls, to an
exact version tag (`major.minor.patch`, never `latest` or a bare major), and
not to a digest.

We will manage NuGet versions centrally in `Directory.Packages.props`; no
project names a version.

We will let Dependabot propose NuGet, Dockerfile, compose and GitHub
Actions updates weekly, with minor and patch updates grouped per ecosystem.

The repo compose keeps connecting the API to MongoDB as the root user.
`docker/mongo-app-user.js` ships as an example that creates, or
re-passwords, a user holding `readWrite` on one database only; the install
guide documents it, plus a compose override pointing the API at that user,
as an optional hardening step.

## Considered alternatives

- **Pin tag and digest** — makes a build byte-for-byte reproducible even if a
  tag is re-pushed, but every bump becomes a 64-character diff that nobody
  reviews by eye, and an exact tag already stops silent major and minor
  jumps. Rejected by the maintainer.
- **A one-shot compose service creating the app user on every start** —
  tried first: it added a required `MONGO_APP_PASSWORD`, a fifth service and
  a startup dependency to every install, for a protection self-hosters can
  opt into with one script run. Kept as the example script instead.
- **Create the app user from `/docker-entrypoint-initdb.d`** — init scripts
  run only on an empty data volume, so every existing install would need a
  manual step, and a password change in `.env` would never reach MongoDB.
- **Make the whole web root writable by nginx** — simpler, but it would let
  a compromised nginx rewrite the app it serves.
- **Renovate instead of Dependabot** — needs an app install or a self-hosted
  runner; Dependabot is built into GitHub and covers all four ecosystems.

## Consequences

- A container escape or an nginx/ASP.NET RCE lands as an unprivileged user.
- A leaked API connection string from the default repo compose is still the
  root one; only installs that apply the optional database user limit it to
  `readWrite` on the PSPad database.
- Anything mounted into the API or client container later must be readable
  (or writable) by UID 1654 or 101; a root-owned host directory fails.
- Nothing upgrades silently: a new image or package arrives as a Dependabot
  pull request, which someone has to review and merge. An exact tag can
  still be re-pushed upstream with patched contents; we accept that.
- Moving the repo compose to Keycloak 26.7.4 migrates its embedded H2
  database on first start.
