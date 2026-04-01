using System.IO;

namespace GodotMCP.Infrastructure.Process;

internal sealed class DefaultSystemService : ISystemService
{
    public string? GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);
    public void SetEnvironmentVariable(string name, string? value) => Environment.SetEnvironmentVariable(name, value);
    public string GetFolderPath(Environment.SpecialFolder folder) => Environment.GetFolderPath(folder);
    public bool FileExists(string path) => File.Exists(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption) => Directory.EnumerateDirectories(path, searchPattern, searchOption);
    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) => Directory.EnumerateFiles(path, searchPattern, searchOption);
    public string Combine(params string[] parts) => Path.Combine(parts);
}
