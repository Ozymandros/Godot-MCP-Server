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
- Scene Graph tooling (list/add/remove/move/rename nodes and inspect/update properties)
- Resource Pipeline tooling (read/write/update/remove properties on .tres/.res assets)
- UI tooling (list/add controls, apply layout presets, and update control properties)
- Lighting tooling (list/create/update/validate light nodes across scenes)
- Physics tooling (list/create/update/validate body and collision setup)
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

Scene Graph Tools

The server includes namespaced scene graph commands that operate on `.tscn` files without launching the Godot runtime:

- `scene.list_nodes`: returns the full node hierarchy for a scene with `name`, `type`, `nodePath`, `parent`, `children`, `script`, and basic `properties`.
- `scene.add_node`: creates a node under the provided parent path and saves the scene.
- `scene.remove_node`: removes a node subtree and saves the scene.
- `scene.move_node`: reparents a node to a new parent path and saves the scene.
- `scene.rename_node`: renames a node and saves the scene.
- `scene.get_node_properties`: returns a dictionary of node properties.
- `scene.set_node_properties`: updates only provided properties with primitive type validation and saves the scene.

Resource Pipeline Tools

The server includes namespaced resource commands for manipulating `.tres` and `.res` files:

- `resource.read`: returns typed resource payloads with `type` and `properties`.
- `resource.write`: writes a full resource document from `type` and property map.
- `resource.update_properties`: updates only provided property keys.
- `resource.remove_property`: removes a single property key.

UI Tools

The server includes namespaced UI commands for control-oriented scene workflows:

- `ui.list_controls`: returns UI control nodes and their serialized properties.
- `ui.add_control`: creates a control node under a parent path with optional initial properties.
- `ui.set_layout_preset`: applies one of `full_rect`, `top_left`, or `center` to a control.
- `ui.set_control_properties`: updates selected control properties with primitive value validation.

Lighting Tools

The server includes namespaced lighting commands for headless scene illumination workflows:

- `light.list`: scans scenes and returns light nodes with energy/color/shadow metadata.
- `light.create`: creates a light under a parent path with optional presets (`sun`, `fill`, `spot`).
- `light.update`: updates selected light properties with type validation.
- `light.validate`: returns lint-style lighting issues (for example non-positive or extreme intensity).

Physics Tools

The server includes namespaced physics commands for headless body and collision workflows:

- `physics.list_bodies`: scans scenes and returns physics body nodes with collision metadata.
- `physics.create_body`: creates a body node and can auto-add a `CollisionShape` child.
- `physics.update_body`: updates selected body properties with type validation.
- `physics.validate`: reports lint-style physics issues (for example invalid masks or missing collision shapes).

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

