using GodotMCP.Core.Interfaces;
using GodotMCP.Infrastructure.Config;
using GodotMCP.Infrastructure.Integrations;
using GodotMCP.Infrastructure.Process;
using GodotMCP.Infrastructure.Serialization;
using GodotMCP.Infrastructure.Services;

namespace GodotMCP.Tests.Fixtures;

/// <summary>
/// Provides helpers for creating and cleaning temporary Godot test fixtures.
/// </summary>
internal static class FixtureFactory
{
    /// <summary>
    /// Creates a temporary project root with a minimal <c>project.godot</c> file.
    /// </summary>
    /// <returns>Tuple with project root, path resolver, and file service instances.</returns>
    public static (string root, IPathResolver resolver, IGodotFileService files) CreateProject()
    {
        var root = Path.Combine(Path.GetTempPath(), "GodotMcpTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "project.godot"), "config_version=5");
        var resolver = new PathResolver(root);
        var files = new GodotFileService(resolver);
        return (root, resolver, files);
    }

    /// <summary>
    /// Deletes a temporary fixture project folder when present.
    /// </summary>
    /// <param name="root">Root folder to remove.</param>
    public static void Cleanup(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>
    /// Resolves the full path to a named scene fixture file.
    /// </summary>
    /// <param name="fixtureName">Fixture file name to resolve.</param>
    /// <returns>Absolute path to the fixture file.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the fixture file does not exist.</exception>
    public static string GetSceneFixturePath(string fixtureName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Scenes", fixtureName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Fixture scene '{fixtureName}' not found at '{path}'.");
        }

        return path;
    }

    /// <summary>
    /// Copies a scene fixture into a temporary project at a target <c>res://</c> path.
    /// </summary>
    /// <param name="destinationRoot">Temporary project root directory.</param>
    /// <param name="fixtureName">Fixture file name to copy.</param>
    /// <param name="destinationResPath">Destination <c>res://</c> scene path.</param>
    /// <returns>A task that completes when copy operation finishes.</returns>
    public static async Task CopySceneFixtureAsync(string destinationRoot, string fixtureName, string destinationResPath)
    {
        var source = GetSceneFixturePath(fixtureName);
        var relative = destinationResPath.Replace("res://", string.Empty, StringComparison.Ordinal);
        var destination = Path.Combine(destinationRoot, relative.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var content = await File.ReadAllTextAsync(source).ConfigureAwait(false);
        await File.WriteAllTextAsync(destination, content).ConfigureAwait(false);
    }
}
