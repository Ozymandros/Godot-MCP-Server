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

    /// <summary>
    /// Ensures an <c>ext_resource</c> for a texture and assigns <paramref name="propertyKey"/> on the root resource
    /// to <c>ExtResource("id")</c> (for example <c>albedo_texture</c> on <c>StandardMaterial3D</c> or <c>shader_parameter/foo</c> on <c>ShaderMaterial</c>).
    /// </summary>
    /// <param name="resourcePath">Path to the <c>.tres</c> / <c>.res</c> material or resource file to edit.</param>
    /// <param name="texturePath">Texture file path (absolute under the project or project-relative).</param>
    /// <param name="propertyKey">Root resource property name (e.g. <c>albedo_texture</c>).</param>
    /// <param name="extResourceType"><c>ext_resource</c> type string (default <c>Texture2D</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Mutation result with updated properties when successful.</returns>
    Task<ResourcePropertyMutationResult> AssignTexturePropertyAsync(
        string resourcePath,
        string texturePath,
        string propertyKey,
        string extResourceType = "Texture2D",
        CancellationToken cancellationToken = default);
}
