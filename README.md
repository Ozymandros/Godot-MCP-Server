# Godot MCP Server

`Godot MCP Server` is a .NET global tool that exposes automation capabilities for Godot 4.x projects over a simple MCP (Model Context Protocol) stdio JSON-RPC transport.

Key features

- Godot-native scene, resource, and importer handling
- Stdio JSON-RPC transport for local and CI automation
- Headless CLI automation suitable for build servers and containers
- SDK-aware plugin discovery and health checks
- Docker and VS Code devcontainer support for reproducible development

Prerequisites

- .NET 10 SDK
- Godot 4.x executable
- `GODOT_PATH` environment variable pointing to the Godot binary (or ensure `godot` is on PATH)

Install (local build)

Run the following from the repository root to pack and install the tool locally:

```powershell
dotnet pack .\GodotMCP.Server\GodotMCP.Server.csproj -c Release
dotnet tool install --global --add-source .\GodotMCP.Server\nupkg GodotMCP.Server
```

Run

Start the server from a terminal:

```powershell
godot-mcp
```

Notes

- The server uses stdio for protocol messages and reserves stdout for MCP traffic; avoid printing other data to stdout when the server is running.

Solution layout

- `GodotMCP.Server` — MCP host, CLI entrypoint, and DI composition root
- `GodotMCP.Application` — use-cases, commands, and public contracts
- `GodotMCP.Core` — domain models and interfaces
- `GodotMCP.Infrastructure` — filesystem, configuration, serializer, and platform integrations
- `GodotMCP.Tests` — unit and integration tests

Containerized development

- Runtime container: see `Dockerfile` and `Docs/docker-mcp-setup.md`
- Dev container configuration: see `.devcontainer/` and `Docs/vscontainer-setup.md`
- Local compose entrypoint: `docker-compose.yml`

Cross-platform & deployment notes

- Godot binary discovery: the server prefers the `GODOT_PATH` environment variable but also searches `PATH` and common installation locations on Windows, macOS, and Linux. Examples:
  - Windows: `C:\\Program Files\\Godot Engine\\godot.exe`
  - macOS: `/Applications/Godot.app/Contents/MacOS/Godot`
  - Linux: `/usr/bin/godot` or `/usr/local/bin/godot`

- Environment examples (PowerShell):
  - `$env:GODOT_PATH = 'C:\\Program Files\\Godot Engine\\godot.exe'`
  - `$env:GODOT_PATH = '/usr/bin/godot'` (WSL or Linux)

- Operations runner: complex operations that require Godot's internal APIs (UID assignment, PackedScene instantiation, resource re-saving) are executed by a headless Godot process using a bundled GDScript. This is intentionally optional — lightweight, fast edits are still performed by the .NET text-based serializers. Use the operations runner for safety-critical operations and the text-based path for bulk scaffolding.

- CI / Tests: If CI needs to run Godot-dependent integration tests, set `GODOT_PATH` in the CI environment or run on a runner image that includes Godot. Tests that do not require Godot will continue to run on any platform.

- Docker / multi-arch images: the repository's GitHub Actions workflow builds and publishes container images. The Docker build in CI is multi-architecture (for example `linux/amd64` and `linux/arm64`) so images can run on common Linux platforms. If you build locally, use Docker Buildx to build multi-arch images.


Release artifacts

- NuGet global tool package: `GodotMCP.Server`
- Container image: `ghcr.io/<org>/godot-mcp-server` (example)

Release process

- Changelog: `CHANGELOG.md`
- Release checklist: `Docs/release-checklist.md`

Contributing

If you plan to contribute, read `Docs/contributing.md` for guidelines, tests, and code style. Create small, focused pull requests and include tests where appropriate.

License

See `LICENSE` for licensing information.

