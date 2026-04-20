using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace GodotMCP.Application.Tools;

/// <summary>
/// Provides methods to create Godot resource files.
/// </summary>
public static partial class GodotTools
{
    /// <summary>
    /// Creates a new Godot resource file (.tres).
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="resourceSerializer">Resource serializer for Godot resources.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Resource file name or relative path under projectPath.</param>
    /// <param name="type">Godot resource type (e.g., Resource, Environment).</param>
    /// <param name="properties">Dictionary of property key-values for the resource.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing creation status.</returns>
    [McpServerTool(Name = "create_resource"), Description("Create a new Godot resource file (.tres).")]
    public static async Task<ToolResult> CreateResourceAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        IResourceSerializer resourceSerializer,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
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
