# Dev Environment & Solution Scaffold Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A fully containerized development environment where `dotnet test` runs green against a real PostgreSQL started by Testcontainers, with unit and integration suites separable, plus the empty solution structure every later plan builds into.

**Architecture:** Everything runs in Docker, including the .NET SDK. A dev container built on the SDK image is where builds, tests and `dotnet watch` execute, with the host Docker socket mounted so Testcontainers works from inside it. Compose supplies PostgreSQL and Keycloak for *running the app*; integration tests start their own disposable Postgres. The solution is a modular monolith: a pure domain, a shared contracts project, a server, a WASM client, and test projects sharing one test-infrastructure library.

**Tech Stack:** .NET 10, Docker Compose, PostgreSQL 17, Keycloak 26, Testcontainers, xUnit, Shouldly

**Spec:** `docs/superpowers/specs/2026-09-11-gtd-core-design.md`

## Global Constraints

- .NET 10 (LTS). All projects target `net10.0`.
- `PSPad.Domain` and `PSPad.Contracts` reference no infrastructure packages and must compile for WebAssembly.
- PostgreSQL 17. Marten 8.x. Wolverine 4.x. MudBlazor 8.x. xUnit + Shouldly.
- All user-facing dates are `DateOnly` in the user's time zone; all stored instants are UTC `DateTimeOffset`.
- No audit table — history comes from the event stream.
- Integration tests start their own Postgres through Testcontainers (AD-9). No compose service is shared with tests.
- Every test class carries `[UnitTest]` or `[IntegrationTest]`. Unmarked is a defect.
- No comments in code. Names carry the meaning. The prose and doc comments in this plan's snippets explain things to *you*; port only a comment that states a non-obvious *why*, and drop the rest.
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
- Produces: services `postgres` (port 5432) and `keycloak` (port 8080) on network `pspad`; connection string `Host=postgres;Port=5432;Database=pspad;Username=pspad;Password=pspad`

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

This stack is what you run the app against. Tests never touch it — they get
their own container (Task 4).

- [ ] **Step 2: Write the example environment file**

`docker/.env.example`:

```dotenv
POSTGRES_CONNECTION=Host=postgres;Port=5432;Database=pspad;Username=pspad;Password=pspad
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
Expected: `postgres` reports `healthy`, `keycloak` reports `running`.

- [ ] **Step 5: Commit**

```bash
git add docker/
git commit -m "chore: add docker compose infrastructure for running the app"
```

---

### Task 2: Dev container

**Files:**
- Create: `.devcontainer/devcontainer.json`
- Create: `.devcontainer/Dockerfile`

**Interfaces:**
- Consumes: the `pspad` network and services from Task 1
- Produces: a dev container where `dotnet` is on PATH, `postgres` and `keycloak` resolve by hostname, and `docker ps` works against the host daemon so Testcontainers can start containers

- [ ] **Step 1: Write the dev container Dockerfile**

`.devcontainer/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0

