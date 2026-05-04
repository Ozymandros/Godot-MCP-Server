using GodotMCP.Core.SceneGraph;
using GodotMCP.Infrastructure.Serialization;
using GodotMCP.Infrastructure.Services;

namespace GodotMCP.Tests.Integration;

/// <summary>
/// Integration tests for animation, diff, and lint tool flows.
/// </summary>
public class AnimationToolsTests
{
    /// <summary>
    /// Verifies that animation player, animation resource, and animation track creation succeed.
    /// </summary>
    [Fact]
    public async Task AddAnimation_ShouldSucceed()
    {
        var root = Path.Combine(Path.GetTempPath(), "GodotMcpAnim", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            IPathResolver resolver = new PathResolver(root);
            IGodotFileService files = new GodotFileService(resolver);
            var scenes = new SceneSerializer();
            var graph = new SceneGraphService(files, scenes, resolver);

            await GodotTools.CreateGodotProjectAsync(files, resolver, root, "AnimDemo");
            await GodotTools.CreateSceneAsync(files, resolver, scenes, root, "scenes/Main.tscn", "Main", "Node2D");
            await GodotTools.AddNodeAsync(graph, files, resolver, scenes, root, "scenes/Main.tscn", ".", "Sprite2D", "Sprite2D");

            (await GodotTools.AddAnimationPlayerAsync(graph, files, resolver, scenes, root, "scenes/Main.tscn", ".", "AnimationPlayer", "Node2D")).Success.Should().BeTrue();
            (await GodotTools.AddAnimationAsync(files, resolver, scenes, root, "scenes/Main.tscn", "AnimationPlayer", "fade", 1.0f, false, "Node2D")).Success.Should().BeTrue();

            var keys = new List<KeyPoint>
            {
                new() { Time = 0, Value = "Vector2(0, 0)" },
                new() { Time = 1, Value = "Vector2(100, 100)" }
            };

            (await GodotTools.AddAnimationTrackAsync(files, resolver, scenes, root, "scenes/Main.tscn", "fade", "Sprite2D:position", "value", keys, "Node2D")).Success.Should().BeTrue();

            var sceneText = await files.ReadAsync(Path.Combine(root, "scenes", "Main.tscn"));
            sceneText.Should().Contain("[sub_resource type=\"AnimationLibrary\" id=\"AnimationLibrary_default\"]");
            sceneText.Should().Contain("resource_name = \"fade\"");
            sceneText.Should().Contain("tracks/0/path = NodePath(\"Sprite2D:position\")");
            sceneText.Should().Contain("PackedFloat32Array(0.0, 1.0)");
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
    /// Verifies that scene diff detects added nodes between two scenes.
    /// </summary>
    [Fact]
    public async Task DiffScenes_ShouldDetectChanges()
    {
        var root = Path.Combine(Path.GetTempPath(), "GodotMcpDiff", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            IPathResolver resolver = new PathResolver(root);
            IGodotFileService files = new GodotFileService(resolver);
            var scenes = new SceneSerializer();

            await GodotTools.CreateGodotProjectAsync(files, resolver, root, "DiffDemo");
            await GodotTools.CreateSceneAsync(files, resolver, scenes, root, "scenes/A.tscn", "Root", "Node2D");
            await GodotTools.CreateSceneAsync(files, resolver, scenes, root, "scenes/B.tscn", "Root", "Node2D");
            var graph = new SceneGraphService(files, scenes, resolver);
            await GodotTools.AddNodeAsync(graph, files, resolver, scenes, root, "scenes/B.tscn", ".", "NewNode", "Sprite2D");

            var result = await GodotTools.DiffScenesAsync(files, resolver, scenes, root, "scenes/A.tscn", "scenes/B.tscn");
            result.Success.Should().BeTrue();
            var data = (SceneDiffModel)result.Data!;
            data.AddedNodes.Should().Contain(n => n.Name == "NewNode");
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
    /// Verifies <c>add_animation</c> targets the AnimationPlayer at the given node path when names collide.
    /// </summary>
    [Fact]
    public async Task AddAnimation_ShouldTargetPlayerByPathWhenDuplicateNames()
    {
        var root = Path.Combine(Path.GetTempPath(), "GodotMcpAnimPath", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            IPathResolver resolver = new PathResolver(root);
            IGodotFileService files = new GodotFileService(resolver);
            var scenes = new SceneSerializer();
            var graph = new SceneGraphService(files, scenes, resolver);

            await GodotTools.CreateGodotProjectAsync(files, resolver, root, "PathDemo");
            await GodotTools.CreateSceneAsync(files, resolver, scenes, root, "scenes/Main.tscn", "Main", "Node2D");
            await GodotTools.AddNodeAsync(graph, files, resolver, scenes, root, "scenes/Main.tscn", ".", "Player", "Node2D");
            await GodotTools.AddNodeAsync(graph, files, resolver, scenes, root, "scenes/Main.tscn", ".", "UI", "Control");
            (await GodotTools.AddAnimationPlayerAsync(graph, files, resolver, scenes, root, "scenes/Main.tscn", "Player", "Anim", "Node2D")).Success.Should().BeTrue();
            (await GodotTools.AddAnimationPlayerAsync(graph, files, resolver, scenes, root, "scenes/Main.tscn", "UI", "Anim", "Node2D")).Success.Should().BeTrue();

            (await GodotTools.AddAnimationAsync(files, resolver, scenes, root, "scenes/Main.tscn", "UI/Anim", "only_ui", 0.5f, false, "Node2D")).Success.Should().BeTrue();

            var sceneText = await files.ReadAsync(Path.Combine(root, "scenes", "Main.tscn"));
            sceneText.Should().Contain("[node name=\"Anim\" type=\"AnimationPlayer\" parent=\"UI\"]");
            sceneText.Split("resource_name = \"only_ui\"", StringSplitOptions.None).Should().HaveCount(2);
            var sceneModel = scenes.Deserialize(sceneText);
            var index = SceneNodePathIndex.Build(sceneModel);
            SceneNodePathIndex.TryGetNode(index, "UI/Anim", out var uiPlayer).Should().BeTrue();
            uiPlayer!.Properties.Should().ContainKey("libraries");
            SceneNodePathIndex.TryGetNode(index, "Player/Anim", out var playerPlayer).Should().BeTrue();
            playerPlayer!.Properties.Should().NotContainKey("libraries");
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
    /// Verifies that project linting reports missing import sidecars.
    /// </summary>
    [Fact]
    public async Task LintProject_ShouldIdentifyMissingImports()
    {
        var root = Path.Combine(Path.GetTempPath(), "GodotMcpLint", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            IPathResolver resolver = new PathResolver(root);
            IGodotFileService files = new GodotFileService(resolver);
            var scenes = new SceneSerializer();

            await GodotTools.CreateGodotProjectAsync(files, resolver, root, "LintDemo");
            File.WriteAllText(Path.Combine(root, "icon.png"), ""); // create dummy asset without .import

            var result = await GodotTools.LintProjectAsync(files, resolver, scenes, root);
            result.Success.Should().BeTrue();
            var issues = (List<LintIssue>)result.Data!;
            issues.Should().Contain(i => i.Message.Contains("missing .import file"));
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
