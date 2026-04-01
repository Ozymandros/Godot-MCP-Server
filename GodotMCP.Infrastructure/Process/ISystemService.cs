using System.IO;

namespace GodotMCP.Infrastructure.Process;

/// <summary>
/// Abstraction over environment and filesystem operations used by platform-specific
/// discovery logic. Allows deterministic unit testing of locator behavior.
/// </summary>
public interface ISystemService
{
    /// <summary>Get an environment variable value or null if not present.</summary>
    string? GetEnvironmentVariable(string name);

    /// <summary>Set an environment variable value (or null to remove).</summary>
    void SetEnvironmentVariable(string name, string? value);

    /// <summary>Return a special folder path (maps to Environment.GetFolderPath).</summary>
    string GetFolderPath(Environment.SpecialFolder folder);

    /// <summary>Return true when the file exists at the given path.</summary>
    bool FileExists(string path);

    /// <summary>Return true when the directory exists at the given path.</summary>
    bool DirectoryExists(string path);

    /// <summary>Enumerate directories with a search pattern and option.</summary>
    IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption);

    /// <summary>Enumerate files with a search pattern and option.</summary>
    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);

    /// <summary>Combine path segments into a single path.</summary>
    string Combine(params string[] parts);
}
