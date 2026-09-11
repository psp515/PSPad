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

Design documents live in `docs/superpowers/specs/`, implementation plans in
`docs/superpowers/plans/`, and the working agreement for agents in `AGENTS.md`.
