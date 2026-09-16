# Handoff — UI polish and burndown (`psp/feature`)

**Status: code-complete and reviewed. Two non-code items remain before merging to `main`.**

Everything this file used to track — the four plans' own fix waves, the
burndown chart, the `MongoBackfill` seq-bump and its knock-on `SyncReader`
marker fix, ADR-0019/ADR-0020, docs — is done, tested, and passed a final
whole-branch review (`main..psp/feature`) plus one fix wave for the two
Important findings that review raised (a sync-marker race under concurrent
cross-collection commits, fixed via a snapshot-isolation transaction; and
Settings falsely showing "Everything is synced." while offline, fixed by
reading the real outbox count). That fix wave was itself re-reviewed clean.
Full history of every task, fix round, and ruling made along the way lives
in this branch's commit log (`git log 71385dd..HEAD`) — nothing is in a
gitignored ledger anymore, so `git log`/`git show` on any commit is the
record now, not this file.

Full test suite as of the last commit: 317 unit tests + 44 integration
tests (Testcontainers, real MongoDB replica set), all green.

## What's left before merging to `main`

**1. Two manual checks, not automatable without Docker + a browser + a real
Keycloak realm:**
- The Keycloak sign-in check: confirm the sidebar shows a real name and
  email after sign-in; confirm the heal path (change the Keycloak user's
  name, sign out, sign in, confirm the sidebar picks it up); clear the
  user's first/last name in Keycloak entirely and sign in again, to check
  the `RenameAsync`/GUID-fallback guard against a real token (not just the
  test-harness's synthetic one).
- The responsive-grid check: column counts at ~360px (narrower than a
  quick eyeball check would normally try) and with a screen holding a
  non-multiple item count (e.g. 3 cards where the layout fits 2 columns) —
  these are the specific conditions that exposed real CSS bugs earlier in
  this branch's history.

**2. The real-email-in-git-history question** — a personal email address
committed to `main` before this branch existed (commit `0a2704d`, PR #11).
This branch's own test fixtures that had the same issue are cleaned; the
exposure on `main` itself is unchanged. Resolving it (rewriting shared
history is destructive to every existing clone; whether to do anything on
GitHub's side) is the maintainer's call, not something to decide as part
of this branch's work.

## Then

- Push `psp/feature` to `origin` (19 commits ahead as of this writing).
- Decide how to land it on `main` (PR vs. direct merge) — maintainer's call.
- Delete this file once merged.
