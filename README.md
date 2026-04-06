# Godot MCP Server

<!-- Build -->
[![CI](https://github.com/Ozymandros/Godot-MCP-Server/actions/workflows/ci.yml/badge.svg)](https://github.com/Ozymandros/Godot-MCP-Server/actions/workflows/ci.yml) [![Release](https://github.com/Ozymandros/Godot-MCP-Server/actions/workflows/release.yml/badge.svg)](https://github.com/Ozymandros/Godot-MCP-Server/actions/workflows/release.yml)

<!-- Package -->
[![NuGet](https://img.shields.io/nuget/v/GodotMCP.Server.svg)](https://www.nuget.org/packages/GodotMCP.Server/) [![.NET](https://img.shields.io/badge/.NET-10-512BD4.svg?logo=.net)](https://dotnet.microsoft.com/)

`Godot MCP Server` is a .NET global tool that exposes automation capabilities for Godot 4.x projects over a simple MCP (Model Context Protocol) stdio JSON-RPC transport.

Key features

- Godot-native scene, resource, and importer handling
- Animation generation (AnimationPlayer, Libraries, Tracks, and Keys)
- Camera tooling (list/create/update/validate across scenes)
- Scene comparison and structural diffing
- Project linting (missing imports, broken dependencies)
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

Camera Tools

The server includes headless camera commands that operate directly on `.tscn` files without launching the Godot runtime:

- `camera.list`: scans scenes and returns all Camera2D and Camera3D nodes with scene path, node path, type, fov/size, near/far, projection, and current flag.
- `camera.create`: inserts a Camera2D/Camera3D node in a scene and supports `cinematic`, `orthographic-ui`, and `fps` presets.
- `camera.update`: updates only the provided camera properties with validation for property names and value types.
- `camera.validate`: returns lint-style issues for multiple current cameras in one scene, invalid near/far ranges, missing parents, and unsupported projection modes.

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

