using GodotMCP.Infrastructure.Services;

namespace GodotMCP.Tests.Integration;

/// <summary>
/// End-to-end integration tests for core application tool flows.
/// </summary>
public class ApplicationToolsE2ETests
{
    /// <summary>
    /// Verifies a happy-path flow from project creation to scene and script wiring.
    /// </summary>
    [Fact]
    public async Task CreateProjectAndSceneFlow_ShouldSucceed()
    {
        var root = Path.Combine(Path.GetTempPath(), "GodotMcpE2E", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            IPathResolver resolver = new PathResolver(root);
            IGodotFileService files = new GodotFileService(resolver);
            var scenes = new SceneSerializer();
            var graph = new SceneGraphService(files, scenes, resolver);
            var resources = new ResourceSerializer();
            var imports = new ImportFileGenerator();
            var config = new ProjectConfigService(resolver);
            var cli = new GodotCliService(resolver);
            var inspector = new IntegrationInspector(resolver);

            (await GodotTools.CreateGodotProjectAsync(files, resolver, root, "Demo")).Success.Should().BeTrue();
            (await GodotTools.CreateSceneAsync(files, resolver, scenes, root, "scenes/Main.tscn", "Main", "Node2D")).Success.Should().BeTrue();
            (await GodotTools.AddNodeAsync(graph, files, resolver, scenes, root, "scenes/Main.tscn", ".", "Player", "Node2D")).Success.Should().BeTrue();
            (await GodotTools.CreateScriptAsync(files, resolver, root, "scripts/Player.gd", "gd", "Node2D", "Player")).Success.Should().BeTrue();
            (await GodotTools.AttachScriptAsync(files, resolver, scenes, root, "scenes/Main.tscn", "Player", "scripts/Player.gd")).Success.Should().BeTrue();
            GodotTools.HealthCheck(root).Success.Should().BeTrue();
            GodotTools.GetServerInfo(resolver, root).Success.Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    /// <summary>
    /// Verifies invalid scene paths fail without crashing tool execution.
    /// </summary>
    [Fact]
    public async Task InvalidPath_ShouldFailGracefully()
    {
        var root = Path.Combine(Path.GetTempPath(), "GodotMcpE2E", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            IPathResolver resolver = new PathResolver(root);
            IGodotFileService files = new GodotFileService(resolver);
            var scenes = new SceneSerializer();

            await GodotTools.CreateGodotProjectAsync(files, resolver, root, "Demo");
            var result = await GodotTools.CreateSceneAsync(files, resolver, scenes, root, "../outside.tscn", "Main", "Node2D");
            result.Success.Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    /// <summary>
    /// Verifies packed scene instantiation writes <c>res://</c> paths in <c>ext_resource</c>, not OS paths.
    /// </summary>
    [Fact]
    public async Task InstantiatePackedScene_ShouldWriteResPathForExtResource()
    {
        var root = Path.Combine(Path.GetTempPath(), "GodotMcpE2E", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            IPathResolver resolver = new PathResolver(root);
            IGodotFileService files = new GodotFileService(resolver);
            var scenes = new SceneSerializer();
            var graph = new SceneGraphService(files, scenes, resolver);

            await GodotTools.CreateGodotProjectAsync(files, resolver, root, "Demo");
            (await GodotTools.CreateSceneAsync(files, resolver, scenes, root, "scenes/Packed.tscn", "Root", "Node2D")).Success.Should().BeTrue();
            (await GodotTools.CreateSceneAsync(files, resolver, scenes, root, "scenes/Container.tscn", "World", "Node2D")).Success.Should().BeTrue();

            var result = await GodotTools.InstantiatePackedSceneAsync(
                graph,
                files,
                resolver,
                scenes,
                root,
                "scenes/Container.tscn",
                ".",
                "scenes/Packed.tscn",
                "Instance");

            result.Success.Should().BeTrue();
            var text = await files.ReadAsync(Path.Combine(root, "scenes", "Container.tscn"));
            text.Should().Contain("path=\"res://scenes/Packed.tscn\"");
            text.Should().NotContain($"path=\"{root.Replace('\\', '/')}");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
