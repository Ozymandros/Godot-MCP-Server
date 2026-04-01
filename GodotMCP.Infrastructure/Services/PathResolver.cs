using GodotMCP.Core.Interfaces;

namespace GodotMCP.Infrastructure.Services;

/// <summary>   
///  Path resolver for Godot resource paths (res://) to absolute filesystem paths inside a configured project root.
/// </summary>
public sealed class PathResolver(string projectRoot) : IPathResolver
{
    /// <summary>Absolute project root path used to resolve Godot resource paths.</summary>
    public string ProjectRoot { get; } = Path.GetFullPath(projectRoot);

    /// <summary>
    /// Resolve a Godot-style resource path (eg. <c>res://scenes/Main.tscn</c>) to an
    /// absolute filesystem path inside the configured project root.
    /// </summary>
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

    /// <summary>Convert an absolute filesystem path inside the project to a <c>res://</c> path.</summary>
    public string ToResPath(string absolutePath)
    {
        var full = Path.GetFullPath(absolutePath);
        EnsureInsideProject(full);
        var relative = Path.GetRelativePath(ProjectRoot, full).Replace('\\', '/');
        return $"res://{relative}";
    }

    /// <summary>Throws when the provided absolute path is outside the configured project root.</summary>
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
