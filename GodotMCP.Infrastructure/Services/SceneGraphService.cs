using System.Globalization;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;

namespace GodotMCP.Infrastructure.Services;

/// <summary>
/// Implements headless scene graph operations by reading and writing scene files.
/// </summary>
/// <param name="fileService">File abstraction for project I/O.</param>
/// <param name="sceneSerializer">Serializer used for scene parsing and emission.</param>
public sealed class SceneGraphService(
    IGodotFileService fileService,
    ISceneSerializer sceneSerializer) : ISceneGraphService
{
    /// <summary>
    /// Ordinal comparer used for deterministic path and property ordering.
    /// </summary>
    private static readonly StringComparer Comparer = StringComparer.Ordinal;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SceneGraphNodeInfo>> ListNodesAsync(string scenePath, CancellationToken cancellationToken = default)
    {
        var scene = await ReadSceneAsync(scenePath, cancellationToken).ConfigureAwait(false);
        var index = BuildIndex(scene);
        return BuildTree(index);
    }

    /// <inheritdoc />
    public async Task<SceneGraphMutationResult> AddNodeAsync(SceneGraphAddNodeRequest request, CancellationToken cancellationToken = default)
    {
        var scene = await ReadSceneAsync(request.ScenePath, cancellationToken).ConfigureAwait(false);
        var index = BuildIndex(scene);

        if (!TryResolveParentForInsert(index, request.ParentNodePath, out var parentPath, out var serializedParent, out var error))
        {
            return new SceneGraphMutationResult(false, error ?? "Invalid parent node path.");
        }

        var nodeName = request.NodeName.Trim();
        if (nodeName.Length == 0)
        {
            return new SceneGraphMutationResult(false, "nodeName is required.");
        }

        if (request.NodeType.Trim().Length == 0)
        {
            return new SceneGraphMutationResult(false, "nodeType is required.");
        }

        var newNodePath = ComposeNodePathFromSerializedParent(serializedParent, nodeName);
        if (index.ByPath.ContainsKey(newNodePath))
        {
            return new SceneGraphMutationResult(false, $"Node path '{newNodePath}' already exists.");
        }

        if (HasSiblingWithName(index, parentPath, nodeName))
        {
            return new SceneGraphMutationResult(false, $"A sibling node named '{nodeName}' already exists under '{DisplayParent(parentPath)}'.");
        }

        var node = new GodotNode
        {
            Name = nodeName,
            Type = request.NodeType.Trim(),
            Parent = serializedParent
        };

        scene.Nodes.Add(node);
        await WriteSceneAsync(request.ScenePath, scene, cancellationToken).ConfigureAwait(false);

        var updated = BuildIndex(scene);
        var snapshot = BuildNodeInfo(updated, updated.ByPath[newNodePath]);
        return new SceneGraphMutationResult(true, $"Node '{newNodePath}' added.", snapshot);
    }

    /// <inheritdoc />
    public async Task<SceneGraphMutationResult> RemoveNodeAsync(SceneGraphRemoveNodeRequest request, CancellationToken cancellationToken = default)
    {
        var scene = await ReadSceneAsync(request.ScenePath, cancellationToken).ConfigureAwait(false);
        var index = BuildIndex(scene);
        var targetPath = NormalizeNodePath(request.NodePath);

        if (!index.ByPath.TryGetValue(targetPath, out var target))
        {
            return new SceneGraphMutationResult(false, $"Node '{request.NodePath}' not found.");
        }

        var toRemove = index.Entries
            .Where(x => IsSameOrDescendant(x.Path, targetPath))
            .Select(x => x.Node)
            .ToHashSet();

        var removedCount = scene.Nodes.RemoveAll(n => toRemove.Contains(n));
        await WriteSceneAsync(request.ScenePath, scene, cancellationToken).ConfigureAwait(false);

        return new SceneGraphMutationResult(true, $"Removed {removedCount} node(s) from '{targetPath}'.");
    }

    /// <inheritdoc />
    public async Task<SceneGraphMutationResult> MoveNodeAsync(SceneGraphMoveNodeRequest request, CancellationToken cancellationToken = default)
    {
        var scene = await ReadSceneAsync(request.ScenePath, cancellationToken).ConfigureAwait(false);
        var index = BuildIndex(scene);
        var targetPath = NormalizeNodePath(request.NodePath);

        if (!index.ByPath.TryGetValue(targetPath, out var target))
        {
            return new SceneGraphMutationResult(false, $"Node '{request.NodePath}' not found.");
        }

        if (target.ParentPath is null)
        {
            return new SceneGraphMutationResult(false, "Root node cannot be moved.");
        }

        if (!TryResolveParentForInsert(index, request.NewParentPath, out var newParentPath, out var serializedParent, out var error))
        {
            return new SceneGraphMutationResult(false, error ?? "Invalid newParentPath.");
        }

        if (Comparer.Equals(newParentPath, targetPath) || (newParentPath is not null && IsSameOrDescendant(newParentPath, targetPath)))
        {
            return new SceneGraphMutationResult(false, "Cannot move a node under itself or one of its descendants.");
        }

        var candidatePath = ComposeNodePathFromSerializedParent(serializedParent, target.Node.Name);
        if (!Comparer.Equals(candidatePath, targetPath) && index.ByPath.ContainsKey(candidatePath))
        {
            return new SceneGraphMutationResult(false, $"A node already exists at '{candidatePath}'.");
        }

        if (HasSiblingWithName(index, newParentPath, target.Node.Name, exceptPath: targetPath))
        {
            return new SceneGraphMutationResult(false, $"A sibling node named '{target.Node.Name}' already exists under '{DisplayParent(newParentPath)}'.");
        }

        target.Node.Parent = serializedParent;
        await WriteSceneAsync(request.ScenePath, scene, cancellationToken).ConfigureAwait(false);

        var updated = BuildIndex(scene);
        var updatedPath = ComposeNodePathFromSerializedParent(serializedParent, target.Node.Name);
        var snapshot = updated.ByPath.TryGetValue(updatedPath, out var moved)
            ? BuildNodeInfo(updated, moved)
            : null;

        return new SceneGraphMutationResult(true, $"Moved node '{targetPath}' to '{DisplayParent(newParentPath)}'.", snapshot);
    }

    /// <inheritdoc />
    public async Task<SceneGraphMutationResult> RenameNodeAsync(SceneGraphRenameNodeRequest request, CancellationToken cancellationToken = default)
    {
        var scene = await ReadSceneAsync(request.ScenePath, cancellationToken).ConfigureAwait(false);
        var index = BuildIndex(scene);
        var targetPath = NormalizeNodePath(request.NodePath);

        if (!index.ByPath.TryGetValue(targetPath, out var target))
        {
            return new SceneGraphMutationResult(false, $"Node '{request.NodePath}' not found.");
        }

        var newName = request.NewName.Trim();
        if (newName.Length == 0)
        {
            return new SceneGraphMutationResult(false, "newName is required.");
        }

        var newPath = ComposeNodePathFromSerializedParent(target.Node.Parent, newName);
        if (!Comparer.Equals(newPath, targetPath) && index.ByPath.ContainsKey(newPath))
        {
            return new SceneGraphMutationResult(false, $"A node already exists at '{newPath}'.");
        }

        if (HasSiblingWithName(index, target.ParentPath, newName, exceptPath: targetPath))
        {
            return new SceneGraphMutationResult(false, $"A sibling node named '{newName}' already exists under '{DisplayParent(target.ParentPath)}'.");
        }

        target.Node.Name = newName;
        await WriteSceneAsync(request.ScenePath, scene, cancellationToken).ConfigureAwait(false);

        var updated = BuildIndex(scene);
        var snapshot = updated.ByPath.TryGetValue(newPath, out var renamed)
            ? BuildNodeInfo(updated, renamed)
            : null;

        return new SceneGraphMutationResult(true, $"Renamed node '{targetPath}' to '{newName}'.", snapshot);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetNodePropertiesAsync(string scenePath, string nodePath, CancellationToken cancellationToken = default)
    {
        var scene = await ReadSceneAsync(scenePath, cancellationToken).ConfigureAwait(false);
        var index = BuildIndex(scene);
        var targetPath = NormalizeNodePath(nodePath);

        if (!index.ByPath.TryGetValue(targetPath, out var target))
        {
            throw new InvalidOperationException($"Node '{nodePath}' not found.");
        }

        return target.Node.Properties
            .OrderBy(x => x.Key, Comparer)
            .ToDictionary(x => x.Key, x => x.Value, Comparer);
    }

    /// <inheritdoc />
    public async Task<SceneGraphMutationResult> SetNodePropertiesAsync(SceneGraphSetPropertiesRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Properties.Count == 0)
        {
            return new SceneGraphMutationResult(false, "properties must contain at least one entry.");
        }

        var scene = await ReadSceneAsync(request.ScenePath, cancellationToken).ConfigureAwait(false);
        var index = BuildIndex(scene);
        var targetPath = NormalizeNodePath(request.NodePath);

        if (!index.ByPath.TryGetValue(targetPath, out var target))
        {
            return new SceneGraphMutationResult(false, $"Node '{request.NodePath}' not found.");
        }

        foreach (var (key, value) in request.Properties)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return new SceneGraphMutationResult(false, "Property keys must be non-empty strings.");
            }

            if (!TryFormatPropertyValue(value, out var serialized))
            {
                return new SceneGraphMutationResult(false, $"Property '{key}' must be a primitive value (string, number, or boolean).",
                    BuildNodeInfo(index, target));
            }

            target.Node.Properties[key] = serialized;
        }

        await WriteSceneAsync(request.ScenePath, scene, cancellationToken).ConfigureAwait(false);

        var updated = BuildIndex(scene);
        var snapshot = BuildNodeInfo(updated, updated.ByPath[targetPath]);
        return new SceneGraphMutationResult(true, $"Updated {request.Properties.Count} propertie(s) on '{targetPath}'.", snapshot);
    }

    /// <summary>
    /// Reads and deserializes a scene file from project storage.
    /// </summary>
    /// <param name="scenePath">Scene path to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parsed scene model.</returns>
    private async Task<GodotScene> ReadSceneAsync(string scenePath, CancellationToken cancellationToken)
    {
        var text = await fileService.ReadAsync(scenePath, cancellationToken).ConfigureAwait(false);
        return sceneSerializer.Deserialize(text);
    }

    /// <summary>
    /// Serializes and writes a scene model to project storage.
    /// </summary>
    /// <param name="scenePath">Scene path to write.</param>
    /// <param name="scene">Scene model to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task WriteSceneAsync(string scenePath, GodotScene scene, CancellationToken cancellationToken)
    {
        await fileService.WriteAsync(scenePath, sceneSerializer.Serialize(scene), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds a recursive tree model from a flat indexed scene graph.
    /// </summary>
    /// <param name="index">Indexed scene representation.</param>
    /// <returns>Root-level tree nodes.</returns>
    private static IReadOnlyList<SceneGraphNodeInfo> BuildTree(SceneIndex index)
    {
        var childrenLookup = index.Entries
            .GroupBy(e => e.ParentPath ?? string.Empty, Comparer)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(x => x.Path, Comparer).ToList(),
                Comparer);

        var roots = index.Entries
            .Where(e => e.ParentPath is null)
            .OrderBy(e => e.Path, Comparer)
            .Select(e => BuildTreeNode(index, e, childrenLookup))
            .ToList();

        return roots;
    }

    /// <summary>
    /// Builds a recursive tree node using child lookup data.
    /// </summary>
    /// <param name="index">Indexed scene representation.</param>
    /// <param name="node">Current node entry.</param>
    /// <param name="childrenLookup">Lookup from parent path to direct children.</param>
    /// <returns>Recursive node DTO.</returns>
    private static SceneGraphNodeInfo BuildTreeNode(
        SceneIndex index,
        IndexedNode node,
        IReadOnlyDictionary<string, List<IndexedNode>> childrenLookup)
    {
        var children = childrenLookup.TryGetValue(node.Path, out var entries)
            ? entries.Select(x => BuildTreeNode(index, x, childrenLookup)).ToList()
            : [];

        return BuildNodeInfo(index, node, children);
    }

    /// <summary>
    /// Maps an indexed node entry into a domain scene graph descriptor.
    /// </summary>
    /// <param name="index">Indexed scene representation.</param>
    /// <param name="node">Indexed node entry.</param>
    /// <param name="children">Optional recursive children to attach.</param>
    /// <returns>Domain scene graph descriptor.</returns>
    private static SceneGraphNodeInfo BuildNodeInfo(SceneIndex index, IndexedNode node, IReadOnlyList<SceneGraphNodeInfo>? children = null)
    {
        var properties = node.Node.Properties
            .OrderBy(x => x.Key, Comparer)
            .ToDictionary(x => x.Key, x => x.Value, Comparer);

        return new SceneGraphNodeInfo(
            node.Node.Name,
            node.Node.Type,
            node.Path,
            DisplayParent(node.ParentPath),
            children ?? [],
            properties.GetValueOrDefault("script"),
            properties);
    }

            /// <summary>
            /// Builds fast lookup indexes over the scene nodes for path-based operations.
            /// </summary>
            /// <param name="scene">Scene model to index.</param>
            /// <returns>Scene index containing entries and path lookup map.</returns>
    private static SceneIndex BuildIndex(GodotScene scene)
    {
        var root = scene.Nodes.FirstOrDefault(n => string.IsNullOrWhiteSpace(n.Parent));
        var rootPath = root is null ? null : NormalizeNodePath(root.Name);

        var entries = new List<IndexedNode>(scene.Nodes.Count);
        var byPath = new Dictionary<string, IndexedNode>(Comparer);

        foreach (var node in scene.Nodes)
        {
            var parentPath = ResolveHierarchyParentPath(node, rootPath);
            var nodePath = ComputeNodePath(node);
            var indexed = new IndexedNode(nodePath, parentPath, node);

            entries.Add(indexed);
            byPath[nodePath] = indexed;
        }

        return new SceneIndex(rootPath, entries, byPath);
    }

    /// <summary>
    /// Resolves the logical hierarchy parent path for a node entry.
    /// </summary>
    /// <param name="node">Node whose parent is being resolved.</param>
    /// <param name="rootPath">Root node path for the scene, when available.</param>
    /// <returns>Hierarchy parent path, or <see langword="null"/> for scene root.</returns>
    private static string? ResolveHierarchyParentPath(GodotNode node, string? rootPath)
    {
        if (string.IsNullOrWhiteSpace(node.Parent))
        {
            return null;
        }

        if (node.Parent.Trim() == ".")
        {
            return rootPath;
        }

        var parent = NormalizeNodePath(node.Parent);
        return parent.Length == 0 ? rootPath : parent;
    }

    /// <summary>
    /// Resolves and validates a parent target for insert and move operations.
    /// </summary>
    /// <param name="index">Indexed scene representation.</param>
    /// <param name="parentNodePath">Requested parent path from the caller.</param>
    /// <param name="parentPath">Resolved normalized parent path.</param>
    /// <param name="serializedParent">Parent token to write into the scene file.</param>
    /// <param name="error">Validation message when resolution fails.</param>
    /// <returns><see langword="true"/> when parent resolution succeeds; otherwise <see langword="false"/>.</returns>
    private static bool TryResolveParentForInsert(
        SceneIndex index,
        string parentNodePath,
        out string? parentPath,
        out string serializedParent,
        out string? error)
    {
        error = null;
        serializedParent = ".";
        var normalized = NormalizeNodePath(parentNodePath);

        if (normalized.Length == 0)
        {
            parentPath = index.RootPath;
            if (index.RootPath is null)
            {
                serializedParent = string.Empty;
                return true;
            }

            serializedParent = ".";
            return true;
        }

        if (!index.ByPath.TryGetValue(normalized, out var parent))
        {
            parentPath = null;
            error = $"Parent node '{parentNodePath}' not found.";
            return false;
        }

        parentPath = parent.Path;
        serializedParent = parent.ParentPath is null ? "." : parent.Path;
        return true;
    }

    /// <summary>
    /// Determines whether another node under the same parent already uses the target name.
    /// </summary>
    /// <param name="index">Indexed scene representation.</param>
    /// <param name="parentPath">Parent path used for sibling scope.</param>
    /// <param name="candidateName">Candidate node name.</param>
    /// <param name="exceptPath">Optional path to exclude from conflict checks.</param>
    /// <returns><see langword="true"/> when a conflicting sibling exists.</returns>
    private static bool HasSiblingWithName(SceneIndex index, string? parentPath, string candidateName, string? exceptPath = null)
    {
        return index.Entries.Any(x =>
            (!Comparer.Equals(x.Path, exceptPath ?? string.Empty)) &&
            Comparer.Equals(x.ParentPath, parentPath) &&
            Comparer.Equals(x.Node.Name, candidateName));
    }

            /// <summary>
            /// Converts a boxed primitive value into a deterministic scene property string.
            /// </summary>
            /// <param name="value">Input value to convert.</param>
            /// <param name="serialized">Serialized scene value when conversion succeeds.</param>
            /// <returns><see langword="true"/> when the value type is supported; otherwise <see langword="false"/>.</returns>
    private static bool TryFormatPropertyValue(object? value, out string serialized)
    {
        serialized = string.Empty;

        switch (value)
        {
            case null:
                return false;
            case string text:
                serialized = text;
                return true;
            case bool boolean:
                serialized = boolean ? "true" : "false";
                return true;
            case byte b:
                serialized = b.ToString(CultureInfo.InvariantCulture);
                return true;
            case sbyte sb:
                serialized = sb.ToString(CultureInfo.InvariantCulture);
                return true;
            case short s:
                serialized = s.ToString(CultureInfo.InvariantCulture);
                return true;
            case ushort us:
                serialized = us.ToString(CultureInfo.InvariantCulture);
                return true;
            case int i:
                serialized = i.ToString(CultureInfo.InvariantCulture);
                return true;
            case uint ui:
                serialized = ui.ToString(CultureInfo.InvariantCulture);
                return true;
            case long l:
                serialized = l.ToString(CultureInfo.InvariantCulture);
                return true;
            case ulong ul:
                serialized = ul.ToString(CultureInfo.InvariantCulture);
                return true;
            case float f when !float.IsNaN(f) && !float.IsInfinity(f):
                serialized = f.ToString("0.0###", CultureInfo.InvariantCulture);
                return true;
            case double d when !double.IsNaN(d) && !double.IsInfinity(d):
                serialized = d.ToString("0.0###", CultureInfo.InvariantCulture);
                return true;
            case decimal dec:
                serialized = dec.ToString(CultureInfo.InvariantCulture);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Checks whether a candidate path is the same as, or a descendant of, an ancestor path.
    /// </summary>
    /// <param name="candidate">Candidate node path.</param>
    /// <param name="ancestor">Ancestor node path.</param>
    /// <returns><see langword="true"/> when candidate is same-or-descendant.</returns>
    private static bool IsSameOrDescendant(string candidate, string ancestor)
    {
        if (Comparer.Equals(candidate, ancestor))
        {
            return true;
        }

        return candidate.StartsWith($"{ancestor}/", StringComparison.Ordinal);
    }

    /// <summary>
    /// Computes a normalized node path from the serialized node parent and node name.
    /// </summary>
    /// <param name="node">Node to resolve.</param>
    /// <returns>Normalized node path.</returns>
    private static string ComputeNodePath(GodotNode node)
    {
        var parent = NormalizeNodePath(node.Parent);
        var normalizedName = NormalizeNodePath(node.Name);
        if (parent.Length == 0)
        {
            return normalizedName;
        }

        return $"{parent}/{normalizedName}";
    }

    /// <summary>
    /// Composes a normalized node path from a serialized parent token and node name.
    /// </summary>
    /// <param name="serializedParent">Serialized parent token from a scene node record.</param>
    /// <param name="name">Node name.</param>
    /// <returns>Normalized node path.</returns>
    private static string ComposeNodePathFromSerializedParent(string? serializedParent, string name)
    {
        var parent = NormalizeNodePath(serializedParent);
        var normalizedName = NormalizeNodePath(name);
        if (string.IsNullOrEmpty(parent))
        {
            return normalizedName;
        }

        return $"{parent}/{normalizedName}";
    }

    /// <summary>
    /// Normalizes node paths by trimming root markers and collapsing separators.
    /// </summary>
    /// <param name="path">Node path candidate.</param>
    /// <returns>Normalized path or an empty string for root markers.</returns>
    private static string NormalizeNodePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == ".")
        {
            return string.Empty;
        }

        var normalized = path.Replace('\\', '/').Trim();
        if (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        return string.Join('/', normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    /// <summary>
    /// Formats a nullable parent path for transport output.
    /// </summary>
    /// <param name="parentPath">Parent path value.</param>
    /// <returns><c>.</c> for root-level parents; otherwise the provided parent path.</returns>
    private static string DisplayParent(string? parentPath)
        => string.IsNullOrWhiteSpace(parentPath) ? "." : parentPath;

    /// <summary>
    /// Internal indexed node representation used during graph transformations.
    /// </summary>
    /// <param name="Path">Resolved node path.</param>
    /// <param name="ParentPath">Resolved parent path.</param>
    /// <param name="Node">Underlying mutable node model.</param>
    private sealed record IndexedNode(string Path, string? ParentPath, GodotNode Node);

    /// <summary>
    /// Internal scene index containing root metadata, flat entries, and fast path lookup.
    /// </summary>
    /// <param name="RootPath">Resolved scene root path.</param>
    /// <param name="Entries">Flat indexed node entries.</param>
    /// <param name="ByPath">Fast lookup map by normalized node path.</param>
    private sealed record SceneIndex(
        string? RootPath,
        IReadOnlyList<IndexedNode> Entries,
        IReadOnlyDictionary<string, IndexedNode> ByPath);
}
