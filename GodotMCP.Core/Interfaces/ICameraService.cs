using GodotMCP.Core.Models;

namespace GodotMCP.Core.Interfaces;

/// <summary>
/// Provides headless scene-camera operations for listing, creating, updating, and validating cameras.
/// </summary>
public interface ICameraService
{
    /// <summary>
    /// Lists all camera nodes from scenes under the provided root path.
    /// </summary>
    /// <param name="rootPath">Project-relative or absolute path inside the current project.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>A read-only collection of discovered camera nodes.</returns>
    Task<IReadOnlyList<CameraNodeInfo>> ListAsync(string rootPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a camera node in a target scene.
    /// </summary>
    /// <param name="request">Camera creation request details.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>The mutation result including status and camera snapshot when successful.</returns>
    Task<CameraMutationResult> CreateAsync(CameraCreateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates selected properties on an existing camera node.
    /// </summary>
    /// <param name="request">Camera update request details.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>The mutation result including status and camera snapshot when successful.</returns>
    Task<CameraMutationResult> UpdateAsync(CameraUpdateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates camera nodes from scenes under the provided root path and reports lint-style issues.
    /// </summary>
    /// <param name="rootPath">Project-relative or absolute path inside the current project.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>A read-only collection of validation issues.</returns>
    Task<IReadOnlyList<CameraValidationIssue>> ValidateAsync(string rootPath, CancellationToken cancellationToken = default);
}