namespace GodotMCP.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="SceneGraphService"/> behavior.
/// </summary>
public class SceneGraphServiceTests
{
    /// <summary>
    /// Verifies that listing nodes returns the expected hierarchy and script metadata.
    /// </summary>
    [Fact]
    public async Task ListNodesAsync_ShouldReturnHierarchyWithChildrenAndScript()
    {
        var (root, _, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            var service = new SceneGraphService(files, new SceneSerializer());

            var result = await service.ListNodesAsync(Path.Combine(root, "scenes", "Main.tscn"));

            result.Should().ContainSingle();
            var rootNode = result.Single();
            rootNode.Name.Should().Be("Root");
            rootNode.Children.Should().Contain(x => x.Name == "Player");
            rootNode.Children.Should().Contain(x => x.Name == "UI");

            var player = rootNode.Children.Single(x => x.Name == "Player");
            player.Script.Should().Be("ExtResource(\"1\")");
            player.NodePath.Should().Be("Player");
            player.Parent.Should().Be("Root");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies that adding a node with root parent marker inserts under the scene root.
    /// </summary>
    [Fact]
    public async Task AddNodeAsync_ShouldInsertUnderRootWhenParentIsDot()
    {
        var (root, _, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            var service = new SceneGraphService(files, new SceneSerializer());

            var result = await service.AddNodeAsync(new SceneGraphAddNodeRequest(
                Path.Combine(root, "scenes", "Main.tscn"),
                ".",
                "Node3D",
                "Props"));

            result.Success.Should().BeTrue();
            var sceneText = await files.ReadAsync(Path.Combine(root, "scenes", "Main.tscn"));
            sceneText.Should().Contain("[node name=\"Props\" type=\"Node3D\" parent=\".\"]");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies that add node rejects non-existing parent paths.
    /// </summary>
    [Fact]
    public async Task AddNodeAsync_ShouldRejectUnknownParentPath()
    {
        var (root, _, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            var service = new SceneGraphService(files, new SceneSerializer());

            var result = await service.AddNodeAsync(new SceneGraphAddNodeRequest(
                Path.Combine(root, "scenes", "Main.tscn"),
                "MissingParent",
                "Node3D",
                "Props"));

            result.Success.Should().BeFalse();
            result.Message.Should().Contain("not found");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies that removing a node removes its full descendant subtree.
    /// </summary>
    [Fact]
    public async Task RemoveNodeAsync_ShouldDeleteSubtree()
    {
        var (root, _, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            var service = new SceneGraphService(files, new SceneSerializer());

            var result = await service.RemoveNodeAsync(new SceneGraphRemoveNodeRequest(Path.Combine(root, "scenes", "Main.tscn"), "Player"));

            result.Success.Should().BeTrue();
            var sceneText = await files.ReadAsync(Path.Combine(root, "scenes", "Main.tscn"));
            sceneText.Should().NotContain("[node name=\"Player\"");
            sceneText.Should().NotContain("[node name=\"CameraRig\"");
            sceneText.Should().NotContain("[node name=\"MainCamera\"");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies that move node rejects circular parenting operations.
    /// </summary>
    [Fact]
    public async Task MoveNodeAsync_ShouldRejectCircularParenting()
    {
        var (root, _, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            var service = new SceneGraphService(files, new SceneSerializer());

            var result = await service.MoveNodeAsync(new SceneGraphMoveNodeRequest(
                Path.Combine(root, "scenes", "Main.tscn"),
                "Player",
                "Player/CameraRig"));

            result.Success.Should().BeFalse();
            result.Message.Should().Contain("descendants");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies that the root node cannot be moved under another node.
    /// </summary>
    [Fact]
    public async Task MoveNodeAsync_ShouldRejectMovingRootNode()
    {
        var (root, _, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            var service = new SceneGraphService(files, new SceneSerializer());

            var result = await service.MoveNodeAsync(new SceneGraphMoveNodeRequest(
                Path.Combine(root, "scenes", "Main.tscn"),
                "Root",
                "Player"));

            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Root node cannot be moved");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies that renaming a node updates the serialized scene output.
    /// </summary>
    [Fact]
    public async Task RenameNodeAsync_ShouldRenameNode()
    {
        var (root, _, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            var service = new SceneGraphService(files, new SceneSerializer());

            var result = await service.RenameNodeAsync(new SceneGraphRenameNodeRequest(
                Path.Combine(root, "scenes", "Main.tscn"),
                "Player/CameraRig",
                "Rig"));

            result.Success.Should().BeTrue();
            var sceneText = await files.ReadAsync(Path.Combine(root, "scenes", "Main.tscn"));
            sceneText.Should().Contain("[node name=\"Rig\" type=\"Node3D\" parent=\"Player\"]");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies that setting properties updates only provided keys and preserves existing ones.
    /// </summary>
    [Fact]
    public async Task SetNodePropertiesAsync_ShouldUpdateOnlyProvidedProperties()
    {
        var (root, _, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            var service = new SceneGraphService(files, new SceneSerializer());

            var result = await service.SetNodePropertiesAsync(new SceneGraphSetPropertiesRequest(
                Path.Combine(root, "scenes", "Main.tscn"),
                "Player/CameraRig/MainCamera",
                new Dictionary<string, object?>
                {
                    ["fov"] = 60,
                    ["current"] = true
                }));

            result.Success.Should().BeTrue();
            var props = await service.GetNodePropertiesAsync(Path.Combine(root, "scenes", "Main.tscn"), "Player/CameraRig/MainCamera");
            props["fov"].Should().Be("60");
            props["current"].Should().Be("true");
            props["near"].Should().Be("0.1");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies that requesting properties for a missing node raises an invalid operation error.
    /// </summary>
    [Fact]
    public async Task GetNodePropertiesAsync_ShouldThrowWhenNodeDoesNotExist()
    {
        var (root, _, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            var service = new SceneGraphService(files, new SceneSerializer());

            var act = async () => await service.GetNodePropertiesAsync(Path.Combine(root, "scenes", "Main.tscn"), "MissingNode");

            await act.Should().ThrowAsync<InvalidOperationException>();
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies that non-primitive values are rejected by property update validation.
    /// </summary>
    [Fact]
    public async Task SetNodePropertiesAsync_ShouldRejectNonPrimitiveValues()
    {
        var (root, _, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            var service = new SceneGraphService(files, new SceneSerializer());

            var result = await service.SetNodePropertiesAsync(new SceneGraphSetPropertiesRequest(
                Path.Combine(root, "scenes", "Main.tscn"),
                "Player/CameraRig/MainCamera",
                new Dictionary<string, object?>
                {
                    ["invalid"] = new object()
                }));

            result.Success.Should().BeFalse();
            result.Message.Should().Contain("primitive value");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }
}
