using GodotMCP.Core.Models;
using StreamJsonRpc;

namespace GodotMCP.Application.Tools;

public partial class GodotTools
{
    [JsonRpcMethod("create_scene")]
    public async Task<ToolResult> CreateSceneAsync(string scenePath, string rootNodeName, string rootNodeType, CancellationToken cancellationToken = default)
    {
        if (IsBlank(rootNodeName) || IsBlank(rootNodeType))
        {
            return Invalid("rootNodeName and rootNodeType are required.");
        }
        if (!IsValidResPath(scenePath))
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

    [JsonRpcMethod("add_node")]
    public async Task<ToolResult> AddNodeAsync(string scenePath, string parentPath, string nodeName, string nodeType, CancellationToken cancellationToken = default)
    {
        if (IsBlank(nodeName) || IsBlank(nodeType) || IsBlank(parentPath))
        {
            return Invalid("parentPath, nodeName and nodeType are required.");
        }
        if (!IsValidResPath(scenePath))
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

    [JsonRpcMethod("set_node_property")]
    public async Task<ToolResult> SetNodePropertyAsync(
        string scenePath,
        string nodeName,
        string propertyKey,
        string propertyValue,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(nodeName) || IsBlank(propertyKey))
        {
            return Invalid("nodeName and propertyKey are required.");
        }
        if (!IsValidResPath(scenePath))
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

    [JsonRpcMethod("remove_node")]
    public async Task<ToolResult> RemoveNodeAsync(string scenePath, string nodeName, CancellationToken cancellationToken = default)
    {
        if (IsBlank(nodeName))
        {
            return Invalid("nodeName is required.");
        }
        if (!IsValidResPath(scenePath))
        {
            return Invalid("scenePath must be a valid project-relative path.");
        }

        var scene = sceneSerializer.Deserialize(await fileService.ReadAsync(scenePath, cancellationToken).ConfigureAwait(false));
        var removed = scene.Nodes.RemoveAll(n => n.Name == nodeName || n.Parent.Contains(nodeName, StringComparison.Ordinal));
        await fileService.WriteAsync(scenePath, sceneSerializer.Serialize(scene), cancellationToken).ConfigureAwait(false);
        return removed > 0 ? new ToolResult(true, $"Removed {removed} nodes.") : new ToolResult(false, "No matching nodes removed.");
    }

    [JsonRpcMethod("instantiate_packed_scene")]
    public async Task<ToolResult> InstantiatePackedSceneAsync(
        string targetScenePath,
        string parentPath,
        string packedScenePath,
        string instanceName,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(parentPath) || IsBlank(instanceName) || !IsValidResPath(targetScenePath) || !IsValidResPath(packedScenePath))
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

    [JsonRpcMethod("save_branch_as_scene")]
    public async Task<ToolResult> SaveBranchAsSceneAsync(
        string sourceScenePath,
        string nodeName,
        string destinationScenePath,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(nodeName) || !IsValidResPath(sourceScenePath) || !IsValidResPath(destinationScenePath))
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
