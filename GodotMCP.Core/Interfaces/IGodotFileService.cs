namespace GodotMCP.Core.Interfaces;

/// <summary>
/// Provides project-scoped file system operations.
/// </summary>
public interface IGodotFileService
{
    /// <summary>
    /// Checks whether a Godot project exists at the configured root.
    /// </summary>
    /// <returns><see langword="true"/> when <c>project.godot</c> exists.</returns>
    bool ProjectExists();

    /// <summary>
    /// Ensures that a directory path exists.
    /// </summary>
    /// <param name="path">Directory path (absolute or project-relative).</param>
    void EnsureDirectory(string path);

    /// <summary>
    /// Checks whether a file exists.
    /// </summary>
    /// <param name="path">File path (absolute or project-relative).</param>
    /// <returns><see langword="true"/> when the file exists.</returns>
    bool Exists(string path);

    /// <summary>
    /// Reads a file as text.
    /// </summary>
    /// <param name="path">File path (absolute or project-relative).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>File content string.</returns>
    Task<string> ReadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes text content to a file.
    /// </summary>
    /// <param name="path">File path (absolute or project-relative).</param>
    /// <param name="content">Content to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WriteAsync(string path, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a file when it exists.
    /// </summary>
    /// <param name="path">File path (absolute or project-relative).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enumerates files in a directory.
    /// </summary>
    /// <param name="directory">Directory path (absolute or project-relative).</param>
    /// <param name="searchPattern">Search pattern.</param>
    /// <param name="recursive">Whether to recurse into subdirectories.</param>
    /// <returns>Absolute file paths matching the pattern.</returns>
    IEnumerable<string> EnumerateFiles(string directory, string searchPattern, bool recursive);
}
