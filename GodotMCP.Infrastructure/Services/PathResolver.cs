using GodotMCP.Core;
using GodotMCP.Core.Interfaces;

namespace GodotMCP.Infrastructure.Services;

/// <summary>
/// Resolves project-relative paths and enforces project-root boundaries.
/// Uses <see cref="ProjectPathSyntax"/> for UNC detection, optional leading separators (Windows), and merge rules.
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

        if (ProjectPathSyntax.ContainsUriSchemeAuthority(trimmed))
        {
            throw new InvalidOperationException("Path schemes are not supported. Use absolute or project-relative filesystem paths.");
        }

        trimmed = ProjectPathSyntax.CollapseDuplicateDirectorySeparators(trimmed);

        if (ProjectPathSyntax.IsUncPath(trimmed))
        {
            var uncFull = Path.GetFullPath(trimmed);
            EnsureInsideProject(uncFull);
            return uncFull;
        }

        // Windows: a single leading '/' or '\' is the current-drive root, not POSIX "/".
        // Strip optional leading directory separators so "/scenes" and "scenes" match, and "///scenes" is not mistaken for UNC.
        if (OperatingSystem.IsWindows() && !ProjectPathSyntax.IsWindowsDriveAbsolutePath(trimmed))
        {
            var withoutLeadingSeparators = ProjectPathSyntax.TrimAllLeadingDirectorySeparators(trimmed);
            if (string.IsNullOrEmpty(withoutLeadingSeparators))
            {
                EnsureInsideProject(ProjectRoot);
                return Path.GetFullPath(ProjectRoot);
            }

            trimmed = withoutLeadingSeparators;
        }

        if (Path.IsPathRooted(trimmed))
        {
            var full = Path.GetFullPath(trimmed);
            if (IsWithinProject(full))
            {
                return full;
            }

            if (!OperatingSystem.IsWindows()
                && ProjectPathSyntax.ShouldReinterpretUnixAbsoluteAsProjectRelative(full))
            {
                return ResolveRelativeUnderProjectRoot(ProjectPathSyntax.TrimAllLeadingDirectorySeparators(trimmed));
            }

            EnsureInsideProject(full);
            return full;
        }

        return ResolveRelativeUnderProjectRoot(trimmed);
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
        if (!IsWithinProject(fullPath))
        {
            throw new InvalidOperationException($"Path escapes project root: {absolutePath}");
        }
    }

    private string ResolveRelativeUnderProjectRoot(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        var relative = normalized.TrimStart('/');
        var projectRootNormalized = ProjectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var combined = ProjectPathSyntax.CombineAvoidingDuplicateSegments(projectRootNormalized, relative);
        EnsureInsideProject(combined);
        return combined;
    }

    private bool IsWithinProject(string absolutePath)
    {
        var fullPath = Path.GetFullPath(absolutePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedRoot = ProjectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var rootWithSeparator = normalizedRoot + Path.DirectorySeparatorChar;

        var isProjectRoot = string.Equals(fullPath, normalizedRoot, StringComparison.OrdinalIgnoreCase);
        var isInsideProject = fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        return isProjectRoot || isInsideProject;
    }
}
