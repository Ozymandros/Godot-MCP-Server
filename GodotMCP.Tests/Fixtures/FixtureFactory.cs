using GodotMCP.Core.Interfaces;
using GodotMCP.Infrastructure.Services;
using GodotMCP.Tests.TestIsolation;

namespace GodotMCP.Tests.Fixtures;

internal static class FixtureFactory
{
    public static (string root, IPathResolver resolver, IGodotFileService files) CreateProject()
    {
        var root = AssemblyStartup.CreateSandboxDirectory("project");
        File.WriteAllText(Path.Combine(root, "project.godot"), "config_version=5");
        var resolver = new PathResolver(root);
        var files = new GodotFileService(resolver);
        return (root, resolver, files);
    }
}
