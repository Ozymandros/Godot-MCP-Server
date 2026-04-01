# Operations runner JSON contract

This document describes the request/response JSON envelope used between the C# runner and the bundled GDScript (`godot_operations.gd`).

Request envelope (written to payload.json)

{
  "schemaVersion": "1.0",
  "requestId": "<guid>",
  "operation": "create_scene",
  "projectRoot": "optional absolute path",
  "payload": { ... },
  "options": { "overwrite": false }
}

Response envelope (written to stdout exactly once)

{
  "schemaVersion": "1.0",
  "requestId": "<guid>",
  "success": true|false,
  "message": "human friendly",
  "data": { ... } | null,
  "error": { "code": "E_SAVE", "type": "Error", "message": "...", "trace": "..." } | null
}

Rules

- The GDScript must print the JSON response to stdout exactly once, then exit.
- All other logs/debug output should go to stderr.
- The runner parses stdout JSON and converts it to `ToolResult`.
- If stdout is empty or invalid JSON, the runner returns failure and attaches raw stdout/stderr to the result for diagnostics.

Examples

See code samples in the repository for `create_scene` and `add_node` payload examples.
