using GodotMCP.Core.Models;

namespace GodotMCP.Core.SceneGraph;

/// <summary>
/// Builds path-keyed indexes over flat <see cref="GodotScene"/> node lists for consistent targeting across tools.
/// </summary>
public static class SceneNodePathIndex
{
    /// <summary>
    /// Ordinal comparer for paths and names.
    /// </summary>
    public static StringComparer Comparer { get; } = StringComparer.Ordinal;

    /// <summary>
    /// Builds an index of nodes by normalized path.
    /// </summary>
    /// <param name="scene">Scene to index.</param>
    /// <returns>Lookup index.</returns>
    public static ScenePathIndex Build(GodotScene scene)
    {
        var root = scene.Nodes.FirstOrDefault(n => string.IsNullOrWhiteSpace(n.Parent));
        var rootPath = root is null ? null : NormalizeNodePath(root.Name);

        var entries = new List<ScenePathEntry>(scene.Nodes.Count);
        var byPath = new Dictionary<string, ScenePathEntry>(Comparer);

        foreach (var node in scene.Nodes)
        {
            var parentPath = ResolveHierarchyParentPath(node, rootPath);
            var nodePath = ComputeNodePath(node);
            var indexed = new ScenePathEntry(nodePath, parentPath, node);

            entries.Add(indexed);
            byPath[nodePath] = indexed;
        }

        return new ScenePathIndex(rootPath, entries, byPath);
    }

    /// <summary>
    /// Tries to resolve a node by normalized path.
    /// </summary>
    /// <param name="index">Built index.</param>
    /// <param name="nodePath">Path such as <c>Player</c> or <c>Player/CameraRig</c>.</param>
    /// <param name="node">Matching node when found.</param>
    /// <returns><see langword="true"/> when a node exists at the path.</returns>
    public static bool TryGetNode(ScenePathIndex index, string nodePath, out GodotNode? node)
    {
        var key = NormalizeNodePath(nodePath);
        if (index.ByPath.TryGetValue(key, out var entry))
        {
            node = entry.Node;
            return true;
        }

        node = null;
        return false;
    }

    /// <summary>
    /// Collects every scene node object that matches the removal rule (target path and descendants).
    /// </summary>
    /// <param name="index">Built index.</param>
    /// <param name="targetPath">Normalized path of subtree root to remove.</param>
    /// <returns>Distinct Godot nodes to remove from <see cref="GodotScene.Nodes"/>.</returns>
    public static HashSet<GodotNode> GetRemovalSet(ScenePathIndex index, string targetPath)
    {
        var normalized = NormalizeNodePath(targetPath);
        return index.Entries
            .Where(x => IsSameOrDescendant(x.Path, normalized))
            .Select(x => x.Node)
            .ToHashSet();
    }

    /// <summary>
    /// Checks whether a candidate path is the same as, or a descendant of, an ancestor path.
    /// </summary>
    public static bool IsSameOrDescendant(string candidate, string ancestor)
    {
        if (Comparer.Equals(candidate, ancestor))
        {
            return true;
        }

        return candidate.StartsWith($"{ancestor}/", StringComparison.Ordinal);
    }

    /// <summary>
    /// Normalizes node paths by trimming root markers and collapsing separators.
    /// </summary>
    /// <param name="path">Node path candidate.</param>
    /// <returns>Normalized path or an empty string for root markers.</returns>
    public static string NormalizeNodePath(string? path)
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
    /// Computes normalized path key from serialized parent + name (matches scene graph indexing).
    /// </summary>
    public static string ComposeNodePathFromSerializedParent(string? serializedParent, string name)
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
    /// Remaps a node's serialized <c>parent=</c> string when exporting a branch under <paramref name="branchRootPath"/> into a new scene whose root is that branch.
    /// </summary>
    public static string RemapParentForBranchExport(string? oldParentSerialized, string branchRootPath)
    {
        var b = NormalizeNodePath(branchRootPath);
        if (string.IsNullOrWhiteSpace(oldParentSerialized))
        {
            return string.Empty;
        }

        var p = NormalizeNodePath(oldParentSerialized);
        if (p == b)
        {
            return ".";
        }

        var prefix = $"{b}/";
        if (p.StartsWith(prefix, StringComparison.Ordinal))
        {
            return p[prefix.Length..];
        }

        return oldParentSerialized.Trim();
    }

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
}

/// <summary>
/// Indexed scene graph entry for path-based lookups.
/// </summary>
/// <param name="Path">Normalized node path.</param>
/// <param name="ParentPath">Logical parent path in the scene graph.</param>
/// <param name="Node">Underlying mutable node.</param>
public sealed record ScenePathEntry(string Path, string? ParentPath, GodotNode Node);

/// <summary>
/// Fast lookup structure produced by <see cref="SceneNodePathIndex.Build"/>.
/// </summary>
/// <param name="RootPath">Path key of the scene root node.</param>
/// <param name="Entries">All indexed entries.</param>
/// <param name="ByPath">Lookup by normalized path.</param>
public sealed record ScenePathIndex(
    string? RootPath,
    IReadOnlyList<ScenePathEntry> Entries,
    IReadOnlyDictionary<string, ScenePathEntry> ByPath);
