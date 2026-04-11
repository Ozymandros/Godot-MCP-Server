namespace GodotMCP.Core.Interfaces;

/// <summary>
/// Resolves and validates project-relative paths.
/// </summary>
public interface IPathResolver
{
    /// <summary>
    /// Gets absolute project root path.
    /// </summary>
    string ProjectRoot { get; }

    /// <summary>
    /// Resolves a <c>res://</c> path into an absolute project path.
    /// </summary>
    /// <param name="path">Project-relative path.</param>
    /// <returns>Absolute file system path.</returns>
    string ResolveResPath(string path);

    /// <summary>
    /// Converts an absolute path inside the project into <c>res://</c> format.
    /// </summary>
    /// <param name="absolutePath">Absolute project path.</param>
    /// <returns>Project-relative <c>res://</c> path.</returns>
    string ToResPath(string absolutePath);

    /// <summary>
    /// Validates that an absolute path is within project boundaries.
    /// </summary>
    /// <param name="absolutePath">Absolute path to validate.</param>
    void EnsureInsideProject(string absolutePath);
}
