# PSPad
My vision of notepad with GTD practicies.

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

The production stack (`docker/compose.prod.yaml`) serves plain HTTP on port
5000 and expects a reverse proxy in front of it.

Design documents live in `docs/superpowers/specs/`, implementation plans in
`docs/superpowers/plans/`, and the working agreement for agents in `AGENTS.md`.
