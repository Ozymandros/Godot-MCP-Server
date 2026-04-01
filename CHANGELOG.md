# Changelog

All notable changes to this project should be documented in this file.

The format is inspired by Keep a Changelog and follows semantic versioning.

## [Unreleased]

### Added

### Changed

### Fixed


## [1.1.0] - 2026-04-01

### Added
- Health and server info RPC endpoints (`health_check`, `get_server_info`).
- Docker/devcontainer/workflow hardening for stdio-compatible MCP usage.

### Changed
- MCP tool methods now expose explicit JSON-RPC method names.
- Serializer and validation robustness improvements.

### Fixed
- Path/input validation across tool surface for safer failure behavior.
