using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;

namespace GodotMCP.Infrastructure.Services;

/// <summary>
/// Implements high-level resource file read/write and property mutation operations.
/// </summary>
/// <param name="fileService">Filesystem abstraction for project files.</param>
/// <param name="pathResolver">Project path resolver used for path validation.</param>
/// <param name="resourceSerializer">Serializer for resource text payloads.</param>
public sealed class ResourcePipelineService(
    IGodotFileService fileService,
    IPathResolver pathResolver,
    IResourceSerializer resourceSerializer) : IResourcePipelineService
{
    /// <inheritdoc />
    public async Task<ResourceDocument> ReadAsync(string resourcePath, CancellationToken cancellationToken = default)
    {
        EnsureValidResourcePath(resourcePath);
        if (!fileService.Exists(resourcePath))
        {
            throw new FileNotFoundException($"Resource file not found: {resourcePath}");
        }

        var content = await fileService.ReadAsync(resourcePath, cancellationToken).ConfigureAwait(false);
        return resourceSerializer.DeserializeDocument(content);
    }

    /// <inheritdoc />
    public async Task WriteAsync(string resourcePath, ResourceDocument document, CancellationToken cancellationToken = default)
    {
        EnsureValidResourcePath(resourcePath);
        if (string.IsNullOrWhiteSpace(document.Type))
        {
            throw new InvalidOperationException("Resource type is required.");
        }

        var serialized = resourceSerializer.Serialize(document);
        await fileService.WriteAsync(resourcePath, serialized, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ResourcePropertyMutationResult> UpdatePropertiesAsync(string resourcePath, Dictionary<string, string> properties, CancellationToken cancellationToken = default)
    {
        if (properties.Count == 0)
        {
            return new ResourcePropertyMutationResult(false, "properties must contain at least one entry.");
        }

        var document = await ReadAsync(resourcePath, cancellationToken).ConfigureAwait(false);
        foreach (var (key, value) in properties)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return new ResourcePropertyMutationResult(false, "Property keys must be non-empty strings.");
            }

            document.Properties[key] = value;
        }

        await WriteAsync(resourcePath, document, cancellationToken).ConfigureAwait(false);
        return new ResourcePropertyMutationResult(true, $"Updated {properties.Count} propertie(s).", Clone(document.Properties));
    }

    /// <inheritdoc />
    public async Task<ResourcePropertyMutationResult> RemovePropertyAsync(string resourcePath, string propertyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(propertyKey))
        {
            return new ResourcePropertyMutationResult(false, "propertyKey is required.");
        }

        var document = await ReadAsync(resourcePath, cancellationToken).ConfigureAwait(false);
        if (!document.Properties.Remove(propertyKey))
        {
            return new ResourcePropertyMutationResult(false, $"Property '{propertyKey}' does not exist.", Clone(document.Properties));
        }

        await WriteAsync(resourcePath, document, cancellationToken).ConfigureAwait(false);
        return new ResourcePropertyMutationResult(true, $"Removed property '{propertyKey}'.", Clone(document.Properties));
    }

    /// <summary>
    /// Validates that a resource path is project-scoped and has a supported resource extension.
    /// </summary>
    /// <param name="resourcePath">Resource path to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown when path is empty, outside project, or has unsupported extension.</exception>
    private void EnsureValidResourcePath(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
        {
            throw new InvalidOperationException("resourcePath is required.");
        }

        var normalized = resourcePath.Replace('\\', '/');
        var isResourceFile = normalized.EndsWith(".tres", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".res", StringComparison.OrdinalIgnoreCase);
        if (!isResourceFile)
        {
            throw new InvalidOperationException("resourcePath must end with .tres or .res.");
        }

        _ = pathResolver.ResolvePath(resourcePath);
    }

    /// <summary>
    /// Clones a resource property dictionary using ordinal key comparison.
    /// </summary>
    /// <param name="source">Source property dictionary.</param>
    /// <returns>Cloned dictionary instance.</returns>
    private static Dictionary<string, string> Clone(Dictionary<string, string> source)
        => source.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
}
