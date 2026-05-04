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
    /// <param name="rawContent">Raw resource file text. If provided, written verbatim instead of serializing type and properties.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing creation status.</returns>
    [McpServerTool(Name = "create_resource"), Description("Create a new Godot resource file (.tres). Optional linkSceneFileName + linkNodePath + linkPropertyKey (with sceneSerializer) attach the new resource to a scene under projectPath/scenes/.")]
    public static async Task<ToolResult> CreateResourceAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        IResourceSerializer resourceSerializer,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Resource file name or relative path under projectPath."), Required] string fileName,
        [Description("Godot resource type (e.g., Resource, Environment)."), Required] string type,
        [Description("Dictionary of property key-values for the resource.")] Dictionary<string, string>? properties = null,
        [Description("Raw resource file text. If provided, written verbatim instead of serializing type+properties.")] string? rawContent = null,
        ISceneSerializer? sceneSerializer = null,
        [Description("Scene file name under projectPath/scenes/; set with linkNodePath, linkPropertyKey, and sceneSerializer to link after create.")] string? linkSceneFileName = null,
        [Description("Node path in the scene.")] string? linkNodePath = null,
        [Description("Node property to set to the new .tres (e.g. environment).")] string? linkPropertyKey = null,
        [Description("ext_resource type for the .tres (often matches Godot resource type).")] string link_ext_resource_type = "Resource",
        [Description("Bootstrap root type when link scene is missing.")] string link_root_type = "Node",
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
        }
        else
        {
            if (properties is null)
            {
                return Invalid("properties are required when rawContent is not provided.");
            }

            await fileService.WriteAsync(path, resourceSerializer.Serialize(type, properties), cancellationToken).ConfigureAwait(false);
        }

        var linkScene = linkSceneFileName?.Trim();
        var linkNode = linkNodePath?.Trim();
        var linkProp = linkPropertyKey?.Trim();
        var hasScene = !string.IsNullOrEmpty(linkScene);
        var hasNode = !string.IsNullOrEmpty(linkNode);
        var hasProp = !string.IsNullOrEmpty(linkProp);
        if (hasScene || hasNode || hasProp)
        {
            if (!hasScene || !hasNode || !hasProp)
            {
                return Invalid("linkSceneFileName, linkNodePath, and linkPropertyKey must all be provided together when linking.");
            }

            if (sceneSerializer is null)
            {
                return Invalid("sceneSerializer is required when linking a resource to a scene.");
            }

            var attach = await AttachExtResourceToSceneNodeAsync(
                    fileService,
                    pathResolver,
                    sceneSerializer,
                    projectPath,
                    linkScene!,
                    linkNode!,
                    fileName,
                    linkProp!,
                    link_ext_resource_type.Trim(),
                    link_root_type.Trim(),
                    cancellationToken)
                .ConfigureAwait(false);

            if (!attach.Success)
            {
                return attach;
            }

            return new ToolResult(true, $"Resource created at '{path}' and linked to '{linkNode}' ({linkProp}).", attach.Data);
        }

        return new ToolResult(true, $"Resource created at '{path}'.");
    }
}
