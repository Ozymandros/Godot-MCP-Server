# Testing Guide

## Run All Tests

```powershell
dotnet test .\GodotMCP.slnx -c Release
```

## Coverage Focus

- Scene serializer round-trip.
- Resource/import generation.
- Project config mutation.
- Path resolver sandbox behavior.
- Integration discovery and health checks.

## Integration Notes

Integration tests that invoke Godot CLI require `GODOT_PATH`.
