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
    /// Lists Godot resource files (.tres, .res) in the project directory, with optional directory and type filters.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="resourceSerializer">Resource serializer for Godot resources.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="directory">Optional subdirectory to search under projectPath.</param>
    /// <param name="resourceType">Optional Godot resource type filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of resource info objects.</returns>
    [McpServerTool(Name = "list_resources"), Description("List Godot resource files (.tres, .res) in the project directory, with optional filters.")]
    public static async Task<IReadOnlyList<ResourceDocument>> ResourceListAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        IResourceSerializer resourceSerializer,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Optional subdirectory to search under projectPath.")] string? directory = null,
        [Description("Optional Godot resource type filter.")] string? resourceType = null,
        CancellationToken cancellationToken = default)
    {
        var baseDir = string.IsNullOrWhiteSpace(directory)
            ? NormalizeProjectPath(pathResolver, projectPath)
            : Path.Combine(NormalizeProjectPath(pathResolver, projectPath), directory);

        if (!Directory.Exists(baseDir))
            return Array.Empty<ResourceDocument>();

        var files = Directory.EnumerateFiles(baseDir, "*.tres", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(baseDir, "*.res", SearchOption.AllDirectories));

        var results = new List<ResourceDocument>();
        foreach (var file in files)
        {
            string content;
            try { content = await fileService.ReadAsync(file, cancellationToken).ConfigureAwait(false); }
            catch { continue; }
            ResourceDocument doc;
            try { doc = resourceSerializer.DeserializeDocument(content); }
            catch { continue; }
            if (resourceType != null && !string.Equals(doc.Type, resourceType, StringComparison.OrdinalIgnoreCase))
                continue;
            results.Add(doc);
        }
        return results;
    }

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
        [Description("Dictionary of property key-values for the resource.")] Dictionary<string, string>? properties = null,
        [Description("Raw resource file text. If provided, written verbatim instead of serializing type+properties.")] string? rawContent = null,
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

        if (!string.IsNullOrWhiteSpace(rawContent))
        {
            await fileService.WriteAsync(path, rawContent, cancellationToken).ConfigureAwait(false);
            return new ToolResult(true, $"Resource created at '{path}'.");
        }

        if (properties is null)
        {
            return Invalid("properties are required when rawContent is not provided.");
        }

        await fileService.WriteAsync(path, resourceSerializer.Serialize(type, properties), cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, $"Resource created at '{path}'.");
    }
}
