# Contributing

Please follow these guidelines when contributing:

- Create small, focused pull requests.
- Add unit tests for new functionality; integration tests for engine-backed features should be gated and documented.
- Keep public APIs documented with triple-slash XML comments.
- Follow existing project coding style and patterns (Clean Architecture, DI, SOLID).

Development workflow

- Build: `dotnet build`
- Test: `dotnet test`
- Pack tool: `dotnet pack GodotMCP.Server -c Release`
