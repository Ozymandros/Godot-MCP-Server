using System.Text.Json;
using GodotMCP.Infrastructure.Services;

namespace GodotMCP.Tests.Integration;

/// <summary>
/// Integration tests for Scene Graph MCP tool commands.
/// </summary>
public class SceneGraphToolsTests
{
    /// <summary>
    /// Verifies that the list command returns the full scene graph tree.
    /// </summary>
    [Fact]
    public async Task SceneListNodes_CommandShouldReturnFullTree()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            ISceneGraphService service = new SceneGraphService(files, new SceneSerializer(), resolver);

            var result = await GodotTools.SceneListNodesAsync(service, files, resolver, root, "scenes/Main.tscn");

            result.Success.Should().BeTrue();
            var nodes = (List<SceneGraphNodeDto>)result.Data!;
            nodes.Should().ContainSingle();
            nodes[0].Name.Should().Be("Root");
            nodes[0].Children.Should().Contain(x => x.Name == "Player");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies that the move command reparents a node and persists the change.
    /// </summary>
    [Fact]
    public async Task SceneMoveNode_CommandShouldReparentNode()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            ISceneGraphService service = new SceneGraphService(files, new SceneSerializer(), resolver);

            var result = await GodotTools.SceneMoveNodeAsync(
                service,
                files,
                resolver,
                root,
                "scenes/Main.tscn",
                "Player/CameraRig/MainCamera",
                "UI");

            result.Success.Should().BeTrue();
            var sceneText = await files.ReadAsync(Path.Combine(root, "scenes", "Main.tscn"));
            sceneText.Should().Contain("[node name=\"MainCamera\" type=\"Camera3D\" parent=\"UI\"]");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies that the add node command inserts a new node under the requested parent.
    /// </summary>
    [Fact]
    public async Task SceneAddNode_CommandShouldCreateNode()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            ISceneGraphService service = new SceneGraphService(files, new SceneSerializer(), resolver);

            var result = await GodotTools.SceneAddNodeAsync(
                service,
                files,
                resolver,
                root,
                "scenes/Main.tscn",
                "Player",
                "Node3D",
                "WeaponAnchor");

            result.Success.Should().BeTrue();
            var sceneText = await files.ReadAsync(Path.Combine(root, "scenes", "Main.tscn"));
            sceneText.Should().Contain("[node name=\"WeaponAnchor\" type=\"Node3D\" parent=\"Player\"]");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies that the remove command deletes a node subtree from the scene.
    /// </summary>
    [Fact]
    public async Task SceneRemoveNode_CommandShouldDeleteNodeSubtree()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            ISceneGraphService service = new SceneGraphService(files, new SceneSerializer(), resolver);

            var result = await GodotTools.SceneRemoveNodeAsync(
                service,
                files,
                resolver,
                root,
                "scenes/Main.tscn",
                "Player");

            result.Success.Should().BeTrue();
            var sceneText = await files.ReadAsync(Path.Combine(root, "scenes", "Main.tscn"));
            sceneText.Should().NotContain("[node name=\"Player\"");
            sceneText.Should().NotContain("[node name=\"CameraRig\"");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies that the rename command updates node name serialization.
    /// </summary>
    [Fact]
    public async Task SceneRenameNode_CommandShouldRenameNode()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            ISceneGraphService service = new SceneGraphService(files, new SceneSerializer(), resolver);

            var result = await GodotTools.SceneRenameNodeAsync(
                service,
                files,
                resolver,
                root,
                "scenes/Main.tscn",
                "Player/CameraRig",
                "Rig");

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
    /// Verifies that get node properties returns a dictionary payload for an existing node.
    /// </summary>
    [Fact]
    public async Task SceneGetNodeProperties_CommandShouldReturnDictionary()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            ISceneGraphService service = new SceneGraphService(files, new SceneSerializer(), resolver);

            var result = await GodotTools.SceneGetNodePropertiesAsync(
                service,
                files,
                resolver,
                root,
                "scenes/Main.tscn",
                "Player/CameraRig/MainCamera");

