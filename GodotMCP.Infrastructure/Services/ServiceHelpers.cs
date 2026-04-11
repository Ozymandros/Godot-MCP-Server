using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;

namespace GodotMCP.Infrastructure.Services;

/// <summary>
/// Shared helper methods used by infrastructure services.
/// </summary>
internal static class ServiceHelpers
{
    /// <summary>
    /// Normalizes a root path into a canonical <c>res://</c> directory path.
    /// </summary>
    /// <param name="pathResolver">Path resolver scoped to the current project.</param>
    /// <param name="rootPath">Input root path that may be absolute or project-relative.</param>
    /// <returns>Normalized <c>res://</c> directory path.</returns>
    public static string NormalizeDirectoryToResPath(IPathResolver pathResolver, string rootPath)
    {
        if (Path.IsPathRooted(rootPath))
        {
            pathResolver.EnsureInsideProject(rootPath);
            var resPath = pathResolver.ToResPath(rootPath);
            return resPath.EndsWith("/", StringComparison.Ordinal) ? resPath.TrimEnd('/') : resPath;
        }

        var normalized = rootPath.Replace('\\', '/');
        if (string.Equals(normalized, "res://", StringComparison.Ordinal))
        {
            return "res://";
        }

        var absolute = pathResolver.ResolveResPath(normalized);
        var res = pathResolver.ToResPath(absolute);
        return res.EndsWith("/", StringComparison.Ordinal) ? res.TrimEnd('/') : res;
    }

    /// <summary>
    /// Flattens recursive scene graph roots into a single node sequence.
    /// </summary>
    /// <param name="nodes">Root nodes to traverse.</param>
    /// <returns>Flattened recursive node sequence.</returns>
    public static IEnumerable<SceneGraphNodeInfo> FlattenNodes(IReadOnlyList<SceneGraphNodeInfo> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in FlattenNodes(node.Children))
            {
                yield return child;
            }
        }
    }
}
