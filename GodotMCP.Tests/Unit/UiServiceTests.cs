namespace GodotMCP.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="UiService"/>.
/// </summary>
public class UiServiceTests
{
    /// <summary>
    /// Verifies that control listing returns only UI node types.
    /// </summary>
    [Fact]
    public async Task ListControlsAsync_ShouldReturnOnlyUiNodes()
    {
        var (root, _, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            var graph = new SceneGraphService(files, new SceneSerializer());
            await graph.AddNodeAsync(new SceneGraphAddNodeRequest(Path.Combine(root, "scenes", "Main.tscn"), "UI", "Button", "PlayButton"));
            await graph.AddNodeAsync(new SceneGraphAddNodeRequest(Path.Combine(root, "scenes", "Main.tscn"), "Player", "Node3D", "NonUi"));
            var service = new UiService(graph);

            var controls = await service.ListControlsAsync(Path.Combine(root, "scenes", "Main.tscn"));

            controls.Should().Contain(x => x.Name == "UI");
            controls.Should().Contain(x => x.Name == "PlayButton");
            controls.Should().NotContain(x => x.Name == "NonUi");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies add control rejects non-UI control types.
    /// </summary>
    [Fact]
    public async Task AddControlAsync_ShouldRejectNonUiType()
    {
        var (root, _, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            var service = new UiService(new SceneGraphService(files, new SceneSerializer()));

            var result = await service.AddControlAsync(new UiAddControlRequest(
                Path.Combine(root, "scenes", "Main.tscn"),
                "UI",
                "Node3D",
                "NotUi"));

            result.Success.Should().BeFalse();
            result.Message.Should().Contain("not a recognized UI control type");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies layout preset applies expected anchor values.
    /// </summary>
    [Fact]
    public async Task SetLayoutPresetAsync_ShouldApplyFullRect()
    {
        var (root, _, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            var graph = new SceneGraphService(files, new SceneSerializer());
            await graph.AddNodeAsync(new SceneGraphAddNodeRequest(Path.Combine(root, "scenes", "Main.tscn"), "UI", "Control", "Hud"));
            var service = new UiService(graph);

            var result = await service.SetLayoutPresetAsync(new UiSetLayoutRequest(
                Path.Combine(root, "scenes", "Main.tscn"),
                "UI/Hud",
                "full_rect"));

            result.Success.Should().BeTrue();
            result.Control!.Properties["anchor_right"].Should().Be("1");
            result.Control.Properties["anchor_bottom"].Should().Be("1");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies unknown layout preset returns failure.
    /// </summary>
    [Fact]
    public async Task SetLayoutPresetAsync_ShouldRejectUnknownPreset()
    {
        var (root, _, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            var service = new UiService(new SceneGraphService(files, new SceneSerializer()));

            var result = await service.SetLayoutPresetAsync(new UiSetLayoutRequest(
                Path.Combine(root, "scenes", "Main.tscn"),
                "UI",
                "diagonal"));

            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Unsupported layout preset");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }
}
