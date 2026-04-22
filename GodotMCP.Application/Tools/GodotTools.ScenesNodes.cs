using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using ModelContextProtocol.Server;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    /// <summary>
    /// Creates a new scene file with a single root node.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="sceneSerializer">Scene serializer used for deterministic output.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Scene file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="rootNodeName">Root node name.</param>
    /// <param name="rootNodeType">Root node type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing creation status.</returns>
    [McpServerTool(Name = "create_scene"), Description("Create a new Godot scene (.tscn) with a single root node.")]
    public static async Task<ToolResult> CreateSceneAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Scene file name or relative path under projectPath."), Required] string fileName,
        [Description("Name of the root node."), Required] string rootNodeName,
        [Description("Godot type of the root node (e.g., Node2D, Control, Node3D)."), Required] string rootNodeType,
        [Description("Raw scene text. If provided, written verbatim instead of generated skeleton.")] string? rawContent = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawContent) && (IsBlank(rootNodeName) || IsBlank(rootNodeType)))
        {
            return Invalid("rootNodeName and rootNodeType are required when rawContent is not provided.");
        }
        string scenePath;
        try
        {
            scenePath = ResolveProjectFilePath(pathResolver, projectPath, fileName);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message, "Use projectPath plus a relative fileName such as scenes/Main.tscn.");
        }

        if (!string.IsNullOrWhiteSpace(rawContent))
        {
            await fileService.WriteAsync(scenePath, rawContent, cancellationToken).ConfigureAwait(false);
            return new ToolResult(true, $"Scene created at {scenePath}.");
        }

        var scene = new GodotScene();
        scene.Nodes.Add(new GodotNode
        {
            Name = rootNodeName,
            Type = rootNodeType,
            Parent = string.Empty
        });
        await fileService.WriteAsync(scenePath, sceneSerializer.Serialize(scene), cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, $"Scene created at {scenePath}.");
    }

    /// <summary>
    /// Appends a child node to a scene under a specific parent path.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="sceneSerializer">Scene serializer used for parsing and writing.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Scene file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="parentPath">Parent node path.</param>
    /// <param name="nodeName">New node name.</param>
    /// <param name="nodeType">New node type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing mutation status.</returns>
    [McpServerTool(Name = "add_node"), Description("Append a new child node to a specific parent path in a Godot scene.")]
    public static async Task<ToolResult> AddNodeAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Scene file name or relative path under projectPath."), Required] string fileName,
        [Description("The hierarchy path of the parent node (e.g., '.', 'Player')."), Required] string parentPath,
        [Description("Name for the new node."), Required] string nodeName,
        [Description("Godot type for the new node."), Required] string nodeType,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(nodeName) || IsBlank(nodeType) || IsBlank(parentPath))
        {
            return Invalid("parentPath, nodeName and nodeType are required.");
        }
        string scenePath;
        try
        {
            scenePath = ResolveProjectFilePath(pathResolver, projectPath, fileName);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        var sceneText = await fileService.ReadAsync(scenePath, cancellationToken).ConfigureAwait(false);
        var scene = sceneSerializer.Deserialize(sceneText);
        scene.Nodes.Add(new GodotNode
        {
            Name = nodeName,
            Type = nodeType,
            Parent = parentPath
        });
        await fileService.WriteAsync(scenePath, sceneSerializer.Serialize(scene), cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, $"Node '{nodeName}' added.");
    }

    /// <summary>
    /// Sets or adds a single property on a node identified by name.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="sceneSerializer">Scene serializer used for parsing and writing.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Scene file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="nodeName">Target node name.</param>
    /// <param name="propertyKey">Property key to update.</param>
    /// <param name="propertyValue">Serialized property value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing mutation status.</returns>
    [McpServerTool(Name = "set_node_property"), Description("Modify or add a property value on a specific node in a scene.")]
    public static async Task<ToolResult> SetNodePropertyAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Scene file name or relative path under projectPath."), Required] string fileName,
        [Description("Name of the node to modify."), Required] string nodeName,
        [Description("Property key (e.g., 'position', 'visible')."), Required] string propertyKey,
        [Description("Raw text value for the property (e.g., 'Vector2(0, 0)')."), Required] string propertyValue,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(nodeName) || IsBlank(propertyKey))
        {
            return Invalid("nodeName and propertyKey are required.");
        }
        string scenePath;
        try
        {
            scenePath = ResolveProjectFilePath(pathResolver, projectPath, fileName);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        var scene = sceneSerializer.Deserialize(await fileService.ReadAsync(scenePath, cancellationToken).ConfigureAwait(false));
        var node = scene.Nodes.FirstOrDefault(n => n.Name == nodeName);
        if (node is null)
        {
            return new ToolResult(false, $"Node '{nodeName}' not found.");
        }

        node.Properties[propertyKey] = propertyValue;
        await fileService.WriteAsync(scenePath, sceneSerializer.Serialize(scene), cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, $"Property '{propertyKey}' updated for '{nodeName}'.");
    }

    /// <summary>
    /// Removes a node by name and recursively removes descendants.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="sceneSerializer">Scene serializer used for parsing and writing.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Scene file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="nodeName">Node name to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing removal status.</returns>
    [McpServerTool(Name = "remove_node"), Description("Remove a node and its recursive children from a Godot scene.")]
    public static async Task<ToolResult> RemoveNodeAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Scene file name or relative path under projectPath."), Required] string fileName,
        [Description("Name of the node to remove."), Required] string nodeName,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(nodeName))
        {
            return Invalid("nodeName is required.");
        }
        string scenePath;
        try
        {
            scenePath = ResolveProjectFilePath(pathResolver, projectPath, fileName);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        var scene = sceneSerializer.Deserialize(await fileService.ReadAsync(scenePath, cancellationToken).ConfigureAwait(false));
        var removed = scene.Nodes.RemoveAll(n => n.Name == nodeName || n.Parent.Contains(nodeName, StringComparison.Ordinal));
        await fileService.WriteAsync(scenePath, sceneSerializer.Serialize(scene), cancellationToken).ConfigureAwait(false);
        return removed > 0 ? new ToolResult(true, $"Removed {removed} nodes.") : new ToolResult(false, "No matching nodes removed.");
    }

    /// <summary>
    /// Adds a packed scene instance node to a target scene and creates an external resource entry.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="sceneSerializer">Scene serializer used for parsing and writing.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Target scene file name or relative path under <c>projectPath</c> (container scene).</param>
    /// <param name="parentPath">Parent path in the target scene.</param>
    /// <param name="packedSceneFileName">Packed scene file name or relative path under <c>projectPath</c>.</param>
    /// <param name="instanceName">Name for the instance node.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing instantiation status.</returns>
    [McpServerTool(Name = "instantiate_packed_scene"), Description("Instantiate an existing .tscn file as a node inside another scene.")]
    public static async Task<ToolResult> InstantiatePackedSceneAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Target scene file name or relative path under projectPath."), Required] string fileName,
        [Description("Parent path within the target scene."), Required] string parentPath,
        [Description("Packed scene file name or relative path under projectPath."), Required] string packedSceneFileName,
        [Description("Name for the new instance node."), Required] string instanceName,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(parentPath) || IsBlank(instanceName))
        {
            return Invalid("projectPath, fileName, packedSceneFileName, parentPath and instanceName are required.");
        }
        string targetScenePath;
        string packedScenePath;
        try
        {
            targetScenePath = ResolveProjectFilePath(pathResolver, projectPath, fileName);
            packedScenePath = ResolveProjectFilePath(pathResolver, projectPath, packedSceneFileName);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        var scene = sceneSerializer.Deserialize(await fileService.ReadAsync(targetScenePath, cancellationToken).ConfigureAwait(false));
        var id = (scene.ExternalResources.Count + 1).ToString();
        scene.ExternalResources.Add(new ExtResource { Id = id, Type = "PackedScene", Path = packedScenePath });
        scene.Nodes.Add(new GodotNode
        {
            Name = instanceName,
            Type = "Node",
            Parent = parentPath,
            Instance = $"ExtResource(\"{id}\")"
        });
        await fileService.WriteAsync(targetScenePath, sceneSerializer.Serialize(scene), cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, $"Packed scene instance '{instanceName}' added.");
    }

    /// <summary>
    /// Exports a node branch from one scene into a new independent scene file.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="sceneSerializer">Scene serializer used for parsing and writing.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Source scene file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="nodeName">Branch root node name in the source scene.</param>
    /// <param name="destinationFileName">Destination scene file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing export status.</returns>
    [McpServerTool(Name = "save_branch_as_scene"), Description("Export a subtree branch from one scene into a new independent .tscn file.")]
    public static async Task<ToolResult> SaveBranchAsSceneAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Source scene file name or relative path under projectPath."), Required] string fileName,
        [Description("Root node name of the branch to export."), Required] string nodeName,
        [Description("Destination scene file name or relative path under projectPath."), Required] string destinationFileName,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(nodeName))
        {
            return Invalid("projectPath, fileName, destinationFileName and nodeName are required.");
        }
        string sourceScenePath;
        string destinationScenePath;
        try
        {
            sourceScenePath = ResolveProjectFilePath(pathResolver, projectPath, fileName);
            destinationScenePath = ResolveProjectFilePath(pathResolver, projectPath, destinationFileName);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        var sourceScene = sceneSerializer.Deserialize(await fileService.ReadAsync(sourceScenePath, cancellationToken).ConfigureAwait(false));
        var branchRoot = sourceScene.Nodes.FirstOrDefault(n => n.Name == nodeName);
        if (branchRoot is null)
        {
            return new ToolResult(false, $"Node '{nodeName}' was not found.");
        }

        var branch = new GodotScene();
        branch.Nodes.Add(new GodotNode { Name = branchRoot.Name, Type = branchRoot.Type, Parent = string.Empty });
        foreach (var node in sourceScene.Nodes.Where(n => n.Parent.Contains(nodeName, StringComparison.Ordinal)))
        {
            branch.Nodes.Add(new GodotNode { Name = node.Name, Type = node.Type, Parent = node.Parent });
        }

        await fileService.WriteAsync(destinationScenePath, sceneSerializer.Serialize(branch), cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, $"Branch saved to '{destinationScenePath}'.");
    }
}
