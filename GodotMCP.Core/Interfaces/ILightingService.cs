using GodotMCP.Core.Models;

namespace GodotMCP.Core.Interfaces;

/// <summary>
/// Provides headless scene-light operations for listing, creating, updating, and validating lights.
/// </summary>
public interface ILightingService
{
    /// <summary>
    /// Lists light nodes from scenes under the provided root path.
    /// </summary>
    /// <param name="rootPath">Project-relative or absolute path inside the current project.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>Discovered light nodes.</returns>
    Task<IReadOnlyList<LightNodeInfo>> ListAsync(string rootPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a light node in a target scene.
    /// </summary>
    /// <param name="request">Light creation request details.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>Mutation result including status and optional light snapshot.</returns>
    Task<LightMutationResult> CreateAsync(LightCreateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates selected properties on an existing light node.
    /// </summary>
    /// <param name="request">Light update request details.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>Mutation result including status and optional light snapshot.</returns>
    Task<LightMutationResult> UpdateAsync(LightUpdateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates light nodes from scenes under the provided root path and reports lint-style issues.
    /// </summary>
    /// <param name="rootPath">Project-relative or absolute path inside the current project.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>Validation issues.</returns>
    Task<IReadOnlyList<LightValidationIssue>> ValidateAsync(string rootPath, CancellationToken cancellationToken = default);
}
