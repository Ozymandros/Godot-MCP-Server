using System.IO;

namespace GodotMCP.Infrastructure.Process;

/// <summary>
/// Abstraction over environment and filesystem operations used by platform-specific
/// discovery logic. Allows deterministic unit testing of locator behavior.
/// </summary>
public interface ISystemService
{
    string? GetEnvironmentVariable(string name);
    void SetEnvironmentVariable(string name, string? value);
    string GetFolderPath(Environment.SpecialFolder folder);
    bool FileExists(string path);
    bool DirectoryExists(string path);
    IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption);
    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);
    string Combine(params string[] parts);
}
