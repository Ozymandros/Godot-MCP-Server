using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;

namespace GodotMCP.Infrastructure.Services;

/// <summary>
/// Shared helper methods used by infrastructure services.
/// </summary>
internal static class ServiceHelpers
{
    /// <summary>
    /// Normalizes a scan root into an absolute directory path inside the project.
    /// </summary>
    /// <param name="pathResolver">Path resolver scoped to the current project.</param>
    /// <param name="rootPath">Input root path that may be absolute or project-relative.</param>
    /// <returns>Absolute directory path.</returns>
    public static string NormalizeProjectDirectory(IPathResolver pathResolver, string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return pathResolver.ProjectRoot;
        }

        return pathResolver.ResolvePath(rootPath);
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
