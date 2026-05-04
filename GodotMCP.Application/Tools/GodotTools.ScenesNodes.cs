using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using GodotMCP.Core.SceneGraph;
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
    /// <param name="rawContent">Raw scene text. If provided, written verbatim instead of generated skeleton.</param>
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
            scenePath = ResolveSceneFilePath(pathResolver, projectPath, fileName);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message, "Use projectPath + /scenes/ + fileName (with .tscn extension).");
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
    /// <param name="sceneGraphService">Scene graph service for validated inserts.</param>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="sceneSerializer">Scene serializer (reserved for MCP host compatibility).</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Scene file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="parentPath">Parent node path.</param>
    /// <param name="nodeName">New node name.</param>
    /// <param name="nodeType">New node type.</param>
    /// <param name="root_type">Root node type used if scene bootstrap creation is required.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing mutation status.</returns>
    [McpServerTool(Name = "add_node"), Description("Append a new child node to a specific parent path in a Godot scene. Uses the same parent validation as scene.add_node.")]
    public static async Task<ToolResult> AddNodeAsync(
        ISceneGraphService sceneGraphService,
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Scene file name or relative path under projectPath."), Required] string fileName,
        [Description("The hierarchy path of the parent node (e.g., '.', 'Player')."), Required] string parentPath,
        [Description("Name for the new node."), Required] string nodeName,
        [Description("Godot type for the new node."), Required] string nodeType,
        [Description("Root node type used when bootstrap creation is needed (for example: Node, Node2D, Node3D).")] string root_type = "Node",
        CancellationToken cancellationToken = default)
    {
        _ = sceneSerializer;
        if (IsBlank(nodeName) || IsBlank(nodeType) || IsBlank(parentPath))
        {
            return Invalid("parentPath, nodeName and nodeType are required.");
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
            .AddNodeAsync(new SceneGraphAddNodeRequest(scenePath, parentPath, nodeType, nodeName), cancellationToken)
            .ConfigureAwait(false);

        return ToToolResult(result);
    }

    /// <summary>
    /// Sets or adds a single property on a node identified by name.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="sceneSerializer">Scene serializer used for parsing and writing.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Scene file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="nodePath">Target node path in the scene.</param>
    /// <param name="propertyKey">Property key to update.</param>
    /// <param name="propertyValue">Serialized property value.</param>
    /// <param name="root_type">Root node type used if scene bootstrap creation is required.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing mutation status.</returns>
    [McpServerTool(Name = "set_node_property"), Description("Modify or add a property value on a specific node in a scene.")]
    public static async Task<ToolResult> SetNodePropertyAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Scene file name or relative path under projectPath."), Required] string fileName,
        [Description("Node path in the scene (e.g. Player, Player/CameraRig)."), Required] string nodePath,
        [Description("Property key (e.g., 'position', 'visible')."), Required] string propertyKey,
        [Description("Raw text value for the property (e.g., 'Vector2(0, 0)')."), Required] string propertyValue,
        [Description("Root node type used when bootstrap creation is needed (for example: Node, Node2D, Node3D).")] string root_type = "Node",
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(nodePath) || IsBlank(propertyKey))
        {
            return Invalid("nodePath and propertyKey are required.");
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

        var scene = sceneSerializer.Deserialize(await fileService.ReadAsync(scenePath, cancellationToken).ConfigureAwait(false));
        var index = SceneNodePathIndex.Build(scene);
        if (!SceneNodePathIndex.TryGetNode(index, nodePath, out var node) || node is null)
        {
            return new ToolResult(false, $"Node '{nodePath}' not found.");
        }

        node.Properties[propertyKey] = propertyValue;
        await fileService.WriteAsync(scenePath, sceneSerializer.Serialize(scene), cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, $"Property '{propertyKey}' updated for '{nodePath}'.");
    }

    /// <summary>
    /// Removes a node by name and recursively removes descendants.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="sceneSerializer">Scene serializer used for parsing and writing.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Scene file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="nodePath">Node path to remove (subtree).</param>
    /// <param name="root_type">Root node type used if scene bootstrap creation is required.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing removal status.</returns>
    [McpServerTool(Name = "remove_node"), Description("Remove a node and its recursive children from a Godot scene.")]
    public static async Task<ToolResult> RemoveNodeAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Scene file name or relative path under projectPath."), Required] string fileName,
        [Description("Node path to remove including descendants (e.g. Player, UI/Button)."), Required] string nodePath,
        [Description("Root node type used when bootstrap creation is needed (for example: Node, Node2D, Node3D).")] string root_type = "Node",
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(nodePath))
        {
            return Invalid("nodePath is required.");
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

        var scene = sceneSerializer.Deserialize(await fileService.ReadAsync(scenePath, cancellationToken).ConfigureAwait(false));
        var index = SceneNodePathIndex.Build(scene);
        var normalized = SceneNodePathIndex.NormalizeNodePath(nodePath);
        if (!index.ByPath.ContainsKey(normalized))
        {
            return new ToolResult(false, $"Node '{nodePath}' not found.");
        }

        var toRemove = SceneNodePathIndex.GetRemovalSet(index, normalized);
        var removed = scene.Nodes.RemoveAll(n => toRemove.Contains(n));
        await fileService.WriteAsync(scenePath, sceneSerializer.Serialize(scene), cancellationToken).ConfigureAwait(false);
        return removed > 0 ? new ToolResult(true, $"Removed {removed} node(s).") : new ToolResult(false, "No matching nodes removed.");
    }

    /// <summary>
    /// Adds a packed scene instance node to a target scene and creates an external resource entry.
    /// </summary>
    /// <param name="sceneGraphService">Scene graph service for validated parent and sibling checks.</param>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="sceneSerializer">Scene serializer (reserved for MCP host compatibility).</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Target scene file name or relative path under <c>projectPath</c> (container scene).</param>
    /// <param name="parentPath">Parent path in the target scene.</param>
    /// <param name="packedSceneFileName">Packed scene file name or relative path under <c>projectPath</c>.</param>
    /// <param name="instanceName">Name for the instance node.</param>
    /// <param name="root_type">Root node type used if scene bootstrap creation is required.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing instantiation status.</returns>
    [McpServerTool(Name = "instantiate_packed_scene"), Description("Instantiate an existing .tscn file as a node inside another scene. Parent path is validated like scene.add_node.")]
    public static async Task<ToolResult> InstantiatePackedSceneAsync(
        ISceneGraphService sceneGraphService,
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Target scene file name or relative path under projectPath."), Required] string fileName,
        [Description("Parent path within the target scene."), Required] string parentPath,
        [Description("Packed scene file name or relative path under projectPath."), Required] string packedSceneFileName,
        [Description("Name for the new instance node."), Required] string instanceName,
        [Description("Root node type used when bootstrap creation is needed (for example: Node, Node2D, Node3D).")] string root_type = "Node",
        CancellationToken cancellationToken = default)
    {
        _ = sceneSerializer;
        if (IsBlank(parentPath) || IsBlank(instanceName))
        {
            return Invalid("projectPath, fileName, packedSceneFileName, parentPath and instanceName are required.");
        }
        string targetScenePath;
        string packedScenePath;
        try
        {
            targetScenePath = await EnsureSceneReadyAsync(fileService, pathResolver, projectPath, fileName, root_type, cancellationToken).ConfigureAwait(false);
            packedScenePath = ResolveSceneFilePath(pathResolver, projectPath, packedSceneFileName);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message, "Use projectPath + /scenes/ + fileName (with .tscn extension).");
        }

        var result = await sceneGraphService
            .InstantiatePackedSceneAsync(
                new SceneGraphInstantiatePackedSceneRequest(targetScenePath, parentPath, packedScenePath, instanceName),
                cancellationToken)
            .ConfigureAwait(false);

        return ToToolResult(result);
    }

    /// <summary>
    /// Exports a node branch from one scene into a new independent scene file.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="sceneSerializer">Scene serializer used for parsing and writing.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Source scene file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="branchNodePath">Branch root node path in the source scene.</param>
    /// <param name="destinationFileName">Destination scene file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="root_type">Root node type used if scene bootstrap creation is required.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing export status.</returns>
    [McpServerTool(Name = "save_branch_as_scene"), Description("Export a subtree branch from one scene into a new independent .tscn file.")]
    public static async Task<ToolResult> SaveBranchAsSceneAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Source scene file name or relative path under projectPath."), Required] string fileName,
        [Description("Root node path of the branch to export (e.g. Player, Player/CameraRig)."), Required] string branchNodePath,
        [Description("Destination scene file name or relative path under projectPath."), Required] string destinationFileName,
        [Description("Root node type used when bootstrap creation is needed (for example: Node, Node2D, Node3D).")] string root_type = "Node",
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(branchNodePath))
        {
            return Invalid("projectPath, fileName, destinationFileName and branchNodePath are required.");
        }
        string sourceScenePath;
        string destinationScenePath;
        try
        {
            sourceScenePath = await EnsureSceneReadyAsync(fileService, pathResolver, projectPath, fileName, root_type, cancellationToken).ConfigureAwait(false);
            destinationScenePath = ResolveSceneFilePath(pathResolver, projectPath, destinationFileName);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message, "Use projectPath + /scenes/ + fileName (with .tscn extension).");
        }

        var sourceScene = sceneSerializer.Deserialize(await fileService.ReadAsync(sourceScenePath, cancellationToken).ConfigureAwait(false));
        var pathIndex = SceneNodePathIndex.Build(sourceScene);
        var branchKey = SceneNodePathIndex.NormalizeNodePath(branchNodePath);
        if (!pathIndex.ByPath.TryGetValue(branchKey, out _))
        {
            return new ToolResult(false, $"Node '{branchNodePath}' was not found.");
        }

        var inBranch = pathIndex.Entries
            .Where(e => e.Path == branchKey || SceneNodePathIndex.IsSameOrDescendant(e.Path, branchKey))
            .OrderBy(e => e.Path.Equals(branchKey, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(e => e.Path, StringComparer.Ordinal)
            .ToList();

        var extIds = new HashSet<string>(StringComparer.Ordinal);
        var subIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in inBranch)
        {
            CollectResourceReferencesFromNode(entry.Node, extIds, subIds);
        }

        ExpandSubResourceClosure(sourceScene, extIds, subIds);

        var branch = new GodotScene();
        foreach (var sub in sourceScene.SubResources.Where(s => subIds.Contains(s.Id)).OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            var copy = new SubResource { Id = sub.Id, Type = sub.Type };
            foreach (var kv in sub.Properties)
            {
                copy.Properties[kv.Key] = kv.Value;
            }

            branch.SubResources.Add(copy);
        }

        foreach (var ext in sourceScene.ExternalResources.Where(e => extIds.Contains(e.Id)).OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            branch.ExternalResources.Add(new ExtResource { Id = ext.Id, Path = ext.Path, Type = ext.Type });
        }

        branch.RecomputeLoadSteps();

        foreach (var e in inBranch)
        {
            var newParent = e.Path == branchKey
                ? string.Empty
                : SceneNodePathIndex.RemapParentForBranchExport(e.Node.Parent, branchKey);

            var gn = new GodotNode
            {
                Name = e.Node.Name,
                Type = e.Node.Type,
                Parent = newParent,
                Instance = e.Node.Instance
            };

            foreach (var kv in e.Node.Properties)
            {
                gn.Properties[kv.Key] = kv.Value;
            }

            branch.Nodes.Add(gn);
        }

        await fileService.WriteAsync(destinationScenePath, sceneSerializer.Serialize(branch), cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, $"Branch saved to '{destinationScenePath}'.");
    }

    private static readonly Regex ExtResourceReferenceRegex = new(@"ExtResource\(\s*""([^""]+)""\s*\)", RegexOptions.Compiled);

    private static readonly Regex SubResourceReferenceRegex = new(@"SubResource\(\s*""([^""]+)""\s*\)", RegexOptions.Compiled);

    private static void CollectResourceReferencesFromNode(GodotNode node, ISet<string> extIds, ISet<string> subIds)
    {
        if (!string.IsNullOrWhiteSpace(node.Instance))
        {
            CollectResourceReferencesFromText(node.Instance, extIds, subIds);
        }

        foreach (var value in node.Properties.Values)
        {
            CollectResourceReferencesFromText(value, extIds, subIds);
        }
    }

    private static void CollectResourceReferencesFromText(string text, ISet<string> extIds, ISet<string> subIds)
    {
        foreach (Match m in ExtResourceReferenceRegex.Matches(text))
        {
            extIds.Add(m.Groups[1].Value);
        }

        foreach (Match m in SubResourceReferenceRegex.Matches(text))
        {
            subIds.Add(m.Groups[1].Value);
        }
    }

    private static void ExpandSubResourceClosure(GodotScene source, ISet<string> extIds, ISet<string> subIds)
    {
        var queue = new Queue<string>(subIds);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            var sub = source.SubResources.FirstOrDefault(s => s.Id == id);
            if (sub is null)
            {
                continue;
            }

            foreach (var value in sub.Properties.Values)
            {
                foreach (Match m in ExtResourceReferenceRegex.Matches(value))
                {
                    extIds.Add(m.Groups[1].Value);
                }

                foreach (Match m in SubResourceReferenceRegex.Matches(value))
                {
                    var sid = m.Groups[1].Value;
                    if (subIds.Add(sid))
                    {
                        queue.Enqueue(sid);
                    }
                }
            }
        }
    }
}
