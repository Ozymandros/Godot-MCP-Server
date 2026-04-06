using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using ModelContextProtocol.Server;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    /// <summary>
    /// Lists the full scene graph tree for a single scene.
    /// </summary>
    /// <param name="sceneGraphService">Scene graph service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="scenePath">Scene path to inspect.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing the recursive node tree payload.</returns>
    [McpServerTool(Name = "scene.list_nodes"), Description("List the full node tree for a scene, including hierarchy metadata and basic properties.")]
    public static async Task<ToolResult> SceneListNodesAsync(
        ISceneGraphService sceneGraphService,
        IPathResolver pathResolver,
        [Description("Scene path (res://...) to inspect."), Required] string scenePath,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidResPath(pathResolver, scenePath))
        {
            return Invalid("scenePath must be a valid project-relative path.");
        }

        var nodes = await sceneGraphService.ListNodesAsync(scenePath, cancellationToken).ConfigureAwait(false);
        var dto = nodes.Select(MapNode).ToList();
        return new ToolResult(true, $"Listed {CountNodes(nodes)} node(s).", dto);
    }

    /// <summary>
    /// Creates a node under a target parent path in a scene.
    /// </summary>
    /// <param name="sceneGraphService">Scene graph service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="scenePath">Scene path to mutate.</param>
    /// <param name="parentNodePath">Parent node path where the new node will be inserted.</param>
    /// <param name="nodeType">Godot node type for the new node.</param>
    /// <param name="nodeName">Name for the new node.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing operation status and optional node snapshot.</returns>
    [McpServerTool(Name = "scene.add_node"), Description("Create and insert a node under a parent node path in a scene, then save the scene.")]
    public static async Task<ToolResult> SceneAddNodeAsync(
        ISceneGraphService sceneGraphService,
        IPathResolver pathResolver,
        [Description("Scene path (res://...) to modify."), Required] string scenePath,
        [Description("Parent node path (for example: ., Player, Player/CameraRig)."), Required] string parentNodePath,
        [Description("Godot node type to create (for example: Node3D, Sprite2D, Control)."), Required] string nodeType,
        [Description("Name for the new node."), Required] string nodeName,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidResPath(pathResolver, scenePath) || IsBlank(parentNodePath) || IsBlank(nodeType) || IsBlank(nodeName))
        {
            return Invalid("scenePath, parentNodePath, nodeType, and nodeName are required.");
        }

        var result = await sceneGraphService
            .AddNodeAsync(new SceneGraphAddNodeRequest(scenePath, parentNodePath, nodeType, nodeName), cancellationToken)
            .ConfigureAwait(false);

        return ToToolResult(result);
    }

    /// <summary>
    /// Removes a node and its descendants from a scene.
    /// </summary>
    /// <param name="sceneGraphService">Scene graph service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="scenePath">Scene path to mutate.</param>
    /// <param name="nodePath">Node path to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing operation status.</returns>
    [McpServerTool(Name = "scene.remove_node"), Description("Remove a node and its descendants from a scene, then save the scene.")]
    public static async Task<ToolResult> SceneRemoveNodeAsync(
        ISceneGraphService sceneGraphService,
        IPathResolver pathResolver,
        [Description("Scene path (res://...) to modify."), Required] string scenePath,
        [Description("Node path to remove."), Required] string nodePath,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidResPath(pathResolver, scenePath) || IsBlank(nodePath))
        {
            return Invalid("scenePath and nodePath are required.");
        }

        var result = await sceneGraphService
            .RemoveNodeAsync(new SceneGraphRemoveNodeRequest(scenePath, nodePath), cancellationToken)
            .ConfigureAwait(false);

        return ToToolResult(result);
    }

    /// <summary>
    /// Moves an existing node to a new parent node path.
    /// </summary>
    /// <param name="sceneGraphService">Scene graph service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="scenePath">Scene path to mutate.</param>
    /// <param name="nodePath">Node path to move.</param>
    /// <param name="newParentPath">Destination parent node path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing operation status and optional moved node snapshot.</returns>
    [McpServerTool(Name = "scene.move_node"), Description("Reparent a node to a new parent node path in a scene, then save the scene.")]
    public static async Task<ToolResult> SceneMoveNodeAsync(
        ISceneGraphService sceneGraphService,
        IPathResolver pathResolver,
        [Description("Scene path (res://...) to modify."), Required] string scenePath,
        [Description("Node path to move."), Required] string nodePath,
        [Description("Destination parent node path (for example: ., Player, Player/CameraRig)."), Required] string newParentPath,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidResPath(pathResolver, scenePath) || IsBlank(nodePath) || IsBlank(newParentPath))
        {
            return Invalid("scenePath, nodePath, and newParentPath are required.");
        }

        var result = await sceneGraphService
            .MoveNodeAsync(new SceneGraphMoveNodeRequest(scenePath, nodePath, newParentPath), cancellationToken)
            .ConfigureAwait(false);

        return ToToolResult(result);
    }

    /// <summary>
    /// Renames a node in a scene graph.
    /// </summary>
    /// <param name="sceneGraphService">Scene graph service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="scenePath">Scene path to mutate.</param>
    /// <param name="nodePath">Node path to rename.</param>
    /// <param name="newName">New node name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing operation status and optional renamed node snapshot.</returns>
    [McpServerTool(Name = "scene.rename_node"), Description("Rename an existing node in a scene, then save the scene.")]
    public static async Task<ToolResult> SceneRenameNodeAsync(
        ISceneGraphService sceneGraphService,
        IPathResolver pathResolver,
        [Description("Scene path (res://...) to modify."), Required] string scenePath,
        [Description("Node path to rename."), Required] string nodePath,
        [Description("New node name."), Required] string newName,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidResPath(pathResolver, scenePath) || IsBlank(nodePath) || IsBlank(newName))
        {
            return Invalid("scenePath, nodePath, and newName are required.");
        }

        var result = await sceneGraphService
            .RenameNodeAsync(new SceneGraphRenameNodeRequest(scenePath, nodePath, newName), cancellationToken)
            .ConfigureAwait(false);

        return ToToolResult(result);
    }

    /// <summary>
    /// Retrieves the serialized property dictionary for a single node.
    /// </summary>
    /// <param name="sceneGraphService">Scene graph service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="scenePath">Scene path to inspect.</param>
    /// <param name="nodePath">Node path to inspect.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing node properties when the node exists.</returns>
    [McpServerTool(Name = "scene.get_node_properties"), Description("Get the properties dictionary for a specific node in a scene.")]
    public static async Task<ToolResult> SceneGetNodePropertiesAsync(
        ISceneGraphService sceneGraphService,
        IPathResolver pathResolver,
        [Description("Scene path (res://...) to inspect."), Required] string scenePath,
        [Description("Node path to inspect."), Required] string nodePath,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidResPath(pathResolver, scenePath) || IsBlank(nodePath))
        {
            return Invalid("scenePath and nodePath are required.");
        }

        try
        {
            var properties = await sceneGraphService.GetNodePropertiesAsync(scenePath, nodePath, cancellationToken).ConfigureAwait(false);
            var dto = properties.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
            return new ToolResult(true, $"Returned {dto.Count} propertie(s) for '{nodePath}'.", dto);
        }
        catch (InvalidOperationException ex)
        {
            return new ToolResult(false, ex.Message);
        }
    }

    /// <summary>
    /// Updates only the provided node properties in a scene.
    /// </summary>
    /// <param name="sceneGraphService">Scene graph service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="scenePath">Scene path to mutate.</param>
    /// <param name="nodePath">Node path to update.</param>
    /// <param name="properties">Property map with primitive JSON values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing operation status and optional updated node snapshot.</returns>
    [McpServerTool(Name = "scene.set_node_properties"), Description("Update only the provided properties on a node and save the scene.")]
    public static async Task<ToolResult> SceneSetNodePropertiesAsync(
        ISceneGraphService sceneGraphService,
        IPathResolver pathResolver,
        [Description("Scene path (res://...) to modify."), Required] string scenePath,
        [Description("Node path to update."), Required] string nodePath,
        [Description("Property map to update. Values must be primitive JSON values."), Required]
        Dictionary<string, JsonElement>? properties,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidResPath(pathResolver, scenePath) || IsBlank(nodePath))
        {
            return Invalid("scenePath and nodePath are required.");
        }

        if (properties is null || properties.Count == 0)
        {
            return Invalid("properties must contain at least one entry.");
        }

        var normalizedProperties = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in properties)
        {
            if (IsBlank(key))
            {
                return Invalid("Property keys must be non-empty strings.");
            }

            var normalizedValue = ToScenePrimitiveValue(value);
            if (normalizedValue is null)
            {
                return Invalid($"Property '{key}' must be a primitive JSON value (string, number, or boolean).");
            }

            normalizedProperties[key] = normalizedValue;
        }

        var result = await sceneGraphService
            .SetNodePropertiesAsync(new SceneGraphSetPropertiesRequest(scenePath, nodePath, normalizedProperties), cancellationToken)
            .ConfigureAwait(false);

        return ToToolResult(result);
    }

    /// <summary>
    /// Converts a mutation result from the domain layer into an MCP <see cref="ToolResult"/>.
    /// </summary>
    /// <param name="result">Mutation result from the scene graph service.</param>
    /// <returns>Transport-friendly tool result payload.</returns>
    private static ToolResult ToToolResult(SceneGraphMutationResult result)
    {
        var dto = result.Node is null ? null : MapNode(result.Node);
        return new ToolResult(result.Success, result.Message, dto);
    }

    /// <summary>
    /// Maps a domain node model to the transport DTO shape returned by MCP tools.
    /// </summary>
    /// <param name="node">Domain scene graph node.</param>
    /// <returns>Mapped transport DTO.</returns>
    private static SceneGraphNodeDto MapNode(SceneGraphNodeInfo node)
        => new(
            node.Name,
            node.Type,
            node.NodePath,
            node.Parent,
            node.Children.Select(MapNode).ToList(),
            node.Script,
            node.Properties.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal));

    /// <summary>
    /// Counts total nodes in a forest payload.
    /// </summary>
    /// <param name="nodes">Root nodes to count.</param>
    /// <returns>Total recursive node count.</returns>
    private static int CountNodes(IReadOnlyList<SceneGraphNodeInfo> nodes)
        => nodes.Sum(CountNodes);

    /// <summary>
    /// Counts total nodes in a single recursive subtree.
    /// </summary>
    /// <param name="node">Root node of the subtree.</param>
    /// <returns>Total recursive node count.</returns>
    private static int CountNodes(SceneGraphNodeInfo node)
        => 1 + node.Children.Sum(CountNodes);

    /// <summary>
    /// Converts a JSON element into a primitive CLR value used by property update flows.
    /// </summary>
    /// <param name="value">JSON value to convert.</param>
    /// <returns>Primitive value when supported; otherwise <see langword="null"/>.</returns>
    private static object? ToScenePrimitiveValue(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number when value.TryGetInt64(out var i) => i,
            JsonValueKind.Number when value.TryGetDouble(out var d) => d,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
}

/// <summary>
/// Scene graph transport model returned by <c>scene.list_nodes</c> and mutation commands.
/// </summary>
/// <param name="Name">Node name.</param>
/// <param name="Type">Godot node type.</param>
/// <param name="NodePath">Resolved node path in the scene graph.</param>
/// <param name="Parent">Resolved parent node path.</param>
/// <param name="Children">Recursive children.</param>
/// <param name="Script">Script reference when present.</param>
/// <param name="Properties">Basic property dictionary.</param>
public sealed record SceneGraphNodeDto(
    string Name,
    string Type,
    string NodePath,
    string Parent,
    IReadOnlyList<SceneGraphNodeDto> Children,
    string? Script,
    IReadOnlyDictionary<string, string> Properties);
