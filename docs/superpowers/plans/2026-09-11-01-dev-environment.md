# Dev Environment & Solution Scaffold Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A fully containerized development environment where `dotnet test` runs green against a real PostgreSQL, plus the empty solution structure every later plan builds into.

**Architecture:** Everything runs in Docker, including the .NET SDK. A dev container built on the SDK image is where builds, tests and `dotnet watch` execute. Docker Compose supplies PostgreSQL (dev), a second PostgreSQL (tests), and Keycloak. The solution is a modular monolith: a pure domain, a shared contracts project, a server, a WASM client, and two test projects.

**Tech Stack:** .NET 10, Docker Compose, PostgreSQL 17, Keycloak 26, xUnit, Shouldly

**Spec:** `docs/superpowers/specs/2026-09-11-gtd-core-design.md`

## Global Constraints

- .NET 10 (LTS). All projects target `net10.0`.
- `PSPad.Domain` and `PSPad.Contracts` reference no infrastructure packages and must compile for WebAssembly.
- PostgreSQL 17. Marten 8.x. Wolverine 4.x. MudBlazor 8.x. xUnit + Shouldly.
- All user-facing dates are `DateOnly` in the user's time zone; all stored instants are UTC `DateTimeOffset`.
- No audit table — history comes from the event stream.
- Code, comments, commits and docs in English.
- GPL v3 — every dependency must be license-compatible.

---

### Task 1: Docker Compose infrastructure

**Files:**
- Create: `docker/compose.yaml`
- Create: `docker/.env.example`
- Create: `docker/keycloak/realm-pspad.json`

**Interfaces:**
- Consumes: nothing
- Produces: services `postgres` (port 5432), `postgres-test` (port 5433), `keycloak` (port 8080) on network `pspad`; connection strings `Host=postgres;Port=5432;Database=pspad;Username=pspad;Password=pspad` and `Host=postgres-test;Port=5432;Database=pspad_test;Username=pspad;Password=pspad`

- [ ] **Step 1: Write the compose file**

`docker/compose.yaml`:

```yaml
name: pspad

services:
  postgres:
    image: postgres:17-alpine
    environment:
      POSTGRES_DB: pspad
      POSTGRES_USER: pspad
      POSTGRES_PASSWORD: pspad
    ports:
      - "5432:5432"
    volumes:
      - postgres-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U pspad -d pspad"]
      interval: 5s
      timeout: 5s
      retries: 10
    networks: [pspad]

  postgres-test:
    image: postgres:17-alpine
    environment:
      POSTGRES_DB: pspad_test
      POSTGRES_USER: pspad
      POSTGRES_PASSWORD: pspad
    ports:
      - "5433:5432"
    tmpfs:
      - /var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U pspad -d pspad_test"]
      interval: 5s
      timeout: 5s
      retries: 10
    networks: [pspad]

  keycloak:
    image: quay.io/keycloak/keycloak:26.0
    command: ["start-dev", "--import-realm"]
    environment:
      KC_BOOTSTRAP_ADMIN_USERNAME: admin
      KC_BOOTSTRAP_ADMIN_PASSWORD: admin
      KC_HEALTH_ENABLED: "true"
    ports:
      - "8080:8080"
    volumes:
      - ./keycloak:/opt/keycloak/data/import:ro
    networks: [pspad]

volumes:
  postgres-data:

networks:
  pspad:
```

The test database uses `tmpfs`, so every restart gives a clean disk and tests
stay fast.

- [ ] **Step 2: Write the example environment file**

`docker/.env.example`:

```dotenv
POSTGRES_CONNECTION=Host=postgres;Port=5432;Database=pspad;Username=pspad;Password=pspad
POSTGRES_TEST_CONNECTION=Host=postgres-test;Port=5432;Database=pspad_test;Username=pspad;Password=pspad
KEYCLOAK_AUTHORITY=http://keycloak:8080/realms/pspad
KEYCLOAK_AUDIENCE=pspad-api
```

- [ ] **Step 3: Write a minimal Keycloak realm**

`docker/keycloak/realm-pspad.json`:

