namespace GodotMCP.Core.Interfaces;

public interface IGodotFileService
{
    bool ProjectExists();
    void EnsureDirectory(string path);
    bool Exists(string path);
    Task<string> ReadAsync(string path, CancellationToken cancellationToken = default);
    Task WriteAsync(string path, string content, CancellationToken cancellationToken = default);
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);
    IEnumerable<string> EnumerateFiles(string directory, string searchPattern, bool recursive);
}