            result.Success.Should().BeTrue();
            var properties = (Dictionary<string, string>)result.Data!;
            properties.Should().ContainKey("fov");
            properties.Should().ContainKey("near");
            properties.Should().ContainKey("far");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies that set node properties accepts primitive JSON values and persists updates.
    /// </summary>
    [Fact]
    public async Task SceneSetNodeProperties_CommandShouldValidatePrimitiveValues()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            ISceneGraphService service = new SceneGraphService(files, new SceneSerializer(), resolver);

            using var payload = JsonDocument.Parse("""
{
  "fov": 68,
  "current": true,
  "label": "Main"
}
""");

            var properties = payload.RootElement
                .EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.Ordinal);

            var result = await GodotTools.SceneSetNodePropertiesAsync(
                service,
                files,
                resolver,
                root,
                "scenes/Main.tscn",
                "Player/CameraRig/MainCamera",
                properties);

            result.Success.Should().BeTrue();
            var sceneText = await files.ReadAsync(Path.Combine(root, "scenes", "Main.tscn"));
            sceneText.Should().Contain("fov = 68");
            sceneText.Should().Contain("current = true");
            sceneText.Should().Contain("label = Main");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies that get node properties returns a failed tool result when node path is missing.
    /// </summary>
    [Fact]
    public async Task SceneGetNodeProperties_CommandShouldReturnFailureForUnknownNode()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            ISceneGraphService service = new SceneGraphService(files, new SceneSerializer(), resolver);

            var result = await GodotTools.SceneGetNodePropertiesAsync(
                service,
                files,
                resolver,
                root,
                "scenes/Main.tscn",
                "MissingNode");

            result.Success.Should().BeFalse();
            result.Message.Should().Contain("not found");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies that object JSON values are rejected by the set node properties command.
    /// </summary>
    [Fact]
    public async Task SceneSetNodeProperties_CommandShouldRejectObjectValues()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            ISceneGraphService service = new SceneGraphService(files, new SceneSerializer(), resolver);

            using var payload = JsonDocument.Parse("""
{
  "invalid": { "nested": 1 }
}
""");

            var properties = payload.RootElement
                .EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.Ordinal);

            var result = await GodotTools.SceneSetNodePropertiesAsync(
                service,
                files,
                resolver,
                root,
                "scenes/Main.tscn",
                "Player/CameraRig/MainCamera",
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
    /// Verifies that scene mutations bootstrap a missing scene file under projectPath/scenes.
    /// </summary>
    [Fact]
    public async Task SceneAddNode_CommandShouldBootstrapMissingSceneUnderScenesDirectory()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            ISceneGraphService service = new SceneGraphService(files, new SceneSerializer(), resolver);

            var result = await GodotTools.SceneAddNodeAsync(
                service,
                files,
                resolver,
                root,
                "BootstrapMain.tscn",
                ".",
                "Node2D",
                "Player",
                "Node2D");

            result.Success.Should().BeTrue();
            var scenePath = Path.Combine(root, "scenes", "BootstrapMain.tscn");
            File.Exists(scenePath).Should().BeTrue();
            var sceneText = await files.ReadAsync(scenePath);
            sceneText.Should().Contain("[node name=\"Root\" type=\"Node2D\"]");
            sceneText.Should().Contain("[node name=\"Player\" type=\"Node2D\" parent=\".\"]");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies that scene tools reject file names without .tscn extension.
    /// </summary>
    [Fact]
    public async Task SceneAddNode_CommandShouldRejectNonTscnFileName()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            ISceneGraphService service = new SceneGraphService(files, new SceneSerializer(), resolver);

            var result = await GodotTools.SceneAddNodeAsync(
                service,
                files,
                resolver,
                root,
                "InvalidSceneName",
                ".",
                "Node2D",
                "Player");

            result.Success.Should().BeFalse();
            result.Message.Should().Contain(".tscn");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies legacy <c>add_node</c> rejects invalid parents like <c>scene.add_node</c>.
    /// </summary>
    [Fact]
    public async Task LegacyAddNode_ShouldRejectInvalidParent()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            ISceneGraphService service = new SceneGraphService(files, new SceneSerializer(), resolver);
            var scenes = new SceneSerializer();
            var result = await GodotTools.AddNodeAsync(service, files, resolver, scenes, root, "scenes/Main.tscn", "NotAParent", "X", "Node");

            result.Success.Should().BeFalse();
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }
}
