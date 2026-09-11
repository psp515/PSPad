# PSPad
My vision of notepad with GTD practicies.

## Development

Develop on your own machine with the .NET 10 SDK. Docker supplies the backing
services and, for integration tests, a throwaway PostgreSQL.

```bash
docker compose -f docker/compose.yaml up -d
```

That brings up PostgreSQL and Keycloak. Then work normally:

```bash
dotnet build
dotnet test
```

Integration tests do not use the stack above — they start their own PostgreSQL
through Testcontainers, so Docker must be running. The suites split by category:

```bash
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
```

## Containers

Each deployable carries its own `Dockerfile` next to its `.csproj`, built with
the repository root as context so the shared build props come along:

- `src/PSPad.Server/Dockerfile` — the API.
- `src/PSPad.Client/Dockerfile` — the Blazor WebAssembly client, served as
  static files. Added with the client project itself.

The production stack (`docker/compose.prod.yaml`) serves plain HTTP and expects
a reverse proxy in front of it.

Design documents live in `docs/superpowers/specs/`, implementation plans in
`docs/superpowers/plans/`, and the working agreement for agents in `AGENTS.md`.
