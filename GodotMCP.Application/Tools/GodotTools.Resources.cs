using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    [McpServerTool(Name = "create_resource"), Description("Create a new Godot resource file (.tres).")]
    public static async Task<ToolResult> CreateResourceAsync(
        IGodotFileService fileService,
        IResourceSerializer resourceSerializer,
        [Description("Project path (res://...) for the new resource."), Required] string path, 
        [Description("Godot resource type (e.g., Resource, Environment)."), Required] string type, 
        [Description("Dictionary of property key-values for the resource."), Required, MinLength(1)] Dictionary<string, string> properties, 
        CancellationToken cancellationToken = default)
    {
        await fileService.WriteAsync(path, resourceSerializer.Serialize(type, properties), cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, $"Resource created at '{path}'.");
    }
}
