# PSPad
My vision of notepad with GTD practicies.

## Development

Develop on your own machine with the .NET 10 SDK. Docker supplies the backing
services and, for integration tests, a throwaway MongoDB.

```bash
cp docker/.env.example docker/.env
docker compose -f docker/compose.yaml up -d
```

That brings up MongoDB and Keycloak. MongoDB runs as a single-node replica set
because transactions require one. Then work normally:

```bash
dotnet build
dotnet test
```

Integration tests do not use the stack above — they start their own MongoDB
through Testcontainers, so Docker must be running. The suites split by category:

```bash
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
```

## Containers

Each deployable carries its own `Dockerfile` next to its `.csproj`, built with
the repository root as context so the shared build props come along:

- `src/PSPad.Api/Dockerfile` — the API.
- `src/PSPad.App/Dockerfile` — the Blazor WebAssembly client, published and
  served as static files by nginx on port 8080. `API_BASE_ADDRESS`,
  `KEYCLOAK_AUTHORITY` and `KEYCLOAK_CLIENT_ID` are substituted into
  `appsettings.json` at container start, so the same image runs in any
  environment.

There is one stack, `docker/compose.yaml`, and it is both the development and
the self-hosting stack. It reads `docker/.env` (copy `docker/.env.example`),
serves plain HTTP, and expects a reverse proxy in front of it. Self-hosting is
documented in full at <https://psp515.github.io/PSPad/install>.

Design specs live in `specs/` — `slice-design.md` covers the backend and
domain end to end, `ui-redesign-2-design.md` the client's navigation, theming
and screens, with `ui-ux-redesign-design.md` behind it as the superseded first
pass. Architecture decision records live in `adr/` (see `adr/README.md`
for the index) and carry the reasoning and rejected alternatives behind each
decision; where a spec and an ADR disagree, the ADR is the decision of record.
The working agreement for agents is in `AGENTS.md`.

## Offline

The client keeps a replica of its data in the browser's IndexedDB (`pspad`
database — `documents`, `meta` and `outbox` object stores), so the app stays
usable with no network at all. Every edit writes straight to the replica and
appends a command to the outbox; nothing waits on the API.

A sync service flushes the outbox in strict append order whenever the app
starts, whenever the browser comes back online, and every 60 seconds while
online. It pushes commands in order and stops at the first one the server
rejects — a command behind a rejected one is never sent ahead of it, since it
may depend on state the rejection means never landed. The rejection message
surfaces to the user through a snackbar; it is never dropped silently. Once
the outbox is flushed, the service pulls everything changed since the
replica's marker (the server's own monotonic sequence number, never a clock)
and overwrites the matching replica rows.

The replica is disposable: clearing site data, or losing IndexedDB entirely,
costs nothing but a full pull from marker 0 the next time the app is online.
The server is the source of truth; the replica only ever mirrors it.
