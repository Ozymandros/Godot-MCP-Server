# install-tool.ps1 — Build, pack, and update the Godot MCP Server .NET global tool (v3.0.0+)
#
# Usage: Run this script from the repo root to update your global godot-mcp tool.
#
# Steps performed:
#   1. Uninstall any existing global GodotMCP.Server tool
#   2. Build the solution in Release mode
#   3. Pack the server project (creates .nupkg in GodotMcp.Server/nupkg/)
#   4. Update the global tool from the nupkg directory
#
# Equivalent manual commands:
#   dotnet tool uninstall --global GodotMCP.Server
#   dotnet build Godot-MCP-Server.sln --configuration Release
#   dotnet pack GodotMCP.Server/GodotMCP.Server.csproj -c Release -o GodotMcp.Server/nupkg
#   dotnet tool update --global --add-source GodotMcp.Server/nupkg GodotMCP.Server
#
# To test the tool with Inspector:
#   npx @modelcontextprotocol/inspector godot-mcp

# Resolve script root reliably (works when invoked from any cwd)
$scriptRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }

Write-Host "Uninstalling any existing GodotMCP.Server tool..." -ForegroundColor Yellow
dotnet tool uninstall --global GodotMCP.Server 2>$null

Write-Host "Building solution in Release mode..." -ForegroundColor Cyan
dotnet build Godot-MCP-Server.sln --configuration Release
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed."
    exit 1
}

# Use a relative path based on the script location and ensure directory exists
$nupkgPath = Join-Path -Path $scriptRoot -ChildPath "GodotMCP.Server\nupkg"
if (-not (Test-Path $nupkgPath)) {
    New-Item -ItemType Directory -Path $nupkgPath | Out-Null
}

Write-Host "Packing GodotMcp.Server project in Release mode (output -> $nupkgPath)..." -ForegroundColor Cyan
# Pack only the server project (PackageId: GodotMCP.Server, tool name: godot-mcp)
dotnet pack (Join-Path $scriptRoot "GodotMCP.Server\GodotMCP.Server.csproj") -c Release -o $nupkgPath
if ($LASTEXITCODE -ne 0) {
    Write-Error "Pack failed."
    exit 1
}

Write-Host "Updating global tool from $nupkgPath..." -ForegroundColor Green
dotnet tool update --global --add-source $nupkgPath GodotMCP.Server
if ($LASTEXITCODE -eq 0) {
    Write-Host "`nInstallation successful!" -ForegroundColor Green
    Write-Host "You can now run the server from anywhere using: " -NoNewline
    Write-Host "godot-mcp" -ForegroundColor Cyan
    Write-Host "\nTo test with Inspector, run: npx @modelcontextprotocol/inspector godot-mcp" -ForegroundColor Magenta
} else {
    Write-Error "Installation failed."
    exit 1
}
