using GodotMCP.Core.Models;

namespace GodotMCP.Core.Interfaces;

/// <summary>
/// Provides headless scene-physics operations for listing, creating, updating, and validating bodies.
/// </summary>
public interface IPhysicsService
{
    /// <summary>
    /// Lists physics bodies from scenes under the provided root path.
    /// </summary>
    /// <param name="rootPath">Project-relative or absolute path inside the current project.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>Discovered body nodes.</returns>
    Task<IReadOnlyList<PhysicsBodyInfo>> ListAsync(string rootPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a physics body node in a target scene.
    /// </summary>
    /// <param name="request">Body creation request details.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>Mutation result including status and optional body snapshot.</returns>
    Task<PhysicsMutationResult> CreateBodyAsync(PhysicsCreateBodyRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates selected properties on an existing physics body.
    /// </summary>
    /// <param name="request">Body update request details.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>Mutation result including status and optional body snapshot.</returns>
    Task<PhysicsMutationResult> UpdateBodyAsync(PhysicsUpdateBodyRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates physics body setup across scenes under the provided root path.
    /// </summary>
    /// <param name="rootPath">Project-relative or absolute path inside the current project.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>Validation issues.</returns>
    Task<IReadOnlyList<PhysicsValidationIssue>> ValidateAsync(string rootPath, CancellationToken cancellationToken = default);

    Task<PhysicsShapeMutationResult> AddShapeAsync(PhysicsAddShapeRequest request, CancellationToken cancellationToken = default);
    Task<PhysicsShapeMutationResult> UpdateShapeAsync(PhysicsUpdateShapeRequest request, CancellationToken cancellationToken = default);
    Task<PhysicsShapeMutationResult> RemoveShapeAsync(PhysicsRemoveShapeRequest request, CancellationToken cancellationToken = default);
    Task<PhysicsShapeMutationResult> AddCollisionPolygonAsync(PhysicsAddCollisionPolygonRequest request, CancellationToken cancellationToken = default);
    Task<PhysicsShapeMutationResult> UpdateCollisionPolygonAsync(PhysicsUpdateCollisionPolygonRequest request, CancellationToken cancellationToken = default);
    Task<PhysicsShapeMutationResult> RemoveCollisionPolygonAsync(PhysicsRemoveCollisionPolygonRequest request, CancellationToken cancellationToken = default);
    Task<PhysicsShapeMutationResult> AssignShapeResourceAsync(PhysicsAssignShapeResourceRequest request, CancellationToken cancellationToken = default);
    Task<PhysicsShapeMutationResult> SetShapeFlagsAsync(PhysicsSetShapeFlagsRequest request, CancellationToken cancellationToken = default);
    Task<PhysicsMutationResult> SetAreaMonitoringAsync(PhysicsAreaSetMonitoringRequest request, CancellationToken cancellationToken = default);
    Task<PhysicsMutationResult> SetAreaPriorityAsync(PhysicsAreaSetPriorityRequest request, CancellationToken cancellationToken = default);
    Task<PhysicsMutationResult> SetAreaSpaceOverrideAsync(PhysicsAreaSetSpaceOverrideRequest request, CancellationToken cancellationToken = default);
    Task<PhysicsMutationResult> SetAreaCollisionFiltersAsync(PhysicsAreaSetCollisionFiltersRequest request, CancellationToken cancellationToken = default);
}
