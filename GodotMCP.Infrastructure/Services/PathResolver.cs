using GodotMCP.Core.Interfaces;

namespace GodotMCP.Infrastructure.Services;

public sealed class PathResolver(string projectRoot) : IPathResolver
{
    public string ProjectRoot { get; } = Path.GetFullPath(projectRoot);

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

    public string ToResPath(string absolutePath)
    {
        var full = Path.GetFullPath(absolutePath);
        EnsureInsideProject(full);
        var relative = Path.GetRelativePath(ProjectRoot, full).Replace('\\', '/');
        return $"res://{relative}";
    }

    public void EnsureInsideProject(string absolutePath)
    {
        var root = ProjectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(absolutePath).StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Path escapes project root: {absolutePath}");
        }
    }
}
