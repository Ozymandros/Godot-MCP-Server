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

            await GodotTools.CreateGodotProjectAsync(files, "AnimDemo");
            await GodotTools.CreateSceneAsync(files, resolver, scenes, "res://scenes/Main.tscn", "Main", "Node2D");
            await GodotTools.AddNodeAsync(files, resolver, scenes, "res://scenes/Main.tscn", ".", "Sprite2D", "Sprite2D");

            (await GodotTools.AddAnimationPlayerAsync(files, resolver, scenes, "res://scenes/Main.tscn", ".")).Success.Should().BeTrue();
            (await GodotTools.AddAnimationAsync(files, resolver, scenes, "res://scenes/Main.tscn", "AnimationPlayer", "fade", 1.0f)).Success.Should().BeTrue();

            var keys = new List<KeyPoint>
            {
                new() { Time = 0, Value = "Vector2(0, 0)" },
                new() { Time = 1, Value = "Vector2(100, 100)" }
            };

            (await GodotTools.AddAnimationTrackAsync(files, resolver, scenes, "res://scenes/Main.tscn", "fade", "Sprite2D:position", "value", keys)).Success.Should().BeTrue();

            var sceneText = await files.ReadAsync("res://scenes/Main.tscn");
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

            await GodotTools.CreateGodotProjectAsync(files, "DiffDemo");
            await GodotTools.CreateSceneAsync(files, resolver, scenes, "res://scenes/A.tscn", "Root", "Node2D");
            await GodotTools.CreateSceneAsync(files, resolver, scenes, "res://scenes/B.tscn", "Root", "Node2D");
            await GodotTools.AddNodeAsync(files, resolver, scenes, "res://scenes/B.tscn", ".", "NewNode", "Sprite2D");

            var result = await GodotTools.DiffScenesAsync(files, resolver, scenes, "res://scenes/A.tscn", "res://scenes/B.tscn");
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

            await GodotTools.CreateGodotProjectAsync(files, "LintDemo");
            File.WriteAllText(Path.Combine(root, "icon.png"), ""); // create dummy asset without .import

            var result = await GodotTools.LintProjectAsync(files, resolver, scenes);
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
