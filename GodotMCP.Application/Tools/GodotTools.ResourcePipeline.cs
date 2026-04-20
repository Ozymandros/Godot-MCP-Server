using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using ModelContextProtocol.Server;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    /// <summary>
    /// Reads and parses a Godot resource file.
    /// </summary>
    /// <param name="resourcePipelineService">Resource pipeline service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Resource file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing typed resource details.</returns>
    [McpServerTool(Name = "resource.read"), Description("Read a Godot resource file and return its type and properties.")]
    public static async Task<ToolResult> ResourceReadAsync(
        IResourcePipelineService resourcePipelineService,
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Resource file name or relative path under projectPath, ending in .tres or .res."), Required] string fileName,
        CancellationToken cancellationToken = default)
    {
        string resourcePath;
        try
        {
            resourcePath = ResolveProjectFilePath(pathResolver, projectPath, fileName);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        try
        {
            var document = await resourcePipelineService.ReadAsync(resourcePath, cancellationToken).ConfigureAwait(false);
            return new ToolResult(true, $"Read resource '{resourcePath}'.", MapResource(document));
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException)
        {
            return new ToolResult(false, ex.Message);
        }
    }

    /// <summary>
    /// Writes a Godot resource file from a type and property map.
    /// </summary>
    /// <param name="resourcePipelineService">Resource pipeline service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Resource file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="type">Godot resource type.</param>
    /// <param name="properties">Resource properties.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing operation status.</returns>
    [McpServerTool(Name = "resource.write"), Description("Write a Godot resource file from type and properties.")]
    public static async Task<ToolResult> ResourceWriteAsync(
        IResourcePipelineService resourcePipelineService,
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Resource file name or relative path under projectPath, ending in .tres or .res."), Required] string fileName,
        [Description("Godot resource type (for example: Resource, Environment, StandardMaterial3D)."), Required] string type,
        [Description("Resource property dictionary."), Required] Dictionary<string, string>? properties,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(type))
        {
            return Invalid("type is required.");
        }

        if (properties is null)
        {
            return Invalid("properties are required.");
        }

        string resourcePath;
        try
        {
            resourcePath = ResolveProjectFilePath(pathResolver, projectPath, fileName);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        try
        {
            await resourcePipelineService
                .WriteAsync(resourcePath, new ResourceDocument(type, properties), cancellationToken)
                .ConfigureAwait(false);
            return new ToolResult(true, $"Wrote resource '{resourcePath}'.");
        }
        catch (InvalidOperationException ex)
        {
            return new ToolResult(false, ex.Message);
        }
    }

    /// <summary>
    /// Updates only the provided resource properties.
    /// </summary>
    /// <param name="resourcePipelineService">Resource pipeline service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Resource file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="properties">Property updates to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing updated resource details.</returns>
    [McpServerTool(Name = "resource.update_properties"), Description("Update specific properties on a Godot resource file.")]
    public static async Task<ToolResult> ResourceUpdatePropertiesAsync(
        IResourcePipelineService resourcePipelineService,
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Resource file name or relative path under projectPath, ending in .tres or .res."), Required] string fileName,
        [Description("Property updates to apply."), Required, MinLength(1)] Dictionary<string, string>? properties,
        CancellationToken cancellationToken = default)
    {
        if (properties is null || properties.Count == 0)
        {
            return Invalid("properties must contain at least one entry.");
        }

        string resourcePath;
        try
        {
            resourcePath = ResolveProjectFilePath(pathResolver, projectPath, fileName);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        try
        {
            var mutation = await resourcePipelineService
                .UpdatePropertiesAsync(resourcePath, properties, cancellationToken)
                .ConfigureAwait(false);
            return new ToolResult(mutation.Success, mutation.Message, mutation.Properties);
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException)
        {
            return new ToolResult(false, ex.Message);
        }
    }

    /// <summary>
    /// Removes a single property from a resource file.
    /// </summary>
    /// <param name="resourcePipelineService">Resource pipeline service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Resource file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="propertyKey">Property key to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing updated resource details.</returns>
    [McpServerTool(Name = "resource.remove_property"), Description("Remove a single property from a Godot resource file.")]
    public static async Task<ToolResult> ResourceRemovePropertyAsync(
        IResourcePipelineService resourcePipelineService,
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Resource file name or relative path under projectPath, ending in .tres or .res."), Required] string fileName,
        [Description("Property key to remove."), Required] string propertyKey,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(propertyKey))
        {
            return Invalid("propertyKey is required.");
        }

        string resourcePath;
        try
        {
            resourcePath = ResolveProjectFilePath(pathResolver, projectPath, fileName);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        try
        {
            var mutation = await resourcePipelineService
                .RemovePropertyAsync(resourcePath, propertyKey, cancellationToken)
                .ConfigureAwait(false);
            return new ToolResult(mutation.Success, mutation.Message, mutation.Properties);
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException)
        {
            return new ToolResult(false, ex.Message);
        }
    }

    /// <summary>
    /// Maps a domain resource document into a transport DTO.
    /// </summary>
    /// <param name="document">Domain resource document.</param>
    /// <returns>Mapped resource DTO.</returns>
    private static ResourceDocumentDto MapResource(ResourceDocument document)
        => new(
            document.Type,
            document.Properties.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal));
}

/// <summary>
/// Resource payload returned by resource pipeline commands.
/// </summary>
/// <param name="Type">Godot resource type.</param>
/// <param name="Properties">Resource properties map.</param>
public sealed record ResourceDocumentDto(
    string Type,
    Dictionary<string, string> Properties);
