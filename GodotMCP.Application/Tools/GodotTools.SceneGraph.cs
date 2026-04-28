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
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Scene file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="root_type">Root node type used if scene bootstrap creation is required.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing the recursive node tree payload.</returns>
    [McpServerTool(Name = "scene.list_nodes"), Description("List the full node tree for a scene, including hierarchy metadata and basic properties.")]
    public static async Task<ToolResult> SceneListNodesAsync(
        ISceneGraphService sceneGraphService,
        IGodotFileService fileService,
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Scene file name or relative path under projectPath."), Required] string fileName,
        [Description("Root node type used when bootstrap creation is needed (for example: Node, Node2D, Node3D).")] string root_type = "Node",
        CancellationToken cancellationToken = default)
    {
        string scenePath;
        try
        {
            scenePath = await EnsureSceneReadyAsync(fileService, pathResolver, projectPath, fileName, root_type, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message, "Use projectPath + /scenes/ + fileName (with .tscn extension).");
        }

        var nodes = await sceneGraphService.ListNodesAsync(scenePath, cancellationToken).ConfigureAwait(false);
        var dto = nodes.Select(MapNode).ToList();
        return new ToolResult(true, $"Listed {CountNodes(nodes)} node(s).", dto);
    }

    /// <summary>
    /// Creates a node under a target parent path in a scene.
    /// </summary>
    /// <param name="sceneGraphService">Scene graph service abstraction.</param>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Scene file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="parentNodePath">Parent node path where the new node will be inserted.</param>
    /// <param name="nodeType">Godot node type for the new node.</param>
    /// <param name="nodeName">Name for the new node.</param>
    /// <param name="root_type">Root node type used if scene bootstrap creation is required.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing operation status and optional node snapshot.</returns>
    [McpServerTool(Name = "scene.add_node"), Description("Create and insert a node under a parent node path in a scene, then save the scene.")]
    public static async Task<ToolResult> SceneAddNodeAsync(
        ISceneGraphService sceneGraphService,
        IGodotFileService fileService,
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Scene file name or relative path under projectPath."), Required] string fileName,
        [Description("Parent node path (for example: ., Player, Player/CameraRig)."), Required] string parentNodePath,
        [Description("Godot node type to create (for example: Node3D, Sprite2D, Control)."), Required] string nodeType,
        [Description("Name for the new node."), Required] string nodeName,
        [Description("Root node type used when bootstrap creation is needed (for example: Node, Node2D, Node3D).")] string root_type = "Node",
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(parentNodePath) || IsBlank(nodeType) || IsBlank(nodeName))
        {
            return Invalid("projectPath, fileName, parentNodePath, nodeType, and nodeName are required.");
        }
        string scenePath;
        try
        {
            scenePath = await EnsureSceneReadyAsync(fileService, pathResolver, projectPath, fileName, root_type, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message, "Use projectPath + /scenes/ + fileName (with .tscn extension).");
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
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Scene file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="nodePath">Node path to remove.</param>
    /// <param name="root_type">Root node type used if scene bootstrap creation is required.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing operation status.</returns>
    [McpServerTool(Name = "scene.remove_node"), Description("Remove a node and its descendants from a scene, then save the scene.")]
    public static async Task<ToolResult> SceneRemoveNodeAsync(
        ISceneGraphService sceneGraphService,
        IGodotFileService fileService,
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Scene file name or relative path under projectPath."), Required] string fileName,
        [Description("Node path to remove."), Required] string nodePath,
        [Description("Root node type used when bootstrap creation is needed (for example: Node, Node2D, Node3D).")] string root_type = "Node",
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(nodePath))
        {
            return Invalid("projectPath, fileName and nodePath are required.");
        }
        string scenePath;
        try
        {
            scenePath = await EnsureSceneReadyAsync(fileService, pathResolver, projectPath, fileName, root_type, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message, "Use projectPath + /scenes/ + fileName (with .tscn extension).");
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
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Scene file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="nodePath">Node path to move.</param>
    /// <param name="newParentPath">Destination parent node path.</param>
    /// <param name="root_type">Root node type used if scene bootstrap creation is required.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing operation status and optional moved node snapshot.</returns>
    [McpServerTool(Name = "scene.move_node"), Description("Reparent a node to a new parent node path in a scene, then save the scene.")]
    public static async Task<ToolResult> SceneMoveNodeAsync(
        ISceneGraphService sceneGraphService,
        IGodotFileService fileService,
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Scene file name or relative path under projectPath."), Required] string fileName,
        [Description("Node path to move."), Required] string nodePath,
        [Description("Destination parent node path (for example: ., Player, Player/CameraRig)."), Required] string newParentPath,
        [Description("Root node type used when bootstrap creation is needed (for example: Node, Node2D, Node3D).")] string root_type = "Node",
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(nodePath) || IsBlank(newParentPath))
        {
            return Invalid("projectPath, fileName, nodePath, and newParentPath are required.");
        }
        string scenePath;
        try
        {
            scenePath = await EnsureSceneReadyAsync(fileService, pathResolver, projectPath, fileName, root_type, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message, "Use projectPath + /scenes/ + fileName (with .tscn extension).");
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
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Scene file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="nodePath">Node path to rename.</param>
    /// <param name="newName">New node name.</param>
    /// <param name="root_type">Root node type used if scene bootstrap creation is required.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing operation status and optional renamed node snapshot.</returns>
    [McpServerTool(Name = "scene.rename_node"), Description("Rename an existing node in a scene, then save the scene.")]
    public static async Task<ToolResult> SceneRenameNodeAsync(
        ISceneGraphService sceneGraphService,
        IGodotFileService fileService,
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Scene file name or relative path under projectPath."), Required] string fileName,
        [Description("Node path to rename."), Required] string nodePath,
        [Description("New node name."), Required] string newName,
        [Description("Root node type used when bootstrap creation is needed (for example: Node, Node2D, Node3D).")] string root_type = "Node",
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(nodePath) || IsBlank(newName))
        {
            return Invalid("projectPath, fileName, nodePath, and newName are required.");
        }
        string scenePath;
        try
        {
            scenePath = await EnsureSceneReadyAsync(fileService, pathResolver, projectPath, fileName, root_type, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message, "Use projectPath + /scenes/ + fileName (with .tscn extension).");
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
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Scene file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="nodePath">Node path to inspect.</param>
    /// <param name="root_type">Root node type used if scene bootstrap creation is required.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing node properties when the node exists.</returns>
    [McpServerTool(Name = "scene.get_node_properties"), Description("Get the properties dictionary for a specific node in a scene.")]
    public static async Task<ToolResult> SceneGetNodePropertiesAsync(
        ISceneGraphService sceneGraphService,
        IGodotFileService fileService,
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Scene file name or relative path under projectPath."), Required] string fileName,
        [Description("Node path to inspect."), Required] string nodePath,
        [Description("Root node type used when bootstrap creation is needed (for example: Node, Node2D, Node3D).")] string root_type = "Node",
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(nodePath))
        {
            return Invalid("projectPath, fileName and nodePath are required.");
        }
        string scenePath;
        try
        {
            scenePath = await EnsureSceneReadyAsync(fileService, pathResolver, projectPath, fileName, root_type, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message, "Use projectPath + /scenes/ + fileName (with .tscn extension).");
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
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Scene file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="nodePath">Node path to update.</param>
    /// <param name="properties">Property map with primitive JSON values.</param>
    /// <param name="root_type">Root node type used if scene bootstrap creation is required.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing operation status and optional updated node snapshot.</returns>
    [McpServerTool(Name = "scene.set_node_properties"), Description("Update only the provided properties on a node and save the scene.")]
    public static async Task<ToolResult> SceneSetNodePropertiesAsync(
        ISceneGraphService sceneGraphService,
        IGodotFileService fileService,
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Scene file name or relative path under projectPath."), Required] string fileName,
        [Description("Node path to update."), Required] string nodePath,
        [Description("Property map to update. Values must be primitive JSON values."), Required]
        Dictionary<string, JsonElement>? properties,
        [Description("Root node type used when bootstrap creation is needed (for example: Node, Node2D, Node3D).")] string root_type = "Node",
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(nodePath))
        {
            return Invalid("projectPath, fileName and nodePath are required.");
        }
        string scenePath;
        try
        {
            scenePath = await EnsureSceneReadyAsync(fileService, pathResolver, projectPath, fileName, root_type, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message, "Use projectPath + /scenes/ + fileName (with .tscn extension).");
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

            var normalizedValue = ToPrimitiveValue(value);
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
