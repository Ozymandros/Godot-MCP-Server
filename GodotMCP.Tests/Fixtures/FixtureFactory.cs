using GodotMCP.Core.Interfaces;
using GodotMCP.Infrastructure.Config;
using GodotMCP.Infrastructure.Integrations;
using GodotMCP.Infrastructure.Process;
using GodotMCP.Infrastructure.Serialization;
using GodotMCP.Infrastructure.Services;

namespace GodotMCP.Tests.Fixtures;

internal static class FixtureFactory
{
    public static (string root, IPathResolver resolver, IGodotFileService files) CreateProject()
    {
        var root = Path.Combine(Path.GetTempPath(), "GodotMcpTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "project.godot"), "config_version=5");
        var resolver = new PathResolver(root);
        var files = new GodotFileService(resolver);
        return (root, resolver, files);
    }

    public static void Cleanup(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