RUN apt-get update \
 && apt-get install -y --no-install-recommends git curl ca-certificates \
 && rm -rf /var/lib/apt/lists/*

RUN install -m 0755 -d /etc/apt/keyrings  && curl -fsSL https://download.docker.com/linux/debian/gpg -o /etc/apt/keyrings/docker.asc  && echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/debian $(. /etc/os-release && echo $VERSION_CODENAME) stable" > /etc/apt/sources.list.d/docker.list  && apt-get update  && apt-get install -y --no-install-recommends docker-ce-cli  && rm -rf /var/lib/apt/lists/*

RUN dotnet workload install wasm-tools

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_USE_POLLING_FILE_WATCHER=1

WORKDIR /workspace
```

`wasm-tools` is for the Blazor WebAssembly client in plan 06. The Docker CLI is
for Testcontainers: it talks to the *host* daemon through the mounted socket, so
test containers are siblings of the dev container, not children of it.

- [ ] **Step 2: Write the dev container definition**

`.devcontainer/devcontainer.json`:

```json
{
  "name": "PSPad",
  "build": { "dockerfile": "Dockerfile" },
  "runServices": ["postgres", "keycloak"],
  "dockerComposeFile": ["../docker/compose.yaml", "compose.devcontainer.yaml"],
  "service": "devcontainer",
  "workspaceFolder": "/workspace",
  "forwardPorts": [5000, 5432, 8080],
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
      - /var/run/docker.sock:/var/run/docker.sock
    command: sleep infinity
    networks: [pspad]
    environment:
      TESTCONTAINERS_HOST_OVERRIDE: host.docker.internal
    extra_hosts:
      - "host.docker.internal:host-gateway"
    depends_on:
      postgres:
        condition: service_healthy
```

`TESTCONTAINERS_HOST_OVERRIDE` matters: a container started on the host daemon
publishes its port on the *host*, not on this container's localhost. Without the
override, connection attempts go nowhere and every integration test times out.

- [ ] **Step 4: Verify connectivity from inside the container**

Run inside the dev container: `dotnet --version`
Expected: a `10.x` version string.

Run: `docker ps`
Expected: a container list, proving the mounted socket works. If this fails,
Testcontainers cannot run and Task 4 has nothing to stand on.

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
- Create: `test/PSPad.TestInfrastructure/PSPad.TestInfrastructure.csproj`
- Create: `test/PSPad.TestInfrastructure/Categories.cs`

**Interfaces:**
- Consumes: the dev container from Task 2
- Produces: assemblies `PSPad.Domain`, `PSPad.Contracts`, `PSPad.Server`, `PSPad.TestInfrastructure`; reference graph `Contracts -> Domain`, `Server -> Contracts`, every test project -> its subject and -> `PSPad.TestInfrastructure`; attributes `[UnitTest]` and `[IntegrationTest]` emitting the xUnit trait `Category`

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
dotnet new classlib -o test/PSPad.TestInfrastructure -f net10.0
dotnet sln add src/**/*.csproj test/**/*.csproj
```

- [ ] **Step 2: Wire the reference graph**

```bash
dotnet add src/PSPad.Contracts reference src/PSPad.Domain
dotnet add src/PSPad.Server reference src/PSPad.Contracts
dotnet add test/PSPad.Domain.Tests reference src/PSPad.Domain
dotnet add test/PSPad.Server.Tests reference src/PSPad.Server
dotnet add test/PSPad.Domain.Tests reference test/PSPad.TestInfrastructure
dotnet add test/PSPad.Server.Tests reference test/PSPad.TestInfrastructure
dotnet add test/PSPad.TestInfrastructure package xunit.abstractions
dotnet add test/PSPad.TestInfrastructure package xunit.extensibility.core
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

- [ ] **Step 4: Write the test category attributes**

`test/PSPad.TestInfrastructure/Categories.cs`:

```csharp
using Xunit.Sdk;

namespace PSPad.TestInfrastructure;

public static class Categories
{
    public const string Key = "Category";
    public const string Unit = "Unit";
    public const string Integration = "Integration";
}

[TraitDiscoverer("PSPad.TestInfrastructure.CategoryDiscoverer", "PSPad.TestInfrastructure")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class UnitTestAttribute : Attribute, ITraitAttribute;

[TraitDiscoverer("PSPad.TestInfrastructure.CategoryDiscoverer", "PSPad.TestInfrastructure")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class IntegrationTestAttribute : Attribute, ITraitAttribute;

public sealed class CategoryDiscoverer : ITraitDiscoverer
{
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        var category = traitAttribute.AttributeType.Name.StartsWith("Integration", StringComparison.Ordinal)
            ? Categories.Integration
            : Categories.Unit;

        yield return new KeyValuePair<string, string>(Categories.Key, category);
    }
}
```

If the installed xUnit major version has dropped `ITraitDiscoverer`, fall back to
plain subclasses of `TraitAttribute`, or to
`[Trait(Categories.Key, Categories.Unit)]` written out on each class. What
matters is that `dotnet test --filter Category=Unit` splits the suites.

- [ ] **Step 5: Write a guard test that the domain stays infrastructure-free**

`test/PSPad.Domain.Tests/PurityTests.cs`:

```csharp
using PSPad.TestInfrastructure;
using Shouldly;

namespace PSPad.Domain.Tests;

[UnitTest]
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

- [ ] **Step 6: Run the test to verify it fails**

Run: `dotnet test test/PSPad.Domain.Tests`
Expected: FAIL — `DomainMarker` does not exist.

- [ ] **Step 7: Add the marker type**

`src/PSPad.Domain/DomainMarker.cs`:

```csharp
namespace PSPad.Domain;

