# Godot MCP Server

Automated API documentation for the **Godot MCP Server** solution, generated with [DocFX](https://dotnet.github.io/docfx/).

Use the navigation bar to browse conceptual articles and the .NET API reference.

**Published site:** [https://ozymandros.github.io/Godot-MCP-Server/](https://ozymandros.github.io/Godot-MCP-Server/) — deployed from the default branch via [`.github/workflows/docs.yml`](https://github.com/Ozymandros/Godot-MCP-Server/blob/main/.github/workflows/docs.yml) when documentation-related paths change. If the URL does not resolve, [configure GitHub Pages](https://docs.github.com/en/pages/getting-started-with-github-pages/configuring-a-publishing-source-for-your-github-pages-site) for [`Ozymandros/Godot-MCP-Server`](https://github.com/Ozymandros/Godot-MCP-Server).

**Preview locally:** do not rely on opening `_site/index.html` via `file://`. Use `dotnet docfx docs/docfx.json --serve` and browse the printed `http://localhost` URL so the table of contents, navbar, and search load correctly.

## Maintainer workflow

- **Regenerate after API or XML-comment changes** — Run a Release build, then DocFX, so the API section reflects new types and comments:

  ```shell
  dotnet tool restore
  dotnet build GodotMCP.slnx -c Release
  dotnet docfx docs/docfx.json
  ```

- **Conceptual docs** — Add or edit Markdown under this folder (for example `articles/`) and update `toc.yml` so pages appear in the table of contents.

- **New assemblies** — If you add a project that should appear in the API reference, add it under `metadata` → `src` in `docfx.json` (paths are relative to the `docs/` directory).

- **Do not commit generated API YAML** — `docs/api/` is ignored by Git; it is recreated every time DocFX metadata runs.

- **CI** — The `docs` GitHub Actions workflow builds and publishes the site; keep the same commands working locally before pushing.

- **Preview** — Run `dotnet docfx docs/docfx.json --serve` and use the HTTP URL (avoid opening `_site/index.html` via `file://`; see note at the top of this page).

## Build locally

From the repository root:

```shell
dotnet tool restore
dotnet build GodotMCP.slnx -c Release
dotnet docfx docs/docfx.json
```

Alternatively, building the documentation project directly (not as part of a full solution build) runs the same DocFX pipeline:

```shell
dotnet build Documentation/Documentation.csproj -c Release
```

Static HTML is written to `_site/` at the solution root.