```json
{
  "realm": "pspad",
  "enabled": true,
  "clients": [
    {
      "clientId": "pspad-client",
      "publicClient": true,
      "standardFlowEnabled": true,
      "redirectUris": ["http://localhost:5000/*"],
      "webOrigins": ["http://localhost:5000"]
    },
    {
      "clientId": "pspad-api",
      "bearerOnly": true
    }
  ]
}
```

- [ ] **Step 4: Verify the stack comes up**

Run: `docker compose -f docker/compose.yaml up -d`
Then: `docker compose -f docker/compose.yaml ps`
Expected: `postgres` and `postgres-test` report `healthy`, `keycloak` reports `running`.

- [ ] **Step 5: Commit**

```bash
git add docker/
git commit -m "chore: add docker compose infrastructure for dev and tests"
```

---

### Task 2: Dev container

**Files:**
- Create: `.devcontainer/devcontainer.json`
- Create: `.devcontainer/Dockerfile`

**Interfaces:**
- Consumes: the `pspad` network and services from Task 1
- Produces: a dev container where `dotnet` is on PATH and `postgres`, `postgres-test`, `keycloak` resolve by hostname

- [ ] **Step 1: Write the dev container Dockerfile**

`.devcontainer/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0

RUN apt-get update \
 && apt-get install -y --no-install-recommends git curl ca-certificates \
 && rm -rf /var/lib/apt/lists/*

RUN dotnet workload install wasm-tools

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_USE_POLLING_FILE_WATCHER=1

WORKDIR /workspace
```

`wasm-tools` is required for the Blazor WebAssembly client in plan 06 — install
it now so the image is built once.

- [ ] **Step 2: Write the dev container definition**

`.devcontainer/devcontainer.json`:

```json
{
  "name": "PSPad",
  "build": { "dockerfile": "Dockerfile" },
  "runServices": ["postgres", "postgres-test", "keycloak"],
  "dockerComposeFile": ["../docker/compose.yaml", "compose.devcontainer.yaml"],
  "service": "devcontainer",
  "workspaceFolder": "/workspace",
  "forwardPorts": [5000, 5432, 5433, 8080],
  "customizations": {
    "vscode": {
      "extensions": ["ms-dotnettools.csdevkit"]
    }
  }
}
```

- [ ] **Step 3: Add the dev container service to compose**

`.devcontainer/compose.devcontainer.yaml`:

```yaml
services:
  devcontainer:
    build:
      context: .
      dockerfile: Dockerfile
    volumes:
      - ..:/workspace:cached
    command: sleep infinity
    networks: [pspad]
    depends_on:
      postgres:
        condition: service_healthy
      postgres-test:
        condition: service_healthy
```

- [ ] **Step 4: Verify connectivity from inside the container**

Run inside the dev container: `dotnet --version`
Expected: a `10.x` version string.

Run: `getent hosts postgres-test`
Expected: an IP address, proving the test database is reachable by hostname.

- [ ] **Step 5: Commit**

```bash
git add .devcontainer/
git commit -m "chore: add dev container running the dotnet sdk in docker"
```

---

### Task 3: Solution and project skeleton

**Files:**
- Create: `PSPad.sln`
- Create: `Directory.Build.props`
- Create: `src/PSPad.Domain/PSPad.Domain.csproj`
- Create: `src/PSPad.Contracts/PSPad.Contracts.csproj`
- Create: `src/PSPad.Server/PSPad.Server.csproj`
- Create: `test/PSPad.Domain.Tests/PSPad.Domain.Tests.csproj`
- Create: `test/PSPad.Server.Tests/PSPad.Server.Tests.csproj`

**Interfaces:**
- Consumes: the dev container from Task 2
- Produces: assemblies `PSPad.Domain`, `PSPad.Contracts`, `PSPad.Server`; reference graph `Contracts -> Domain`, `Server -> Contracts`, both test projects -> their subject

`PSPad.Client` is deliberately absent — plan 06 creates it, because the WASM
template pulls a large dependency set that has no business existing before
there is a UI to run.

