# Godot MCP Server

<!-- Build -->
[![CI](https://github.com/Ozymandros/Godot-MCP-Server/actions/workflows/ci.yml/badge.svg)](https://github.com/Ozymandros/Godot-MCP-Server/actions/workflows/ci.yml) [![Release](https://github.com/Ozymandros/Godot-MCP-Server/actions/workflows/release.yml/badge.svg)](https://github.com/Ozymandros/Godot-MCP-Server/actions/workflows/release.yml)

<!-- Documentation (DocFX on GitHub Pages) -->
[![Documentation](https://img.shields.io/badge/documentation-GitHub%20Pages-0366d6?logo=github)](https://ozymandros.github.io/Godot-MCP-Server/)

<!-- Package -->
[![NuGet](https://img.shields.io/nuget/v/GodotMCP.Server.svg)](https://www.nuget.org/packages/GodotMCP.Server/) [![.NET](https://img.shields.io/badge/.NET-10-512BD4.svg?logo=.net)](https://dotnet.microsoft.com/)

`Godot MCP Server` is a .NET global tool that exposes automation capabilities for Godot 4.x projects over a simple MCP (Model Context Protocol) stdio JSON-RPC transport.

Key features

- Godot-native scene, resource, and importer handling
- Animation generation (AnimationPlayer, Libraries, Tracks, and Keys)
- Camera tooling (list/create/update/validate across scenes)
- Scene Graph tooling (list/add/remove/move/rename nodes and inspect/update properties)
- Resource Pipeline tooling (read/write/update/remove properties on .tres/.res assets, and list resources with new generic resource enumeration tool)
## 1.6.5 Highlights

- Added: Generic resource listing tool (`ResourceListAsync`) for `.tres` and `.res` files, with directory and type filters.

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

 - Project paths: Most tools accept an absolute `projectPath` in addition to paths relative to the configured server project root. When a requested project folder does not contain a `project.godot`, the server will automatically create a minimal `project.godot` and the `scenes`, `scripts`, and `addons` directories so tooling can proceed without manual initialization.
 - Scene files: Tools that mutate a `.tscn` together with `projectPath` resolve the scene under `projectPath/scenes/` (same contract as `scene.add_node` and `scene.list_nodes`). Pass `fileName` such as `Main.tscn` or `scenes/Main.tscn` (leading `scenes/` duplicates are merged with the base `scenes` folder). Optional `root_type` is used only when the scene file is bootstrapped because it is missing. Disk-first tools (`create_script`, `create_texture`, `create_audio`, `create_resource`) accept optional link parameters to attach or assign in one step when you pass the full set documented on each tool; otherwise use `attach_script`, `resource.assign_texture`, or `scene.set_node_properties` in a follow-up call.
 - Input and signals: Input-map tooling includes explicit CRUD (`project.input_list_actions`, `project.input_add_action`, `project.input_update_action`, `project.input_remove_action`, `project.input_add_event`, `project.input_remove_event`) with deterministic event identity for `key`, `mouse_button`, `joypad_button`, and `joypad_motion`. Signal tooling includes `scene.list_connections`, `scene.add_connection`, `scene.update_connection`, and `scene.remove_connection` with strict connection-key matching, node existence checks, known-signal validation (best effort), and target-method validation (best effort).
 - Collision/shape authoring: Physics tooling includes shape/polygon lifecycle commands (`physics.add_shape`, `physics.update_shape`, `physics.remove_shape`, `physics.add_collision_polygon`, `physics.update_collision_polygon`, `physics.remove_collision_polygon`) plus assignment/flags operations (`physics.assign_shape_resource`, `physics.set_shape_flags`) for explicit collision workflows.
 - Resource listing: The new `ResourceListAsync` tool enables automation and scripting scenarios that require discovery of all Godot resource files in a project or subdirectory, with optional filtering by resource type.
Solution layout

- `GodotMCP.Server` — MCP host, CLI entrypoint, and DI composition root
- `GodotMCP.Application` — use-cases, commands, and public contracts
- `GodotMCP.Core` — domain models and interfaces
- `GodotMCP.Infrastructure` — filesystem, configuration, serializer, and platform integrations
- `GodotMCP.Tests` — unit and integration tests
- `docs/` — DocFX configuration and **conceptual** Markdown (for example `articles/`, `toc.yml`, `index.md`)
- `Documentation/` — MSBuild project (`Documentation.csproj`) that runs the DocFX pipeline when built **on its own** (see below)

API and documentation site (DocFX)

The public .NET API reference and the static documentation site are generated with [DocFX](https://dotnet.github.io/docfx/) from `docs/docfx.json`. Generated output is written to `_site/` at the repository root. That folder and `docs/api/` (intermediate YAML from metadata) are **gitignored**; they must be produced locally or by CI.

**Live site:** [https://ozymandros.github.io/Godot-MCP-Server/](https://ozymandros.github.io/Godot-MCP-Server/) — same target as the **Documentation** badge under the title. The DocFX output is deployed by [`.github/workflows/docs.yml`](.github/workflows/docs.yml) when you push; if the URL does not load yet, [enable GitHub Pages](https://docs.github.com/en/pages/getting-started-with-github-pages/configuring-a-publishing-source-for-your-github-pages-site) for this repository.

**Keeping docs up to date (maintainers)**

1. **Restore the doc tool** (once per clone or after manifest changes):

   ```powershell
   dotnet tool restore
   ```

2. **Build the solution** so XML documentation files exist for all projects (DocFX consumes the compiler-generated XML):

   ```powershell
   dotnet build GodotMCP.slnx -c Release
   ```

3. **Regenerate the full site** (API metadata + HTML). From the repository root, prefer the top-level DocFX entrypoint so metadata always runs:

   ```powershell
   dotnet docfx docs/docfx.json
   ```

   Alternatively, build only the documentation project (this runs the same pipeline; it does **not** run when building the whole solution, to keep normal builds fast):

   ```powershell
   dotnet build Documentation/Documentation.csproj -c Release
   ```

4. **Preview** the site over **HTTP**, not by double‑clicking `_site/index.html`. DocFX’s navigation and search load extra files (`toc.html`, search index, web workers) with JavaScript; most browsers block those requests from the `file://` protocol, which leaves the top nav empty and can make the page look almost blank even though the HTML file contains the article body.

   ```powershell
   dotnet docfx docs/docfx.json --serve
   ```

   Then open the URL shown in the terminal (for example `http://localhost:8080`). To use another port: `dotnet docfx docs/docfx.json --serve --port 8090`.

5. **Conceptual pages** live under `docs/` (for example `docs/articles/`). Edit Markdown and `docs/toc.yml` as needed; API namespaces and types come from the projects listed in `docs/docfx.json`—add a new project there only if you introduce a new documented assembly.

6. **Published site**: pushes to the default branch that touch documentation-related paths trigger [`.github/workflows/docs.yml`](.github/workflows/docs.yml). The deployed site is **[https://ozymandros.github.io/Godot-MCP-Server/](https://ozymandros.github.io/Godot-MCP-Server/)** (configure the **github-pages** environment and [publishing source](https://docs.github.com/en/pages/getting-started-with-github-pages/configuring-a-publishing-source-for-your-github-pages-site) if needed).

7. **Agents**: the MCP tool `query_system_documentation` searches `_site/manifest.json` and conceptual Markdown under `docs/` after a local build.

More detail for editors is in [`docs/index.md`](docs/index.md).

Containerized development

- Runtime container: see `Dockerfile` and `docs/docker-mcp-setup.md`
- Dev container configuration: see `.devcontainer/` and `docs/vscontainer-setup.md`
- Local compose entrypoint: `docker-compose.yml`

Camera Tools

The server includes headless camera commands that operate directly on `.tscn` files without launching the Godot runtime:

- `camera.list`: scans scenes and returns all Camera2D and Camera3D nodes with scene path, node path, type, fov/size, near/far, projection, and current flag.
- `camera.create`: inserts a Camera2D/Camera3D node in a scene and supports `cinematic`, `orthographic-ui`, and `fps` presets.
- `camera.update`: updates only the provided camera properties with validation for property names and value types. Also accepts an optional `rawContent` (and `fileService`) parameter — when provided the server writes the supplied text verbatim to replace the entire scene file (client-driven full-file workflows).
- `camera.validate`: returns lint-style issues for multiple current cameras in one scene, invalid near/far ranges, missing parents, and unsupported projection modes.

Scene Graph Tools

The server includes namespaced scene graph commands that operate on `.tscn` files without launching the Godot runtime:

- Scene path contract: tools resolve scene files as `projectPath + /scenes/ + fileName`.
- `fileName` must end with `.tscn` (invalid extensions fail with actionable errors).
- Bootstrap behavior: if the target scene does not exist, the server creates a minimal valid scene using `root_type` (default `Node`) before applying node/property operations.

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
- `light.update`: updates selected light properties with type validation. Also accepts an optional `rawContent` (and `fileService`) parameter — when provided the server writes the supplied text verbatim to replace the entire scene file.
- `light.validate`: returns lint-style lighting issues (for example non-positive or extreme intensity).

Physics Tools

The server includes namespaced physics commands for headless body and collision workflows:

- `physics.list_bodies`: scans scenes and returns physics body nodes with collision metadata.
- `physics.create_body`: creates a body node and can auto-add a `CollisionShape` child.
- `physics.update_body`: updates selected body properties with type validation.
- `physics.validate`: reports lint-style physics issues (for example invalid masks or missing collision shapes).
- `physics.add_shape` / `physics.update_shape` / `physics.remove_shape`: manage `CollisionShape2D/3D` nodes with explicit shape kind/parameter payloads.
- `physics.add_collision_polygon` / `physics.update_collision_polygon` / `physics.remove_collision_polygon`: manage `CollisionPolygon2D/3D` nodes and polygon payloads.
- `physics.assign_shape_resource`: assigns explicit shape resource expressions (for example sub/ext resource references) to collision shape nodes.
- `physics.set_shape_flags`: sets `disabled`, `one_way_collision`, `one_way_collision_margin`, and `platform_on_leave` style flags when supported by the target node type.
- `physics.area_set_monitoring`: sets `monitoring`/`monitorable` on `Area2D`/`Area3D`.
- `physics.area_set_priority`: sets `priority` on `Area2D`/`Area3D`.
- `physics.area_set_space_override`: sets `space_override` mode (`disabled`, `combine`, `combine_replace`, `replace`, `replace_combine`) and optional gravity/damping overrides.
- `physics.area_set_collision_filters`: sets `collision_layer`/`collision_mask` on `Area2D`/`Area3D` (both must be > 0).

Release artifacts

- NuGet global tool package: `GodotMCP.Server`
- Container image: `ghcr.io/<org>/godot-mcp-server` (example)

Release process

- Changelog: `CHANGELOG.md`
- Release checklist: `docs/release-checklist.md`

Contributing

If you plan to contribute, read `docs/contributing.md` for guidelines, tests, and code style. Create small, focused pull requests and include tests where appropriate.

License

See `LICENSE` for licensing information.

