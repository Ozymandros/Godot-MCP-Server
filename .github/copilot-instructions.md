# GitHub Copilot — repository custom instructions

Persistent guidance for this repository. Adjust as the project evolves. For how these files work, see [About customizing GitHub Copilot responses](https://docs.github.com/en/copilot/concepts/prompting/response-customization) and [Adding repository custom instructions](https://docs.github.com/en/copilot/how-tos/configure-custom-instructions/add-repository-instructions).

## What this repository is

- **Godot MCP Server** — a .NET global tool that exposes Godot 4.x project automation over MCP stdio JSON-RPC (`GodotMCP.Server`, command `godot-mcp`).
- **Runtime**: .NET 10 (`net10.0`). Godot binary is external; `GODOT_PATH` or `godot` on PATH is expected for features that invoke the editor.
- **Critical operational rule**: When the server runs, **stdout is reserved for MCP protocol traffic**. Do not write unrelated output to stdout in server paths.

## Solution layout

| Area | Project / path |
|------|----------------|
| Host, CLI, DI | `GodotMCP.Server/` |
| Use cases, commands | `GodotMCP.Application/` |
| Domain | `GodotMCP.Core/` |
| Filesystem, config, platform | `GodotMCP.Infrastructure/` |
| Tests | `GodotMCP.Tests/` |

Solution file: `GodotMCP.slnx`.

## Build, test, and validation

After substantive changes, validate locally (same sequence as CI):

```powershell
dotnet restore GodotMCP.slnx
dotnet build GodotMCP.slnx -c Release --no-restore
dotnet test GodotMCP.slnx -c Release --no-build
```

Docker image check (optional): `docker build -t godot-mcp-server:local .` from repo root.

## C# and design preferences

- Prefer **clear names**, **small focused types**, and **early returns** for error paths.
- Use **XML documentation** on public APIs where it adds clarity (this repo enables XML doc generation on the server project).
- Respect **nullable reference types** and existing patterns in neighboring code; avoid drive-by refactors outside the requested change.
- Keep changes **scoped** to the task; explain non-obvious tradeoffs briefly when suggesting edits.

## Git, branches, and pull requests

- Do **not** merge into, push to, or rewrite **protected/default branches** without explicit user permission.
- Do **not** open or submit pull requests unless the user asked for that workflow.
- Prefer **small, reviewable** changes with accurate summaries when PRs are in scope.

## Collaboration and safety

- If requirements are ambiguous, **ask** rather than guessing behavior or public API shape.
- Call out **limitations and risks** of generated code; users should review and test before release.
- Treat **credentials, tokens, and signing material** as sensitive — never commit them; use existing configuration patterns.
