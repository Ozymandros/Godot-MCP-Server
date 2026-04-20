using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using ModelContextProtocol.Server;

namespace GodotMCP.Application.Tools;

/// <summary>
/// Provides static methods for Godot MCP server tools and automation features.
/// </summary>
[McpServerToolType]
public static partial class GodotTools
{
    /// <summary>
    /// Enumerates Godot MCP server capabilities and supported features.
    /// </summary>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <returns>Tool result containing the server capabilities.</returns>
    [McpServerTool(Name = "get_server_capabilities"), Description("Enumerate Godot MCP server capabilities and supported features.")]
    public static ToolResult GetServerCapabilities(
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath)
    {
        if (IsBlank(projectPath))
        {
            return Invalid("projectPath is required.");
        }

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
            ["documentation"] = "query_system_documentation (local DocFX _site + docs/); query_godot_engine_documentation (docs.godotengine.org API, requires network)",
            ["classification"] = string.Join(',', Enum.GetNames<ParityClassification>())
        };

        return new ToolResult(true, "Capabilities enumerated.", capabilities);
    }

    /// <summary>
    /// Verifies server health and transport status.
    /// </summary>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <returns>Tool result indicating server health.</returns>
    [McpServerTool(Name = "health_check"), Description("Verify server health and transport status.")]
    public static ToolResult HealthCheck(
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath)
    {
        if (IsBlank(projectPath))
        {
            return Invalid("projectPath is required.");
        }

        return new ToolResult(true, "ok", new Dictionary<string, string>
        {
            ["status"] = "healthy",
            ["transport"] = "stdio-mcp"
        });
    }

    /// <summary>
    /// Gets information about the Godot MCP server version and working directory.
    /// </summary>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <returns>Tool result containing server info.</returns>
    [McpServerTool(Name = "get_server_info"), Description("Get information about the Godot MCP server version and working directory.")]
    public static ToolResult GetServerInfo(
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath)
    {
        if (IsBlank(projectPath))
        {
            return Invalid("projectPath is required.");
        }

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