public sealed class DomainMarker;
```

`DomainMarker` is an anchor for assembly-level reflection and holds no behavior.

- [ ] **Step 8: Run the test to verify it passes and is categorized**

Run: `dotnet test test/PSPad.Domain.Tests`
Expected: PASS, 1 test.

Run: `dotnet test test/PSPad.Domain.Tests --filter Category=Unit`
Expected: PASS, 1 test — proving the trait is discovered. If this reports 0
tests, the attribute is not wired and every later plan inherits the problem.

- [ ] **Step 9: Commit**

```bash
git add PSPad.sln Directory.Build.props src/ test/
git commit -m "chore: scaffold solution with domain purity guard"
```

---

### Task 4: Testcontainers Postgres fixture

**Files:**
- Create: `test/PSPad.TestInfrastructure/PostgresFixture.cs`
- Create: `test/PSPad.TestInfrastructure/PostgresCollection.cs`
- Modify: `test/PSPad.TestInfrastructure/PSPad.TestInfrastructure.csproj`
- Test: `test/PSPad.Server.Tests/PostgresFixtureTests.cs`

**Interfaces:**
- Consumes: the Docker socket mounted in Task 2, the attributes from Task 3
- Produces: `PostgresFixture` (`IAsyncLifetime`) exposing `string ConnectionString`; `PostgresCollection` with `public const string Name = "postgres"`, joined by every integration test as `[Collection(PostgresCollection.Name)]`

One container per test run, shared through the collection fixture. Not one per
test — Postgres startup costs seconds, and per-test containers make the suite
unusable. Tests isolate by data instead: each uses a fresh `UserId`.

- [ ] **Step 1: Write the failing test**

`test/PSPad.Server.Tests/PostgresFixtureTests.cs`:

```csharp
using Npgsql;
using PSPad.TestInfrastructure;
using Shouldly;

namespace PSPad.Server.Tests;

[IntegrationTest]
[Collection(PostgresCollection.Name)]
public class PostgresFixtureTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Container_database_is_reachable()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand("select 1", connection);

        (await command.ExecuteScalarAsync()).ShouldBe(1);
    }

    [Fact]
    public void Connection_string_does_not_point_at_the_dev_database()
    {
        fixture.ConnectionString.ShouldNotContain("Host=postgres;");
    }
}
```

The second test is not ceremony: pointing the suite at the dev database is the
exact mistake this task exists to prevent, and it fails silently otherwise.

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test test/PSPad.Server.Tests`
Expected: FAIL — `PostgresFixture` and `PostgresCollection` do not exist.

- [ ] **Step 3: Add the packages**

```bash
dotnet add test/PSPad.TestInfrastructure package Testcontainers.PostgreSql
dotnet add test/PSPad.TestInfrastructure package xunit.core
dotnet add test/PSPad.Server.Tests package Npgsql
```

- [ ] **Step 4: Write the fixture**

`test/PSPad.TestInfrastructure/PostgresFixture.cs`:

```csharp
using Testcontainers.PostgreSql;
using Xunit;

namespace PSPad.TestInfrastructure;

public sealed class PostgresFixture : IAsyncLifetime
{
    readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("pspad_test")
        .WithUsername("pspad")
        .WithPassword("pspad")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
```

`test/PSPad.TestInfrastructure/PostgresCollection.cs`:

```csharp
using Xunit;

namespace PSPad.TestInfrastructure;

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
```

If the installed xUnit version renames `IAsyncLifetime`'s members
(`InitializeAsync`/`DisposeAsync` became `ValueTask` in v3), match the installed
signature. The shape — one container started once, disposed at the end — does
not change.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test test/PSPad.Server.Tests`
Expected: PASS, 2 tests. The first run pulls `postgres:17-alpine` and takes
longer; later runs start in a few seconds.

- [ ] **Step 6: Verify the suites split**

Run: `dotnet test --filter Category=Unit`
Expected: PASS, and no Docker container is started.

Run: `dotnet test --filter Category=Integration`
Expected: PASS, 2 tests.

- [ ] **Step 7: Commit**

```bash
git add test/
git commit -m "test: add testcontainers postgres fixture and category split"
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

- `docker compose -f docker/compose.yaml up -d` brings up a healthy `postgres` and a running `keycloak`.
- The dev container builds, `dotnet --version` reports 10.x inside it, and `docker ps` works from inside it.
- `dotnet test` is green: domain purity guard plus the Testcontainers database check.
- `dotnet test --filter Category=Unit` runs without starting a container; `--filter Category=Integration` starts one.
- `docker compose -f docker/compose.prod.yaml build` produces a runnable server image.
