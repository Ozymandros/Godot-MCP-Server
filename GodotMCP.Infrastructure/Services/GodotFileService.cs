using System.IO;
using GodotMCP.Core.Interfaces;

namespace GodotMCP.Infrastructure.Services;

/// <summary>
/// Implements project-scoped filesystem operations using absolute or project-relative paths.
/// </summary>
/// <param name="pathResolver">Project path resolver.</param>
public sealed class GodotFileService(IPathResolver pathResolver) : IGodotFileService
{
    /// <inheritdoc />
    public bool ProjectExists() => File.Exists(Path.Combine(pathResolver.ProjectRoot, "project.godot"));

    private string ResolveAbsolutePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Path is required.");
        }

        var trimmed = path.Trim();
        if (Path.IsPathRooted(trimmed))
        {
            return Path.GetFullPath(trimmed);
        }

        return pathResolver.ResolvePath(trimmed);
    }

    /// <inheritdoc />
    public void EnsureDirectory(string path)
    {
        var absolute = ResolveAbsolutePath(path);
        Directory.CreateDirectory(absolute);
    }

    /// <inheritdoc />
    public bool Exists(string path) => File.Exists(ResolveAbsolutePath(path));

    /// <inheritdoc />
    public async Task<string> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        var absolute = ResolveAbsolutePath(path);
        return await File.ReadAllTextAsync(absolute, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task WriteAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        var absolute = ResolveAbsolutePath(path);
        var directory = Path.GetDirectoryName(absolute);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(absolute, content, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        var absolute = ResolveAbsolutePath(path);
        if (File.Exists(absolute))
        {
            File.Delete(absolute);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IEnumerable<string> EnumerateFiles(string directory, string searchPattern, bool recursive)
    {
        var absolute = ResolveAbsolutePath(directory);
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
