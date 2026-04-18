using System.Text.Json;

namespace GodotMCP.Tests.Integration;

/// <summary>
/// Integration tests for lighting MCP tool commands.
/// </summary>
public class LightingToolsTests
{
    /// <summary>
    /// Verifies light.create creates a light node in the scene.
    /// </summary>
    [Fact]
    public async Task LightCreate_CommandShouldCreateLightNode()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "res://scenes/Main.tscn");
            ILightingService service = new LightingService(files, resolver, new SceneGraphService(files, new SceneSerializer()));

            var result = await GodotTools.LightCreateAsync(
                service,
                resolver,
                "res://",
                "scenes/Main.tscn",
                ".",
                "DirectionalLight3D",
                "Sun",
                "sun");

            result.Success.Should().BeTrue();
            var sceneText = await files.ReadAsync("res://scenes/Main.tscn");
            sceneText.Should().Contain("[node name=\"Sun\" type=\"DirectionalLight3D\" parent=\".\"]");
            sceneText.Should().Contain("light_energy = 2.5");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies light.update updates selected properties.
    /// </summary>
    [Fact]
    public async Task LightUpdate_CommandShouldUpdateProperties()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "res://scenes/Main.tscn");
            ILightingService service = new LightingService(files, resolver, new SceneGraphService(files, new SceneSerializer()));
            await GodotTools.LightCreateAsync(service, resolver, "res://", "scenes/Main.tscn", ".", "OmniLight3D", "Lamp");

            using var payload = JsonDocument.Parse("""
{
  "light_energy": 3.2,
  "shadow_enabled": true
}
""");

            var properties = payload.RootElement
                .EnumerateObject()
                .ToDictionary(x => x.Name, x => x.Value.Clone(), StringComparer.Ordinal);

            var result = await GodotTools.LightUpdateAsync(
                service,
                resolver,
                "res://",
                "scenes/Main.tscn",
                "Lamp",
                properties);

            result.Success.Should().BeTrue();
            var sceneText = await files.ReadAsync("res://scenes/Main.tscn");
            sceneText.Should().Contain("light_energy = 3.2");
            sceneText.Should().Contain("shadow_enabled = true");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies light.validate reports expected issues for invalid configurations.
    /// </summary>
    [Fact]
    public async Task LightValidate_CommandShouldReportIssues()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "res://scenes/Main.tscn");
            ILightingService service = new LightingService(files, resolver, new SceneGraphService(files, new SceneSerializer()));
            await GodotTools.LightCreateAsync(service, resolver, "res://", "scenes/Main.tscn", ".", "OmniLight3D", "Lamp");

            using var payload = JsonDocument.Parse("""
{
  "light_energy": 0
}
""");
            var properties = payload.RootElement
                .EnumerateObject()
                .ToDictionary(x => x.Name, x => x.Value.Clone(), StringComparer.Ordinal);
            await GodotTools.LightUpdateAsync(service, resolver, "res://", "scenes/Main.tscn", "Lamp", properties);

            var result = await GodotTools.LightValidateAsync(service, resolver, "res://");

            result.Success.Should().BeTrue();
            var issues = (List<LightValidationIssueDto>)result.Data!;
            issues.Should().Contain(x => x.Rule == "non-positive-energy");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }
}
