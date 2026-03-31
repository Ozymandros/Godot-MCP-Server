using GodotMCP.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GodotMCP.Infrastructure.Services;

/// <summary>
/// File operations helper that understands Godot project resource paths
/// (res://) and performs safe read/write/delete operations inside the project.
/// </summary>
/// <remarks>
/// All paths passed to this service are interpreted as Godot resource paths
/// (for example, <c>res://scenes/Main.tscn</c>) and are resolved to absolute
/// paths within the configured project root. The service prevents escaping the
/// project root and ensures necessary directories exist when writing files.
/// </remarks>
public sealed class GodotFileService(IPathResolver pathResolver) : IGodotFileService
{
    /// <summary>
    /// Returns true when a Godot project file (<c>project.godot</c>) exists in the
    /// configured project root.
    /// </summary>
    public bool ProjectExists() => File.Exists(Path.Combine(pathResolver.ProjectRoot, "project.godot"));

    public void EnsureDirectory(string path)
    {
        var absolute = pathResolver.ResolveResPath(path);
        Directory.CreateDirectory(absolute);
    }

    public bool Exists(string path) => File.Exists(pathResolver.ResolveResPath(path));

    /// <summary>
    /// Reads the contents of a resource file at <paramref name="path"/>.
    /// </summary>
    /// <param name="path">Godot resource path (eg. <c>res://assets/sprite.png</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>File contents as a string.</returns>
    public async Task<string> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        var absolute = pathResolver.ResolveResPath(path);
        return await File.ReadAllTextAsync(absolute, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes <paramref name="content"/> to a resource path, creating directories
    /// as necessary.
    /// </summary>
    /// <param name="path">Godot resource path.</param>
    /// <param name="content">Content to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task WriteAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        var absolute = pathResolver.ResolveResPath(path);
        var directory = Path.GetDirectoryName(absolute);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(absolute, content, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the resource at <paramref name="path"/> if it exists.
    /// </summary>
    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        var absolute = pathResolver.ResolveResPath(path);
        if (File.Exists(absolute))
        {
            File.Delete(absolute);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Enumerates files inside a resource directory using the given <paramref name="searchPattern"/>.
    /// </summary>
    /// <param name="directory">Resource directory path (eg. <c>res://</c> or <c>res://assets</c>).</param>
    /// <param name="searchPattern">Search pattern (eg. <c>*.tscn</c>).</param>
    /// <param name="recursive">Whether to search subdirectories.</param>
    /// <returns>Enumerable of absolute file paths inside the project.</returns>
    public IEnumerable<string> EnumerateFiles(string directory, string searchPattern, bool recursive)
    {
        var absolute = pathResolver.ResolveResPath(directory);
        if (!Directory.Exists(absolute))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(
            absolute,
            searchPattern,
            recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
    }
}