- [ ] **Step 1: Create the solution and class library projects**

```bash
dotnet new sln -n PSPad
dotnet new classlib -o src/PSPad.Domain -f net10.0
dotnet new classlib -o src/PSPad.Contracts -f net10.0
dotnet new web -o src/PSPad.Server -f net10.0
dotnet new xunit -o test/PSPad.Domain.Tests -f net10.0
dotnet new xunit -o test/PSPad.Server.Tests -f net10.0
dotnet sln add src/**/*.csproj test/**/*.csproj
```

- [ ] **Step 2: Wire the reference graph**

```bash
dotnet add src/PSPad.Contracts reference src/PSPad.Domain
dotnet add src/PSPad.Server reference src/PSPad.Contracts
dotnet add test/PSPad.Domain.Tests reference src/PSPad.Domain
dotnet add test/PSPad.Server.Tests reference src/PSPad.Server
dotnet add test/PSPad.Domain.Tests package Shouldly
dotnet add test/PSPad.Server.Tests package Shouldly
```

- [ ] **Step 3: Add shared build settings**

`Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
```

- [ ] **Step 4: Write a guard test that the domain stays infrastructure-free**

`test/PSPad.Domain.Tests/PurityTests.cs`:

```csharp
using System.Reflection;
using Shouldly;

namespace PSPad.Domain.Tests;

public class PurityTests
{
    static readonly string[] ForbiddenPrefixes =
    [
        "Marten", "Npgsql", "Wolverine", "Microsoft.AspNetCore", "System.Net.Http"
    ];

    [Fact]
    public void Domain_assembly_references_no_infrastructure()
    {
        var referenced = typeof(DomainMarker).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ToArray();

        foreach (var prefix in ForbiddenPrefixes)
        {
            referenced.ShouldNotContain(
                name => name.StartsWith(prefix, StringComparison.Ordinal),
                $"PSPad.Domain must not reference {prefix} (see AD-4)");
        }
    }
}
```

- [ ] **Step 5: Run the test to verify it fails**

Run: `dotnet test test/PSPad.Domain.Tests`
Expected: FAIL — `DomainMarker` does not exist.

- [ ] **Step 6: Add the marker type**

`src/PSPad.Domain/DomainMarker.cs`:

```csharp
namespace PSPad.Domain;

/// <summary>Anchor type for assembly-level reflection. Holds no behavior.</summary>
public sealed class DomainMarker;
```

- [ ] **Step 7: Run the test to verify it passes**

Run: `dotnet test test/PSPad.Domain.Tests`
Expected: PASS, 1 test.

- [ ] **Step 8: Commit**

```bash
git add PSPad.sln Directory.Build.props src/ test/
git commit -m "chore: scaffold solution with domain purity guard"
```

---

### Task 4: Test database fixture

**Files:**
- Create: `test/PSPad.Server.Tests/PostgresFixture.cs`
- Create: `test/PSPad.Server.Tests/DatabaseCollection.cs`
- Modify: `test/PSPad.Server.Tests/PSPad.Server.Tests.csproj`

**Interfaces:**
- Consumes: the `postgres-test` service from Task 1
- Produces: `PostgresFixture` exposing `string ConnectionString`, and an xUnit collection named `"database"` that every later server test joins with `[Collection("database")]`

- [ ] **Step 1: Write a failing test that the fixture can reach the database**

`test/PSPad.Server.Tests/PostgresFixtureTests.cs`:

```csharp
using Npgsql;
using Shouldly;

namespace PSPad.Server.Tests;

[Collection("database")]
public class PostgresFixtureTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Test_database_is_reachable()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand("select 1", connection);
        var result = await command.ExecuteScalarAsync();

        result.ShouldBe(1);
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test test/PSPad.Server.Tests`
Expected: FAIL — `PostgresFixture` and `Npgsql` do not exist.

- [ ] **Step 3: Add the Npgsql package and write the fixture**

```bash
dotnet add test/PSPad.Server.Tests package Npgsql
```

`test/PSPad.Server.Tests/PostgresFixture.cs`:

