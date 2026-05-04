using System.Text.Json;
using GodotMCP.Infrastructure.Services;

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
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            ILightingService service = new LightingService(files, resolver, new SceneGraphService(files, new SceneSerializer(), resolver));

            var result = await GodotTools.LightCreateAsync(
                service,
                resolver,
                root,
                "scenes/Main.tscn",
                ".",
                "DirectionalLight3D",
                "Sun",
                "sun");

            result.Success.Should().BeTrue();
            var sceneText = await files.ReadAsync(Path.Combine(root, "scenes", "Main.tscn"));
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
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            ILightingService service = new LightingService(files, resolver, new SceneGraphService(files, new SceneSerializer(), resolver));
            await GodotTools.LightCreateAsync(service, resolver, root, "scenes/Main.tscn", ".", "OmniLight3D", "Lamp");

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
                root,
                "scenes/Main.tscn",
                "Lamp",
                properties);

            result.Success.Should().BeTrue();
            var sceneText = await files.ReadAsync(Path.Combine(root, "scenes", "Main.tscn"));
            sceneText.Should().Contain("light_energy = 3.2");
            sceneText.Should().Contain("shadow_enabled = true");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies that when rawContent is supplied the light.update tool writes the provided text verbatim.
    /// </summary>
    [Fact]
    public async Task LightUpdate_WithRawContent_ReplacesSceneFile()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            ILightingService service = new LightingService(files, resolver, new SceneGraphService(files, new SceneSerializer(), resolver));

            var newContent = "REPLACED LIGHT SCENE";
            var result = await GodotTools.LightUpdateAsync(service, resolver, root, "scenes/Main.tscn", ".", properties: null, rawContent: newContent, fileService: files);

            result.Success.Should().BeTrue();
            var sceneText = await files.ReadAsync(Path.Combine(root, "scenes", "Main.tscn"));
            sceneText.Should().Be(newContent);
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
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            ILightingService service = new LightingService(files, resolver, new SceneGraphService(files, new SceneSerializer(), resolver));
            await GodotTools.LightCreateAsync(service, resolver, root, "scenes/Main.tscn", ".", "OmniLight3D", "Lamp");

            using var payload = JsonDocument.Parse("""
{
  "light_energy": 0
}
""");
            var properties = payload.RootElement
                .EnumerateObject()
                .ToDictionary(x => x.Name, x => x.Value.Clone(), StringComparer.Ordinal);
            await GodotTools.LightUpdateAsync(service, resolver, root, "scenes/Main.tscn", "Lamp", properties);

            var result = await GodotTools.LightValidateAsync(service, resolver, root);

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
