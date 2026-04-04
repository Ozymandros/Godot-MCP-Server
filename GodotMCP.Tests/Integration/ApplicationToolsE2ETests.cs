using FluentAssertions;
using GodotMCP.Application.Tools;
using GodotMCP.Core.Interfaces;
using GodotMCP.Infrastructure.Config;
using GodotMCP.Infrastructure.Integrations;
using GodotMCP.Infrastructure.Process;
using GodotMCP.Infrastructure.Serialization;
using GodotMCP.Infrastructure.Services;
using GodotMCP.Tests.Fixtures;

namespace GodotMCP.Tests.Integration;

public class ApplicationToolsE2ETests
{
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

            (await GodotTools.CreateGodotProjectAsync(files, "Demo")).Success.Should().BeTrue();
            (await GodotTools.CreateSceneAsync(files, resolver, scenes, "res://scenes/Main.tscn", "Main", "Node2D")).Success.Should().BeTrue();
            (await GodotTools.AddNodeAsync(files, resolver, scenes, "res://scenes/Main.tscn", ".", "Player", "Node2D")).Success.Should().BeTrue();
            (await GodotTools.CreateScriptAsync(files, resolver, "res://scripts/Player.gd", "gd", "Node2D", "Player")).Success.Should().BeTrue();
            (await GodotTools.AttachScriptAsync(files, resolver, scenes, "res://scenes/Main.tscn", "Player", "res://scripts/Player.gd")).Success.Should().BeTrue();
            GodotTools.HealthCheck().Success.Should().BeTrue();
            GodotTools.GetServerInfo(resolver).Success.Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

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

            await GodotTools.CreateGodotProjectAsync(files, "Demo");
            var result = await GodotTools.CreateSceneAsync(files, resolver, scenes, "../outside.tscn", "Main", "Node2D");
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
