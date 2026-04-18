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
            var resources = new ResourceSerializer();
            var imports = new ImportFileGenerator();
            var config = new ProjectConfigService(resolver);
            var cli = new GodotCliService(resolver);
            var inspector = new IntegrationInspector(resolver);

            (await GodotTools.CreateGodotProjectAsync(files, resolver, root, "Demo")).Success.Should().BeTrue();
            (await GodotTools.CreateSceneAsync(files, resolver, scenes, root, "scenes/Main.tscn", "Main", "Node2D")).Success.Should().BeTrue();
            (await GodotTools.AddNodeAsync(files, resolver, scenes, root, "scenes/Main.tscn", ".", "Player", "Node2D")).Success.Should().BeTrue();
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
}
