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
        IPathResolver pathResolver,
        IResourceSerializer resourceSerializer,
        [Description("Project root path (res:// or absolute path under the project)."), Required] string projectPath,
        [Description("Resource file name or relative path under projectPath."), Required] string fileName,
        [Description("Godot resource type (e.g., Resource, Environment)."), Required] string type,
        [Description("Dictionary of property key-values for the resource."), Required, MinLength(1)] Dictionary<string, string> properties,
        CancellationToken cancellationToken = default)
    {
        string path;
        try
        {
            path = ResolveProjectFilePath(pathResolver, projectPath, fileName);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        await fileService.WriteAsync(path, resourceSerializer.Serialize(type, properties), cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, $"Resource created at '{path}'.");
    }
}
