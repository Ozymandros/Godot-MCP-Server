# Testing

Unit tests

- Run all unit tests with `dotnet test`.
- Unit tests do not require Godot to be installed.

Integration tests (engine-backed)

- Integration tests that require Godot are gated. Set `GODOT_PATH` before running them:

```powershell
$env:GODOT_PATH = 'C:\\path\\to\\Godot.exe'
dotnet test
```

- In CI, configure the runner to include a Godot binary or set `GODOT_PATH` in the workflow environment.

Coverage and docs

- The projects are configured to generate XML documentation files during build. Ensure code has XML doc comments for public APIs.
- Use `dotnet test /p:CollectCoverage=true` with `coverlet` collector for coverage reporting.

New tests

- A new unit test class `GodotToolsIntegrationsTests` was added under `GodotMCP.Tests/Unit` to validate
  integration discovery, health verification and compatibility listing behaviors.

Running specific tests

- Run only the new unit tests with:

```powershell
dotnet test --filter FullyQualifiedName~GodotToolsIntegrationsTests
```

Coverage

- To collect coverage for the test run:

```powershell
dotnet test /p:CollectCoverage=true --filter FullyQualifiedName~GodotToolsIntegrationsTests
```

CI notes

- Integration tests that require Godot are still gated by `GODOT_PATH`. Unit tests do not require Godot.
