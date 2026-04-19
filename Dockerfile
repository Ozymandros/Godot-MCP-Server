FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "GodotMCP.slnx"
RUN dotnet publish "GodotMCP.Server/GodotMCP.Server.csproj" -c Release -o /out --no-restore

# Static site + manifest for query_system_documentation (DocFX).
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet tool install -g docfx \
    && docfx docs/docfx.json

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

# So QuerySystemDocumentationAsync can resolve docs/docfx.json and _site/manifest.json without extra mounts.
ENV GODOT_MCP_REPO_ROOT=/app

COPY --from=build /out ./
COPY --from=build /src/docs ./docs
COPY --from=build /src/_site ./_site

# Stdio MCP server; keep container alive only while client is connected.
ENTRYPOINT ["./GodotMCP.Server"]
