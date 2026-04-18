using System.Text.Json;

namespace GodotMCP.Tests.Integration;

/// <summary>
/// Integration tests for UI MCP tool commands.
/// </summary>
public class UiToolsTests
{
    /// <summary>
    /// Verifies add control command creates a UI node in the scene.
    /// </summary>
    [Fact]
    public async Task UiAddControl_CommandShouldCreateControlNode()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            IUiService service = new UiService(new SceneGraphService(files, new SceneSerializer()));

            var result = await GodotTools.UiAddControlAsync(
                service,
                resolver,
                root,
                "scenes/Main.tscn",
                "UI",
                "Button",
                "PlayButton");

            result.Success.Should().BeTrue();
            var sceneText = await files.ReadAsync(Path.Combine(root, "scenes", "Main.tscn"));
            sceneText.Should().Contain("[node name=\"PlayButton\" type=\"Button\" parent=\"UI\"]");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies set layout preset command applies expected properties.
    /// </summary>
    [Fact]
    public async Task UiSetLayoutPreset_CommandShouldApplyAnchors()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            IUiService service = new UiService(new SceneGraphService(files, new SceneSerializer()));
            await GodotTools.UiAddControlAsync(service, resolver, root, "scenes/Main.tscn", "UI", "Control", "Hud");

            var result = await GodotTools.UiSetLayoutPresetAsync(
                service,
                resolver,
                root,
                "scenes/Main.tscn",
                "UI/Hud",
                "full_rect");

            result.Success.Should().BeTrue();
            var sceneText = await files.ReadAsync(Path.Combine(root, "scenes", "Main.tscn"));
            sceneText.Should().Contain("anchor_right = 1");
            sceneText.Should().Contain("anchor_bottom = 1");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies set control properties command validates primitive JSON values.
    /// </summary>
    [Fact]
    public async Task UiSetControlProperties_CommandShouldRejectObjectValue()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            IUiService service = new UiService(new SceneGraphService(files, new SceneSerializer()));
            await GodotTools.UiAddControlAsync(service, resolver, root, "scenes/Main.tscn", "UI", "Label", "Title");

            using var payload = JsonDocument.Parse("""
{
  "text": { "nested": 1 }
}
""");

            var properties = payload.RootElement
                .EnumerateObject()
                .ToDictionary(x => x.Name, x => x.Value.Clone(), StringComparer.Ordinal);

            var result = await GodotTools.UiSetControlPropertiesAsync(
                service,
                resolver,
                root,
                "scenes/Main.tscn",
                "UI/Title",
                properties);

            result.Success.Should().BeFalse();
            result.Message.Should().Contain("primitive JSON value");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies list controls returns UI nodes including added controls.
    /// </summary>
    [Fact]
    public async Task UiListControls_CommandShouldReturnUiNodes()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            IUiService service = new UiService(new SceneGraphService(files, new SceneSerializer()));
            await GodotTools.UiAddControlAsync(service, resolver, root, "scenes/Main.tscn", "UI", "Button", "PlayButton");

            var result = await GodotTools.UiListControlsAsync(service, resolver, root, "scenes/Main.tscn");

            result.Success.Should().BeTrue();
            var controls = (List<UiControlDto>)result.Data!;
            controls.Should().Contain(x => x.Name == "UI");
            controls.Should().Contain(x => x.Name == "PlayButton");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }
}
