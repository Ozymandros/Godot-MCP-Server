# SDK Integration Policy

This project follows Godot ecosystem realities: community and vendor SDKs are supported as optional integration layers, not hard runtime dependencies.

## Profiles

- `official_engine_feature`
- `community_sdk`
- `vendor_sdk`
- `project_local_plugin`

## Lifecycle

`discover -> evaluate -> install -> configure -> verify -> upgrade -> retire`

## Rules

- Core MCP operations must work even if external SDKs are unavailable.
- SDK operations must return actionable remediation on failures.
- Compatibility checks should include Godot version and platform metadata.
