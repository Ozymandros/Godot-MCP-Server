namespace GodotMCP.Core.Interfaces;

/// <summary>
/// File operations within a Godot project. Paths are expected to be Godot
/// resource paths (res://...). Implementations must ensure operations stay
/// inside the configured project root.
/// </summary>
public interface IGodotFileService
{
    /// <summary>
    /// Returns true when a Godot project file (<c>project.godot</c>) exists.
    /// </summary>
    bool ProjectExists();

    /// <summary>
    /// Ensures the directory for the given resource path exists.
    /// </summary>
    void EnsureDirectory(string path);

    /// <summary>
    /// Returns true when the resource exists on disk.
    /// </summary>
    bool Exists(string path);

    /// <summary>
    /// Reads a text resource.
    /// </summary>
    Task<string> ReadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a text resource, creating parent directories when necessary.
    /// </summary>
    Task WriteAsync(string path, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a resource if present.
    /// </summary>
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enumerates files inside a resource directory.
    /// </summary>
    IEnumerable<string> EnumerateFiles(string directory, string searchPattern, bool recursive);
}
