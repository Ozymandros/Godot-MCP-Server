using GodotMCP.Core.Interfaces;

namespace GodotMCP.Infrastructure.Services;

/// <summary>
/// Implements project-scoped filesystem operations over <c>res://</c> paths.
/// </summary>
/// <param name="pathResolver">Project path resolver.</param>
public sealed class GodotFileService(IPathResolver pathResolver) : IGodotFileService
{
    /// <inheritdoc />
    public bool ProjectExists() => File.Exists(Path.Combine(pathResolver.ProjectRoot, "project.godot"));

    /// <inheritdoc />
    public void EnsureDirectory(string path)
    {
        var absolute = pathResolver.ResolveResPath(path);
        Directory.CreateDirectory(absolute);
    }

    /// <inheritdoc />
    public bool Exists(string path) => File.Exists(pathResolver.ResolveResPath(path));

    /// <inheritdoc />
    public async Task<string> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        var absolute = pathResolver.ResolveResPath(path);
        return await File.ReadAllTextAsync(absolute, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        var absolute = pathResolver.ResolveResPath(path);
        if (File.Exists(absolute))
        {
            File.Delete(absolute);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IEnumerable<string> EnumerateFiles(string directory, string searchPattern, bool recursive)
    {
        var absolute = pathResolver.ResolveResPath(directory);
        if (!Directory.Exists(absolute))
        {
            return [];
        }

        return Directory.EnumerateFiles(
            absolute,
            searchPattern,
            recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
    }
}
