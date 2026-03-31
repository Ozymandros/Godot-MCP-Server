#!/usr/bin/env bash
set -euo pipefail
dotnet pack ./GodotMCP.Server/GodotMCP.Server.csproj -c Release
dotnet tool install --global --add-source ./GodotMCP.Server/nupkg GodotMCP.Server
