<div align="center">

  <img src="brand/icon.svg" alt="PSPad logo" width="96" height="96" />

  <h1>PSPad</h1>
  <p>A self-hosted GTD notepad that works offline, on your own hardware</p>

  <div>
    <a href="https://github.com/psp515/PSPad/actions/workflows/ci.yml">
      <img src="https://img.shields.io/github/actions/workflow/status/psp515/PSPad/ci.yml?branch=main&label=CI" alt="CI status" />
    </a>
    <a href="https://psp515.github.io/PSPad/">
      <img src="https://img.shields.io/github/deployments/psp515/PSPad/github-pages?label=docs" alt="docs" />
    </a>
    <a href="">
      <img src="https://img.shields.io/github/last-commit/psp515/PSPad" alt="last update" />
    </a>
    <a href="https://github.com/psp515/PSPad/network/members">
      <img src="https://img.shields.io/github/forks/psp515/PSPad" alt="forks" />
    </a>
    <a href="https://github.com/psp515/PSPad/stargazers">
      <img src="https://img.shields.io/github/stars/psp515/PSPad" alt="stars" />
    </a>
    <a href="https://github.com/psp515/PSPad/issues/">
      <img src="https://img.shields.io/github/issues/psp515/PSPad" alt="open issues" />
    </a>
    <a href="https://github.com/psp515/PSPad/blob/main/LICENSE">
      <img src="https://img.shields.io/github/license/psp515/PSPad" alt="license" />
    </a>
  </div>
</div>

<br/>

### Built With

![.NET](https://img.shields.io/badge/.NET%2010-512BD4?style=for-the-badge&logo=dotnet&logoColor=white&style=flat)
![Blazor](https://img.shields.io/badge/-Blazor%20WebAssembly-512BD4?style=for-the-badge&logo=blazor&logoColor=white&style=flat)
![MongoDB](https://img.shields.io/badge/-MongoDB-47A248?style=for-the-badge&logo=mongodb&logoColor=white&style=flat)
![Docker](https://img.shields.io/badge/-Docker-2496ED?style=for-the-badge&logo=docker&logoColor=white&style=flat)
![Keycloak](https://img.shields.io/badge/-Keycloak-4D4D4D?style=for-the-badge&logo=keycloak&logoColor=white&style=flat)

## About the project

PSPad is my own vision of a notepad helping with implementing GTD ideas. One
Today screen answers "what do I do now" across every part of your life, one
Inbox catches everything else, and the whole thing keeps working on a phone
with no signal.

- **One Today screen.** Due today or earlier, overdue pinned to the top —
  worked out in your own time zone, never the server's.
- **One Inbox.** Capture anything, decide where it belongs later.
- **Areas and lists** you define yourself — work, home, a side project —
  each with its own lists.
- **Tasks with steps.** Due dates, priorities, goals, stars, and ordered
  steps with their own dates.
- **Recurrence that never nags.** Miss a day and it just stays missed —
  never overdue.
- **Goals across areas.** Global, not filed inside one area, because the
  things worth calling goals rarely stay in one.
- **A real history.** Every change logged automatically, read back from the
  event log — no separate audit table to drift out of sync.
- **Works with no signal.** Saves to a local replica first, syncs the moment
  a connection returns.
- **Multi-user**, through your own Keycloak. Everyone gets their own
  private areas, lists and Inbox.

## Documentation

Full docs live at **[psp515.github.io/PSPad](https://psp515.github.io/PSPad/)**
(built with Astro):

- [Features](https://psp515.github.io/PSPad/features/) — what's built, what's
  in progress, and what's still only planned.
- [Install](https://psp515.github.io/PSPad/install/) — run PSPad on your own
  hardware with Docker Compose, from a clone to your first sign-in.

## Development

Develop on your own machine with the .NET 10 SDK. Docker supplies the backing
services and, for integration tests, a throwaway MongoDB.

```bash
cp docker/.env.example docker/.env
docker compose -f docker/compose.yaml up -d
dotnet build
dotnet test
```

Integration tests do not use the stack above — they start their own MongoDB
through Testcontainers, so Docker must be running:

```bash
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
```

Each deployable carries its own `Dockerfile` next to its `.csproj`
(`src/PSPad.Api`, `src/PSPad.App`), built with the repository root as
context. `docker/compose.yaml` is both the development and the self-hosting
stack — self-hosting is documented in full on the
[install page](https://psp515.github.io/PSPad/install/).

Design specs live in `specs/`, architecture decision records in `adr/` (see
`adr/README.md` for the index — they carry the reasoning and rejected
alternatives behind each decision; where a spec and an ADR disagree, the ADR
is the decision of record), and the working agreement for agents is in
`AGENTS.md`.

## Contributing

This is a hobby-driven, personal-vision project first, but contributions,
bug reports and ideas are welcome:

- ⭐ **Star** the repo if you find it useful — it helps others find it too.
- 🐛 Open an [issue](https://github.com/psp515/PSPad/issues) for bugs or
  feature requests.
- 🔀 Fork it and send a pull request — `AGENTS.md` covers how the pieces fit
  together, and `adr/README.md` the decisions already made.

## License

Distributed under the GPL v3 License. See `LICENSE` for more information.
