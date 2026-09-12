# PSPad
My vision of notepad with GTD practicies.

## Development

Develop on your own machine with the .NET 10 SDK. Docker supplies the backing
services and, for integration tests, a throwaway MongoDB.

```bash
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

The production stack (`docker/compose.prod.yaml`) serves plain HTTP and expects
a reverse proxy in front of it.

Design documents live in `docs/superpowers/specs/2026-09-12-slice-1-design.md`,
implementation plans in `docs/superpowers/plans/`, and the working agreement for
agents in `AGENTS.md`.
