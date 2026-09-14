---
title: One .env-driven self-host compose stack
tags: [deployment, security, identity]
date: 2026-09-14
status: Active
---

# ADR-0015: One `.env`-driven self-host compose stack

## Context

The repository carried two compose files, a dev one and a production one, and
the production one could not start: its connection string asked Mongo for
replica-set discovery against a set whose only member is `localhost:27017`,
a host the API container cannot resolve as itself. The replica-set keyfile
that authenticates members to each other was committed to the repository, so
every installation built from it would share one published secret — anyone
who had ever cloned the repo could authenticate as a cluster member. Keycloak
ran `start-dev` with no volume behind it, so every account, client and realm
setting was lost the moment the container was recreated. Nothing in the repo
described how a self-hoster was meant to run the result, or what to change
before doing so.

## Decision

We will run one `docker/compose.yaml`, used for both development and
self-hosting, with every environment-specific value read from `${VAR}` and
`docker/.env.example` committed as the tracked template a self-hoster copies
to `docker/.env`. MongoDB connects with `directConnection=true` instead of
replica-set discovery, so the API never needs to resolve a member host it
cannot reach. The replica-set keyfile is generated into a named volume the
first time the `mongo` container starts, by `docker/mongo-entrypoint.sh`,
instead of living in the repository. Keycloak keeps its data on the
`keycloak-data` named volume mounted at `/opt/keycloak/data`, so accounts and
realm state survive a container recreate, while it stays in `start-dev` mode
rather than moving to a production Keycloak deployment.

## Considered alternatives

- **Keep two compose files.** Rejected: the two drift by construction — every
  change has to be made twice or the files disagree — and the second file was
  already broken in a way nobody had caught, because nothing exercised it.
- **Put Postgres behind Keycloak for production.** Rejected: it is the
  supported path, but it adds a second database engine and a second container
  to operate for a single-user, self-hosted deployment whose only other store
  is one MongoDB. The cost does not match what it buys here.
- **Initiate the replica set with member host `mongo:27017` and keep
  discovery.** Rejected: that hostname resolves fine for another container on
  the compose network, but it becomes the replica set's own advertised
  member address, baked into its configuration rather than into any one
  caller's connection string. Anything that ever reaches this MongoDB from
  outside that network — a restored backup, a `mongosh` run from the host, an
  operator debugging with the port temporarily published — would be handed
  back an advertised host it cannot resolve. `directConnection=true` performs
  no discovery at all, so no hostname is ever advertised to a caller and the
  question does not arise.
- **Bring-your-own identity provider.** Rejected: an install page that opens
  by telling the reader to go install and configure a separate product first
  is a worse start than a Keycloak container the stack already brings up.

## Consequences

There is one stack left to keep working, and it is the same one contributors
run day to day, so a break in it is caught immediately rather than discovered
by the first self-hoster to try it. The install page has to say plainly that
Keycloak's dev-mode H2 store is not supported for production use and that a
major Keycloak image bump may not migrate its data cleanly — that risk is now
carried by every deployment, not isolated to a throwaway dev container. The
old committed keyfile remains reachable in git history; rotating it on any
installation that started from an early clone is a manual step this decision
does not perform.

The realm export was tried with `${env.APP_BASE_ADDRESS}`-style placeholders
in the `pspad-app` client's redirect URIs and web origins, matching the rest
of the stack's `${VAR}` substitution. Keycloak 26.0 validates that field as a
URI before substitution happens and refuses to start on the raw placeholder,
so the export keeps literal `http://localhost:5001/*` values instead. A
self-hoster who changes `APP_BASE_ADDRESS` away from the default must also
open the Keycloak admin console and edit the `pspad-app` client's redirect
URIs and web origins by hand — `.env` alone no longer fully determines the
running stack, and the install page has to say so.

A self-hoster pointing at an external MongoDB instead of the bundled
container now edits `docker/compose.yaml` directly — removing the `mongo`
service and pointing `Mongo__ConnectionString` at their own cluster — rather
than flipping a variable, since no environment variable was carried for that
case.
