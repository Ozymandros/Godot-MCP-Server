# [1.6.5] - 2026-04-21

### Added
- New `ResourceListAsync` tool: List Godot resource files (`.tres`, `.res`) in a project directory, with optional directory and resource type filters. Enables automation and scripting scenarios that require resource discovery.

### Changed
- Project version updated to 1.6.5 in all relevant project files.

### Notes
- This release introduces a generic resource enumeration tool for the first time. See the README and API docs for usage details.

# Changelog

All notable changes to this project should be documented in this file.

The format is inspired by Keep a Changelog and follows semantic versioning.

## [1.5.0] - 2026-04-20

### Added
- Accept absolute `projectPath` values for most MCP tooling (in addition to server-root-relative paths).
- Automatic project initialization: when a requested project directory does not contain `project.godot`, the server now auto-creates a minimal `project.godot` and the `scenes`, `scripts`, and `addons` directories so tools can proceed without manual editor initialization.
- Helpers to mutate a project's `project.godot` (`SetProjectConfigValueAsync`, `RemoveProjectConfigKeyAsync`) and internal APIs to perform in-place project-file edits.
- Integration test `ExternalProjectCreationTests` validating auto-create + autoload mutation.
- Documentation updates (README and `docs/index.md`) describing absolute path support and auto-initialization behavior.

### Changed
- `GodotTools` commands now accept absolute project paths and normalize the base directory used for create/mutate flows.
- `GodotFileService` now supports absolute file paths.
- `GetProjectInfoAsync` will auto-initialize missing `project.godot` instead of failing.
- Tools that previously relied solely on the server-configured `ProjectConfigService` will ensure and, when appropriate, mutate the `project.godot` at the requested project path.

### Fixed
- Path containment/validation bug in `ResolveProjectFilePath` that could allow invalid resolutions.
- Various compile-time issues found during implementation.

### Tests
- Added `ExternalProjectCreationTests`. All tests pass locally (191/191).

### Notes
- Allowing absolute `projectPath` enables creating projects outside the configured server root — review security and deployment policies if you want to restrict this behavior.
- If you rely on `IProjectConfigService` that targets the server `ProjectRoot`, be aware that some tools now mutate the per-project `project.godot` at the specified `projectPath`.


## [1.6.4] - 2026-04-20

### Changed
- Comprehensive XML documentation pass: updated all public GodotTools.* methods to ensure XML doc comments match method signatures and parameter names, eliminating CS1572/CS1573/CS1591 warnings and errors during build.
- All scene, camera, lighting, physics, UI, and script tool methods now have accurate `<param>` tags for `projectPath`, `fileName`, and other parameters.

### Fixed
- Build now passes with XML documentation warnings treated as errors (no doc comment mismatches remain).

### Notes
- This release is a documentation/quality update only. No runtime or API changes were made since 1.5.0.
