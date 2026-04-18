namespace GodotMCP.Core.Interfaces;

/// <summary>
/// Resolves and validates paths under the Godot project root.
/// </summary>
public interface IPathResolver
{
    /// <summary>
    /// Gets absolute project root path.
    /// </summary>
    string ProjectRoot { get; }

    /// <summary>
    /// Resolves a path to an absolute file system path inside the project.
    /// Accepts absolute paths, paths relative to the project root (forward slashes allowed),
    /// or legacy Godot virtual paths (<c>res://...</c>).
    /// </summary>
    /// <param name="path">Path to resolve.</param>
    /// <returns>Canonical absolute path.</returns>
    string ResolvePath(string path);

    /// <summary>
    /// Returns a project-relative path using forward slashes (no <c>res://</c> prefix).
    /// </summary>
    /// <param name="absolutePath">Absolute path inside the project.</param>
    string GetProjectRelativePath(string absolutePath);

    /// <summary>
    /// Formats an absolute path as a Godot <c>res://</c> URI for use inside <c>.tscn</c>, <c>.godot</c>, and similar files.
    /// </summary>
    /// <param name="absolutePath">Absolute path inside the project.</param>
    string ToGodotResPath(string absolutePath);

    /// <summary>
    /// Validates that an absolute path is within project boundaries.
    /// </summary>
    /// <param name="absolutePath">Absolute path to validate.</param>
    void EnsureInsideProject(string absolutePath);
}
