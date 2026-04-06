using GodotMCP.Core.Models;

namespace GodotMCP.Core.Interfaces;

/// <summary>
/// Provides high-level resource file operations for read/write and property mutation flows.
/// </summary>
public interface IResourcePipelineService
{
    /// <summary>
    /// Reads and parses a Godot resource file.
    /// </summary>
    /// <param name="resourcePath">Resource path to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parsed resource document.</returns>
    Task<ResourceDocument> ReadAsync(string resourcePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a Godot resource file from a typed document model.
    /// </summary>
    /// <param name="resourcePath">Resource path to write.</param>
    /// <param name="document">Resource document payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when write operation finishes.</returns>
    Task WriteAsync(string resourcePath, ResourceDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates or adds the provided resource properties.
    /// </summary>
    /// <param name="resourcePath">Resource path to update.</param>
    /// <param name="properties">Property updates to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Mutation result with updated properties when successful.</returns>
    Task<ResourcePropertyMutationResult> UpdatePropertiesAsync(string resourcePath, Dictionary<string, string> properties, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a single resource property.
    /// </summary>
    /// <param name="resourcePath">Resource path to update.</param>
    /// <param name="propertyKey">Property key to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Mutation result with updated properties when successful.</returns>
    Task<ResourcePropertyMutationResult> RemovePropertyAsync(string resourcePath, string propertyKey, CancellationToken cancellationToken = default);
}
