using GodotMCP.Core.Interfaces;

namespace GodotMCP.Infrastructure.Services;

public sealed class GodotFileService(IPathResolver pathResolver) : IGodotFileService
{
    public bool ProjectExists() => File.Exists(Path.Combine(pathResolver.ProjectRoot, "project.godot"));

    public void EnsureDirectory(string path)
    {
        var absolute = pathResolver.ResolveResPath(path);
        Directory.CreateDirectory(absolute);
    }

    public bool Exists(string path) => File.Exists(pathResolver.ResolveResPath(path));

    public async Task<string> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        var absolute = pathResolver.ResolveResPath(path);
        return await File.ReadAllTextAsync(absolute, cancellationToken).ConfigureAwait(false);
    }

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

    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        var absolute = pathResolver.ResolveResPath(path);
        if (File.Exists(absolute))
        {
            File.Delete(absolute);
        }

        return Task.CompletedTask;
    }

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
