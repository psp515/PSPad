---
title: Keep the OIDC login-callback route off AppShell's layout
tags: [identity, sync, offline]
date: 2026-09-17
status: Active
---

# ADR-0024: Keep the OIDC login-callback route off AppShell's layout

## Context

`AppShell` (`DefaultLayout` for every route via `AuthorizeRouteView`) resolves
the signed-in account exactly once, in `OnInitializedAsync`: it awaits the
cascading `AuthenticationState`, and if authenticated calls `/api/me`, sets
`AppState.UserId`, runs `ReplicaOwnership.EnsureCurrentUserAsync` (ADR-0018),
then flips `_ready` and starts `SyncCoordinator`. If unauthenticated, it flips
`_ready` immediately with `AppState.UserId` left at `Guid.Empty` and still
starts `SyncCoordinator` — a deliberate choice so a genuinely anonymous or
offline load doesn't hang on `BrandLoader` forever.

`Pages/Authentication.razor` (`/authentication/{action}`) is not
`[Authorize]`-guarded (it can't be — that would be circular), so it also
renders through `AppShell`. On a first-ever sign-in, Keycloak's redirect lands
the browser on this exact route with a fresh WASM boot, and
`RemoteAuthenticatorView` — a *child* of `AppShell`'s `@Body` — is the
component that actually exchanges the code and stores the session. Until it
finishes, the cascading `AuthenticationState` reads unauthenticated. AppShell's
one-shot check almost always wins that race: it takes the unauthenticated
branch, `_ready` becomes true and `AppState.UserId` stays `Guid.Empty`.

`RemoteAuthenticatorView` then finishes and navigates to the return URL with
plain client-side `NavigationManager.NavigateTo` — no full reload. Blazor
layouts are not re-instantiated by a client-side route change, so `AppShell`
never re-runs its check. The result: `AppState.UserId` stays `Guid.Empty` for
the rest of the session even though the user is now genuinely signed in.
Every command built from `AppState.UserId` from then on embeds the wrong user
id, and `CommandDispatcher` rejects each one forever as "That command is for a
different user" — indistinguishable, from the user's side, from ADR-0018's
stale-outbox scenario, but with a different root cause and unfixable by that
ADR's purge-on-switch logic, since `ReplicaOwnership.EnsureCurrentUserAsync`
is never even called on this path. A page refresh "fixes" it only because it
lands on a normal route where the OIDC session is already established, so the
cascading auth state resolves correctly on AppShell's first (and only) check.

## Decision

We will give `Authentication.razor` its own minimal layout
(`Layout/AuthenticationLayout.razor`, a bare `@Body`) via `@layout`, which
Blazor's route-to-layout resolution prefers over `AuthorizeRouteView`'s
`DefaultLayout`. `AppShell` then never mounts on the login-callback route, so
its one-shot auth check only ever runs on a route where the OIDC redirect
dance has already fully completed — the race is removed by construction
rather than patched by making the check retry or reactive.

## Considered alternatives

- **Make `AppShell` react to subsequent `AuthenticationState` changes**
  (subscribe to `AuthenticationStateProvider.AuthenticationStateChanged`, or
  move the check into `OnParametersSetAsync`). Rejected: it treats the
  symptom on the one component we noticed it on, but `AppShell` doing
  first-sign-in-sensitive work at all while sharing a mount point with the
  callback page is the actual hazard — any future logic added to `AppShell`
  keyed off "am I authenticated yet" would be exposed to the same race again.
  Removing the shared mount point removes the whole class of bug.
- **Detect and recover from the wrong-user state after the fact** (e.g. have
  the client re-check `/api/me` if a command comes back "different user").
  Rejected: papers over a `Guid.Empty` session that should never have existed,
  and every command sent in the meantime is still lost — same outcome ADR-0018
  rejected for the stale-outbox case, for the same reason.

## Consequences

The login-callback route no longer has access to MudBlazor's providers
(`MudThemeProvider`, dialogs, snackbar) that live inside `AppShell` — fine,
since `RemoteAuthenticatorView`'s own UI is a brief, unstyled "processing"
message. Any other route that must render outside `AppShell` in the future
follows the same `@layout` pattern rather than growing a second conditional
inside `AppShell` itself. If `AppShell` ever needs its own OIDC-callback
awareness again (unlikely, since none of its data-loading logic applies
mid-login), that would call for a second decision rather than quietly
weakening this one.
