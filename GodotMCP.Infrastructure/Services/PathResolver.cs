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
    public string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Path is required.");
        }

        var trimmed = path.Trim();
        var normalized = trimmed.Replace('\\', '/');

        if (normalized.StartsWith("res://", StringComparison.Ordinal))
        {
            var rel = normalized["res://".Length..];
            while (rel.StartsWith('/'))
            {
                rel = rel[1..];
            }

            var absolute = string.IsNullOrEmpty(rel)
                ? ProjectRoot
                : Path.GetFullPath(Path.Combine(ProjectRoot, rel.Replace('/', Path.DirectorySeparatorChar)));
            EnsureInsideProject(absolute);
            return absolute;
        }

        if (Path.IsPathRooted(trimmed))
        {
            var full = Path.GetFullPath(trimmed);
            EnsureInsideProject(full);
            return full;
        }

        var relative = normalized.TrimStart('/');
        var combined = Path.GetFullPath(Path.Combine(ProjectRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        EnsureInsideProject(combined);
        return combined;
    }

    /// <inheritdoc />
    public string GetProjectRelativePath(string absolutePath)
    {
        var full = Path.GetFullPath(absolutePath);
        EnsureInsideProject(full);
        return Path.GetRelativePath(ProjectRoot, full).Replace('\\', '/');
    }

    /// <inheritdoc />
    public string ToGodotResPath(string absolutePath)
    {
        var rel = GetProjectRelativePath(absolutePath);
        if (string.IsNullOrEmpty(rel) || string.Equals(rel, ".", StringComparison.Ordinal))
        {
            return "res://";
        }

        return $"res://{rel}";
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
