---
title: Run MongoDB 9 in compose, CI and integration tests
tags: [persistence, testing, containers, ci, dependencies]
date: 2026-10-21
status: Active
---

# ADR-0078: Run MongoDB 9 in compose, CI and integration tests

> Status: Active (amends [ADR-0009](0009-integration-tests-own-their-database.md))

## Context

ADR-0009 fixed integration tests on a MongoDB 8 replica set, and ADR-0071
pinned the compose stack to `mongo:8.3.11`. Every MongoDB 8 release up to
8.3.13 reads `uname -r` and refuses to start on kernels it takes for 6.19 to
7.0.13. GitHub's `ubuntu-24.04` runner reports `7.0.0-azure`, so CI's three
Mongo jobs were held on `ubuntu-22.04` (actions/runner-images#15005), and a
self-hoster on a recent distribution kernel hits the same exit. MongoDB 9.0.4
carries the fix (SERVER-131779); 8.3.14 was announced but not released.
Dependabot proposed `mongo:9.0.4`. The pinned MongoDB.Driver 3.12 is listed as
fully compatible with server 9.0. The app uses no server-side JavaScript, no
queryable encryption and no change streams, which are the areas 9.0 changes.

## Decision

We will run MongoDB 9: compose pins `mongo:9.0.4`, CI's smoke test pins the
same tag, the Testcontainers fixture uses `mongo:9`, and CI's Mongo jobs go
back to `ubuntu-latest`. The rest of ADR-0009 stands. Self-hosters upgrade
in place by raising the feature compatibility version to 8.3 before pulling
and to 9.0 after the new container is healthy, as the install page says.

## Considered alternatives

- **Stay on 8.3 until 8.3.14 ships** — rejected: no release date, and CI stays
  on an older runner image in the meantime.
- **Pin the CI runner kernel or image** — rejected: GitHub declined to pin
  the kernel, and `ubuntu-22.04` retires before 8.x leaves support.

## Consequences

CI and recent host kernels start Mongo again. Upgrading is no longer only a
`git pull`: a deployment still at feature compatibility 8.0 (installed with
floating `mongo:8`) refuses to start on 9.0 until it is raised to 8.3, and
after the move to 9.0 the only way back is 8.3, so a dump before upgrading is
the rollback. Integration tests follow the latest 9.x on the `mongo:9` tag,
which can change under a test run on a 9.x patch.
