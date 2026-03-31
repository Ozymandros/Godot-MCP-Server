FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "GodotMCP.slnx"
RUN dotnet publish "GodotMCP.Server/GodotMCP.Server.csproj" -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /out ./

# Stdio MCP server; keep container alive only while client is connected.
ENTRYPOINT ["./GodotMCP.Server"]
