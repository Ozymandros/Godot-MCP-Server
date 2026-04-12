using System.ComponentModel;
using System.Reflection;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using ModelContextProtocol.Server;

namespace GodotMCP.Application.Tools;

[McpServerToolType]
public static partial class GodotTools
{
    [McpServerTool(Name = "get_server_capabilities"), Description("Enumerate Godot MCP server capabilities and supported features.")]
    public static ToolResult GetServerCapabilities()
    {
        var capabilities = new Dictionary<string, string>
        {
            ["parity_doctrine"] = "mutatis-mutandis",
            ["scenes"] = "create/edit/remove/instantiate/diff/scene-graph",
            ["resources"] = "create/read/write/update/remove-property",
            ["ui"] = "list/add/set-layout/set-properties",
            ["lighting"] = "list/create/update/validate",
            ["physics"] = "list/create/update/validate",
            ["animations"] = "player/add/track/keys",
            ["scripts"] = "gdscript/csharp/create/attach/validate",
            ["imports"] = ".import generation + reimport",
            ["sdk_ecosystem"] = "discover + health verification + linting",
            ["documentation"] = "query_system_documentation (DocFX manifest + docs/)",
            ["classification"] = string.Join(',', Enum.GetNames<ParityClassification>())
        };

        return new ToolResult(true, "Capabilities enumerated.", capabilities);
    }

    [McpServerTool(Name = "health_check"), Description("Verify server health and transport status.")]
    public static ToolResult HealthCheck()
    {
        return new ToolResult(true, "ok", new Dictionary<string, string>
        {
            ["status"] = "healthy",
            ["transport"] = "stdio-mcp"
        });
    }

    [McpServerTool(Name = "get_server_info"), Description("Get information about the Godot MCP server version and working directory.")]
    public static ToolResult GetServerInfo(IPathResolver pathResolver)
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "0.0.0";

        return new ToolResult(true, "Server info.", new Dictionary<string, string>
        {
            ["name"] = "GodotMCP.Server",
            ["version"] = version,
            ["parity_mode"] = "mutatis-mutandis",
            ["cwd"] = pathResolver.ProjectRoot
        });
    }
}
