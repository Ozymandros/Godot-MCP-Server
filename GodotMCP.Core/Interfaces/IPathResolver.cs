namespace GodotMCP.Core.Interfaces;

/// <summary>
/// Resolves project-relative resource paths and provides the configured project root.
/// </summary>
public interface IPathResolver
{
    /// <summary>
    /// The absolute project root path used to resolve resource paths.
    /// </summary>
    string ProjectRoot { get; }

    /// <summary>
    /// Resolves a Godot resource-style path (for example <c>res://scenes/Main.tscn</c>)
    /// to an absolute filesystem path inside the project.
    /// </summary>
    string ResolveResPath(string path);

    /// <summary>
    /// Converts an absolute filesystem path inside the project to a Godot resource
    /// path (<c>res://...</c>).
    /// </summary>
    string ToResPath(string absolutePath);

    /// <summary>
    /// Throws when <paramref name="absolutePath"/> is outside the configured project root.
    /// </summary>
    void EnsureInsideProject(string absolutePath);
}
