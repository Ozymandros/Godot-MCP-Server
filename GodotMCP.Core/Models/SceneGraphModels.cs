namespace GodotMCP.Core.Models;

/// <summary>
/// Represents a scene graph node with hierarchy metadata and basic properties.
/// </summary>
/// <param name="Name">Node name.</param>
/// <param name="Type">Godot node type.</param>
/// <param name="NodePath">Resolved node path (for example: <c>.</c>, <c>Player</c>, <c>Player/Camera</c>).</param>
/// <param name="Parent">Resolved parent node path, or <c>.</c> for root-level nodes.</param>
/// <param name="Children">Child nodes in deterministic order.</param>
/// <param name="Script">Script reference when present.</param>
/// <param name="Properties">Basic node properties.</param>
public sealed record SceneGraphNodeInfo(
    string Name,
    string Type,
    string NodePath,
    string Parent,
    IReadOnlyList<SceneGraphNodeInfo> Children,
    string? Script,
    IReadOnlyDictionary<string, string> Properties);

/// <summary>
/// Input contract for adding a node to a scene graph.
/// </summary>
/// <param name="ScenePath">Scene path containing the graph.</param>
/// <param name="ParentNodePath">Parent node path where the new node will be inserted.</param>
/// <param name="NodeType">Godot node type to create.</param>
/// <param name="NodeName">Name of the node to create.</param>
public sealed record SceneGraphAddNodeRequest(
    string ScenePath,
    string ParentNodePath,
    string NodeType,
    string NodeName);

/// <summary>
/// Input contract for instantiating a packed scene as a child node.
/// </summary>
/// <param name="ScenePath">Target scene path to modify.</param>
/// <param name="ParentNodePath">Parent node path in the target scene.</param>
/// <param name="PackedSceneAbsolutePath">Absolute filesystem path to the packed <c>.tscn</c> inside the project.</param>
/// <param name="InstanceName">Name for the new instance node.</param>
public sealed record SceneGraphInstantiatePackedSceneRequest(
    string ScenePath,
    string ParentNodePath,
    string PackedSceneAbsolutePath,
    string InstanceName);

/// <summary>
/// Input contract for removing a node subtree from a scene graph.
/// </summary>
/// <param name="ScenePath">Scene path containing the graph.</param>
/// <param name="NodePath">Node path to remove.</param>
public sealed record SceneGraphRemoveNodeRequest(
    string ScenePath,
    string NodePath);

/// <summary>
/// Input contract for moving a node to a new parent in a scene graph.
/// </summary>
/// <param name="ScenePath">Scene path containing the graph.</param>
/// <param name="NodePath">Node path to move.</param>
/// <param name="NewParentPath">Target parent path.</param>
public sealed record SceneGraphMoveNodeRequest(
    string ScenePath,
    string NodePath,
    string NewParentPath);

/// <summary>
/// Input contract for renaming a node in a scene graph.
/// </summary>
/// <param name="ScenePath">Scene path containing the graph.</param>
/// <param name="NodePath">Node path to rename.</param>
/// <param name="NewName">New node name.</param>
public sealed record SceneGraphRenameNodeRequest(
    string ScenePath,
    string NodePath,
    string NewName);

/// <summary>
/// Input contract for setting selected node properties.
/// </summary>
/// <param name="ScenePath">Scene path containing the graph.</param>
/// <param name="NodePath">Node path to update.</param>
/// <param name="Properties">Property map with only values to update.</param>
public sealed record SceneGraphSetPropertiesRequest(
    string ScenePath,
    string NodePath,
    IReadOnlyDictionary<string, object?> Properties);

/// <summary>
/// Connection descriptor for scene [connection] rows.
/// </summary>
public sealed record SceneConnectionInfo(
    string Signal,
    string From,
    string To,
    string Method,
    string? Flags,
    string? Binds,
    string? Unbinds,
    IReadOnlyDictionary<string, string> Attributes,
    string CanonicalKey);

public sealed record SceneConnectionAddRequest(
    string ScenePath,
    string Signal,
    string From,
    string To,
    string Method,
    string? Flags = null,
    string? Binds = null,
    string? Unbinds = null,
    bool Idempotent = true);

public sealed record SceneConnectionRemoveRequest(
    string ScenePath,
    string Signal,
    string From,
    string To,
    string Method,
    string? Flags = null,
    string? Binds = null,
    string? Unbinds = null);

public sealed record SceneConnectionUpdateRequest(
    string ScenePath,
    SceneConnectionRemoveRequest Match,
    SceneConnectionAddRequest NewValue);

/// <summary>
/// Represents the result of a scene graph mutation command.
/// </summary>
/// <param name="Success">Whether the operation succeeded.</param>
/// <param name="Message">Human-readable operation result message.</param>
/// <param name="Node">Optional node snapshot after the operation.</param>
public sealed record SceneGraphMutationResult(
    bool Success,
    string Message,
    SceneGraphNodeInfo? Node = null);
