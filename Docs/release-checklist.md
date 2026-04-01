# Release Checklist

Use this checklist before creating a version tag (`vX.Y.Z`).

## Quality gates

- [ ] `dotnet build GodotMCP.slnx -c Release`
- [ ] `dotnet test GodotMCP.slnx -c Release`
- [ ] Verify no new diagnostics in edited files
- [ ] Confirm stdio startup behavior (no protocol noise on stdout)

## Packaging

- [ ] NuGet package is generated from `GodotMCP.Server`
- [ ] Docker image builds from `Dockerfile`
- [ ] Release notes drafted from `CHANGELOG.md`

## Publish

- [ ] Create and push tag `vX.Y.Z`
- [ ] Verify `publish-nuget` workflow succeeded
- [ ] Verify `publish-container` workflow pushed GHCR image
- [ ] Smoke-test installed tool (`godot-mcp`) on clean environment
