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

We will pin every base image, and every image the repo compose pulls, to a
version and a digest.

We will manage NuGet versions centrally in `Directory.Packages.props`; no
project names a version.

We will let Dependabot propose NuGet, Dockerfile, compose and GitHub
Actions updates weekly, with minor and patch updates grouped per ecosystem.

In the repo compose the API signs in to MongoDB as `pspad-api`, holding
`readWrite` on `MONGO_DATABASE` only. A one-shot `mongo-app-user` service,
which the API waits for, creates that user or resets its password and roles
on every start; the root user remains for the healthcheck, that service and
backups.

## Considered alternatives

- **Pin by tag only** — a tag can be re-pushed; the digest makes a build
  reproducible, and Dependabot updates tag and digest together.
- **Create the app user from `/docker-entrypoint-initdb.d`** — init scripts
  run only on an empty data volume, so every existing install would need a
  manual step, and a password change in `.env` would never reach MongoDB.
- **Make the whole web root writable by nginx** — simpler, but it would let
  a compromised nginx rewrite the app it serves.
- **Renovate instead of Dependabot** — needs an app install or a self-hosted
  runner; Dependabot is built into GitHub and covers all four ecosystems.

## Consequences

- A container escape or an nginx/ASP.NET RCE lands as an unprivileged user;
  a leaked API connection string can no longer drop databases or create
  users.
- Anything mounted into the API or client container later must be readable
  (or writable) by UID 1654 or 101; a root-owned host directory fails.
- Nothing upgrades silently: a new image or package arrives as a Dependabot
  pull request, which someone has to review and merge, and a pinned digest
  keeps an old image until then.
- Self-hosters must set `MONGO_APP_PASSWORD`; compose refuses to start
  without it.
- Moving the repo compose to Keycloak 26.7.4 migrates its embedded H2
  database on first start.
