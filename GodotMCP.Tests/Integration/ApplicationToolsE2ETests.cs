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
            var tools = new GodotTools(
                files,
                resolver,
                new SceneSerializer(),
                new ResourceSerializer(),
                new ImportFileGenerator(),
                new ProjectConfigService(resolver),
                new GodotCliService(resolver),
                new IntegrationInspector(resolver));

            (await tools.CreateGodotProjectAsync("Demo")).Success.Should().BeTrue();
            (await tools.CreateSceneAsync("res://scenes/Main.tscn", "Main", "Node2D")).Success.Should().BeTrue();
            (await tools.AddNodeAsync("res://scenes/Main.tscn", ".", "Player", "Node2D")).Success.Should().BeTrue();
            (await tools.CreateScriptAsync("res://scripts/Player.gd", "gd", "Node2D", "Player")).Success.Should().BeTrue();
            (await tools.AttachScriptAsync("res://scenes/Main.tscn", "Player", "res://scripts/Player.gd")).Success.Should().BeTrue();
            tools.HealthCheck().Success.Should().BeTrue();
            tools.GetServerInfo().Success.Should().BeTrue();
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
            var tools = new GodotTools(
                files,
                resolver,
                new SceneSerializer(),
                new ResourceSerializer(),
                new ImportFileGenerator(),
                new ProjectConfigService(resolver),
                new GodotCliService(resolver),
                new IntegrationInspector(resolver));

            await tools.CreateGodotProjectAsync("Demo");
            var result = await tools.CreateSceneAsync("../outside.tscn", "Main", "Node2D");
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