```csharp
namespace PSPad.Server.Tests;

/// <summary>
/// Points tests at the compose service `postgres-test`. The connection string
/// can be overridden with POSTGRES_TEST_CONNECTION so the same suite runs in CI.
/// </summary>
public sealed class PostgresFixture
{
    public string ConnectionString { get; } =
        Environment.GetEnvironmentVariable("POSTGRES_TEST_CONNECTION")
        ?? "Host=postgres-test;Port=5432;Database=pspad_test;Username=pspad;Password=pspad";
}
```

`test/PSPad.Server.Tests/DatabaseCollection.cs`:

```csharp
namespace PSPad.Server.Tests;

[CollectionDefinition("database")]
public sealed class DatabaseCollection : ICollectionFixture<PostgresFixture>;
```

The collection serializes database tests, so two suites never fight over the
same schema.

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test test/PSPad.Server.Tests`
Expected: PASS, 1 test.

- [ ] **Step 5: Commit**

```bash
git add test/PSPad.Server.Tests/
git commit -m "test: add postgres fixture backed by the compose test database"
```

---

### Task 5: Production image and developer entry points

**Files:**
- Create: `docker/Dockerfile`
- Create: `docker/compose.prod.yaml`
- Create: `docker/.dockerignore`
- Modify: `README.md`

**Interfaces:**
- Consumes: the solution from Task 3
- Produces: image `pspad-server:local`, and the documented commands `docker compose -f docker/compose.yaml up -d` and `dotnet test`

- [ ] **Step 1: Write the production Dockerfile**

`docker/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props PSPad.sln ./
COPY src/ src/
RUN dotnet publish src/PSPad.Server/PSPad.Server.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PSPad.Server.dll"]
```

- [ ] **Step 2: Write the ignore file**

`docker/.dockerignore`:

```gitignore
**/bin/
**/obj/
.git/
.devcontainer/
docs/
test/
```

- [ ] **Step 3: Write the production compose file**

`docker/compose.prod.yaml`:

```yaml
name: pspad-prod

services:
  app:
    build:
      context: ..
      dockerfile: docker/Dockerfile
    image: pspad-server:local
    environment:
      ConnectionStrings__Postgres: ${POSTGRES_CONNECTION}
      Keycloak__Authority: ${KEYCLOAK_AUTHORITY}
      Keycloak__Audience: ${KEYCLOAK_AUDIENCE}
    ports:
      - "5000:8080"
    depends_on:
      postgres:
        condition: service_healthy
    networks: [pspad]

  postgres:
    image: postgres:17-alpine
    environment:
      POSTGRES_DB: pspad
      POSTGRES_USER: pspad
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - postgres-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U pspad -d pspad"]
      interval: 5s
      timeout: 5s
      retries: 10
    networks: [pspad]

volumes:
  postgres-data:

networks:
  pspad:
```

- [ ] **Step 4: Verify the image builds and the server starts**

Run: `docker compose -f docker/compose.prod.yaml --env-file docker/.env.example build`
Expected: build succeeds, image `pspad-server:local` exists.

- [ ] **Step 5: Document the entry points in the README**

Append to `README.md`:

```markdown
## Development

Everything runs in Docker, including the .NET SDK.

```bash
docker compose -f docker/compose.yaml up -d
```

Then open the repository in the dev container (`.devcontainer/`) and work there:

```bash
dotnet build
dotnet test
```

Design documents live in `docs/superpowers/specs/`, implementation plans in
`docs/superpowers/plans/`, and the working agreement for agents in `AGENTS.md`.
```

- [ ] **Step 6: Commit**

```bash
git add docker/ README.md
git commit -m "chore: add production image, prod compose and developer docs"
```

---

## Done when

- `docker compose -f docker/compose.yaml up -d` brings up healthy `postgres`, `postgres-test` and `keycloak`.
- The dev container builds and `dotnet --version` reports 10.x inside it.
- `dotnet test` runs both test projects green, including the domain purity guard and the live database check.
- `docker compose -f docker/compose.prod.yaml build` produces a runnable server image.
