using GodotMCP.Core.Interfaces;

namespace GodotMCP.Infrastructure.Services;

/// <summary>
/// Resolves project-relative paths and enforces project-root boundaries.
/// </summary>
/// <param name="projectRoot">Project root directory.</param>
public sealed class PathResolver(string projectRoot) : IPathResolver
{
    /// <inheritdoc />
    public string ProjectRoot { get; } = Path.GetFullPath(projectRoot);

    /// <inheritdoc />
    public string ResolveResPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        var relative = normalized.StartsWith("res://", StringComparison.Ordinal)
            ? normalized["res://".Length..]
            : normalized.TrimStart('/');
        var absolute = Path.GetFullPath(Path.Combine(ProjectRoot, relative));
        EnsureInsideProject(absolute);
        return absolute;
    }

    /// <inheritdoc />
    public string ToResPath(string absolutePath)
    {
        var full = Path.GetFullPath(absolutePath);
        EnsureInsideProject(full);
        var relative = Path.GetRelativePath(ProjectRoot, full).Replace('\\', '/');
        return $"res://{relative}";
    }

    /// <inheritdoc />
    public void EnsureInsideProject(string absolutePath)
    {
        var fullPath = Path.GetFullPath(absolutePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedRoot = ProjectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var rootWithSeparator = normalizedRoot + Path.DirectorySeparatorChar;

        var isProjectRoot = string.Equals(fullPath, normalizedRoot, StringComparison.OrdinalIgnoreCase);
        var isInsideProject = fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        if (!isProjectRoot && !isInsideProject)
        {
            throw new InvalidOperationException($"Path escapes project root: {absolutePath}");
        }
    }
}
