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
