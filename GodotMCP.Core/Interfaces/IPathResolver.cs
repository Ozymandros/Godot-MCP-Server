namespace GodotMCP.Core.Interfaces;

public interface IPathResolver
{
    string ProjectRoot { get; }
    string ResolveResPath(string path);
    string ToResPath(string absolutePath);
    void EnsureInsideProject(string absolutePath);
}
