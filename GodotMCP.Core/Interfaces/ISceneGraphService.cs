using GodotMCP.Core.Models;

namespace GodotMCP.Core.Interfaces;

/// <summary>
/// Provides headless scene graph operations for listing, mutating, and inspecting nodes.
/// </summary>
public interface ISceneGraphService
{
    /// <summary>
    /// Lists the full scene graph hierarchy for a scene.
    /// </summary>
    /// <param name="scenePath">Scene path to inspect.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>Root-level nodes with full recursive children.</returns>
    Task<IReadOnlyList<SceneGraphNodeInfo>> ListNodesAsync(string scenePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a node under a parent path in the scene graph.
    /// </summary>
    /// <param name="request">Node creation request details.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>The mutation result including status and optional node snapshot.</returns>
    Task<SceneGraphMutationResult> AddNodeAsync(SceneGraphAddNodeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a packed scene instance node under a validated parent and registers an <c>ext_resource</c> for the packed scene.
    /// </summary>
    /// <param name="request">Instantiation request details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Mutation result including status and optional node snapshot.</returns>
    Task<SceneGraphMutationResult> InstantiatePackedSceneAsync(SceneGraphInstantiatePackedSceneRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a node and its descendants from the scene graph.
    /// </summary>
    /// <param name="request">Node removal request details.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>The mutation result including status information.</returns>
    Task<SceneGraphMutationResult> RemoveNodeAsync(SceneGraphRemoveNodeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a node under a different parent path.
    /// </summary>
    /// <param name="request">Node move request details.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>The mutation result including status and optional updated node snapshot.</returns>
    Task<SceneGraphMutationResult> MoveNodeAsync(SceneGraphMoveNodeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a node in the scene graph.
    /// </summary>
    /// <param name="request">Node rename request details.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>The mutation result including status and optional updated node snapshot.</returns>
    Task<SceneGraphMutationResult> RenameNodeAsync(SceneGraphRenameNodeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the properties dictionary for a single node.
    /// </summary>
    /// <param name="scenePath">Scene path containing the node.</param>
    /// <param name="nodePath">Node path to inspect.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>A property dictionary for the target node.</returns>
    Task<IReadOnlyDictionary<string, string>> GetNodePropertiesAsync(string scenePath, string nodePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates only the provided node properties and persists the scene.
    /// </summary>
    /// <param name="request">Property update request details.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>The mutation result including status and optional updated node snapshot.</returns>
    Task<SceneGraphMutationResult> SetNodePropertiesAsync(SceneGraphSetPropertiesRequest request, CancellationToken cancellationToken = default);
}
