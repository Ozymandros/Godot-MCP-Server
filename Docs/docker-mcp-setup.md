# Docker MCP Setup

## Build

```bash
docker build -t godot-mcp-server:latest .
```

## Run (stdio)

The server uses stdio JSON-RPC. Run it attached:

```bash
docker run --rm -i -v "$(pwd):/workspace" -w /workspace godot-mcp-server:latest
```

## Run with docker compose

```bash
docker compose run --rm godot-mcp
```

## Godot CLI support

- Mount a Godot project directory.
- Inject `GODOT_PATH` in container if Godot binary is present.
- Keep stdout clean for JSON-RPC (logs/errors go to stderr).

## Release images

Release workflow publishes a container image to GHCR:

- `ghcr.io/<owner>/<repo>/godot-mcp-server:<tag>`
