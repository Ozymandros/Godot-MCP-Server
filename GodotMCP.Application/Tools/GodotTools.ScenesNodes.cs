using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    [McpServerTool(Name = "create_scene"), Description("Create a new Godot scene (.tscn) with a single root node.")]
    public static async Task<ToolResult> CreateSceneAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        [Description("Project path (res://...) for the new scene."), Required] string scenePath, 
        [Description("Name of the root node."), Required] string rootNodeName, 
        [Description("Godot type of the root node (e.g., Node2D, Control, Node3D)."), Required] string rootNodeType, 
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(rootNodeName) || IsBlank(rootNodeType))
        {
            return Invalid("rootNodeName and rootNodeType are required.");
        }
        if (!IsValidResPath(pathResolver, scenePath))
        {
            return Invalid("scenePath must be a valid project-relative path.", "Use paths like res://scenes/Main.tscn.");
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

    [McpServerTool(Name = "add_node"), Description("Append a new child node to a specific parent path in a Godot scene.")]
    public static async Task<ToolResult> AddNodeAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        [Description("Project path (res://...) to the scene file."), Required] string scenePath, 
        [Description("The hierarchy path of the parent node (e.g., '.', 'Player')."), Required] string parentPath, 
        [Description("Name for the new node."), Required] string nodeName, 
        [Description("Godot type for the new node."), Required] string nodeType, 
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(nodeName) || IsBlank(nodeType) || IsBlank(parentPath))
        {
            return Invalid("parentPath, nodeName and nodeType are required.");
        }
        if (!IsValidResPath(pathResolver, scenePath))
        {
            return Invalid("scenePath must be a valid project-relative path.");
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

    [McpServerTool(Name = "set_node_property"), Description("Modify or add a property value on a specific node in a scene.")]
    public static async Task<ToolResult> SetNodePropertyAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        [Description("Project path (res://...) to the scene file."), Required] string scenePath, 
        [Description("Name of the node to modify."), Required] string nodeName, 
        [Description("Property key (e.g., 'position', 'visible')."), Required] string propertyKey, 
        [Description("Raw text value for the property (e.g., 'Vector2(0, 0)')."), Required] string propertyValue, 
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(nodeName) || IsBlank(propertyKey))
        {
            return Invalid("nodeName and propertyKey are required.");
        }
        if (!IsValidResPath(pathResolver, scenePath))
        {
            return Invalid("scenePath must be a valid project-relative path.");
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

    [McpServerTool(Name = "remove_node"), Description("Remove a node and its recursive children from a Godot scene.")]
    public static async Task<ToolResult> RemoveNodeAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        [Description("Project path (res://...) to the scene file."), Required] string scenePath, 
        [Description("Name of the node to remove."), Required] string nodeName, 
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(nodeName))
        {
            return Invalid("nodeName is required.");
        }
        if (!IsValidResPath(pathResolver, scenePath))
        {
            return Invalid("scenePath must be a valid project-relative path.");
        }

        var scene = sceneSerializer.Deserialize(await fileService.ReadAsync(scenePath, cancellationToken).ConfigureAwait(false));
        var removed = scene.Nodes.RemoveAll(n => n.Name == nodeName || n.Parent.Contains(nodeName, StringComparison.Ordinal));
        await fileService.WriteAsync(scenePath, sceneSerializer.Serialize(scene), cancellationToken).ConfigureAwait(false);
        return removed > 0 ? new ToolResult(true, $"Removed {removed} nodes.") : new ToolResult(false, "No matching nodes removed.");
    }

    [McpServerTool(Name = "instantiate_packed_scene"), Description("Instantiate an existing .tscn file as a node inside another scene.")]
    public static async Task<ToolResult> InstantiatePackedSceneAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        [Description("Project path (res://...) to the scene file acting as the container."), Required] string targetScenePath, 
        [Description("Parent path within the target scene."), Required] string parentPath, 
        [Description("Project path (res://...) to the .tscn file to instantiate."), Required] string packedScenePath, 
        [Description("Name for the new instance node."), Required] string instanceName, 
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(parentPath) || IsBlank(instanceName) || !IsValidResPath(pathResolver, targetScenePath) || !IsValidResPath(pathResolver, packedScenePath))
        {
            return Invalid("targetScenePath, packedScenePath, parentPath and instanceName are required and must be valid.");
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

    [McpServerTool(Name = "save_branch_as_scene"), Description("Export a subtree branch from one scene into a new independent .tscn file.")]
    public static async Task<ToolResult> SaveBranchAsSceneAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        [Description("Project path (res://...) to the source scene file."), Required] string sourceScenePath, 
        [Description("Root node name of the branch to export."), Required] string nodeName, 
        [Description("Destination project path (res://...) for the exported scene."), Required] string destinationScenePath, 
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(nodeName) || !IsValidResPath(pathResolver, sourceScenePath) || !IsValidResPath(pathResolver, destinationScenePath))
        {
            return Invalid("sourceScenePath, destinationScenePath and nodeName are required and must be valid.");
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
