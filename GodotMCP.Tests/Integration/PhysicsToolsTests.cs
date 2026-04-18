using System.Text.Json;

namespace GodotMCP.Tests.Integration;

/// <summary>
/// Integration tests for physics MCP tool commands.
/// </summary>
public class PhysicsToolsTests
{
    /// <summary>
    /// Verifies physics.create_body creates the body node.
    /// </summary>
    [Fact]
    public async Task PhysicsCreateBody_CommandShouldCreateBody()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            IPhysicsService service = new PhysicsService(files, resolver, new SceneGraphService(files, new SceneSerializer()));

            var result = await GodotTools.PhysicsCreateBodyAsync(
                service,
                resolver,
                root,
                "scenes/Main.tscn",
                ".",
                "RigidBody3D",
                "Crate");

            result.Success.Should().BeTrue();
            var sceneText = await files.ReadAsync(Path.Combine(root, "scenes", "Main.tscn"));
            sceneText.Should().Contain("[node name=\"Crate\" type=\"RigidBody3D\" parent=\".\"]");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies physics.update_body updates selected properties.
    /// </summary>
    [Fact]
    public async Task PhysicsUpdateBody_CommandShouldUpdateProperties()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            IPhysicsService service = new PhysicsService(files, resolver, new SceneGraphService(files, new SceneSerializer()));
            await GodotTools.PhysicsCreateBodyAsync(service, resolver, root, "scenes/Main.tscn", ".", "RigidBody3D", "Crate");

            using var payload = JsonDocument.Parse("""
{
  "collision_layer": 2,
  "collision_mask": 3,
  "gravity_scale": 0.7,
  "lock_rotation": true
}
""");
            var properties = payload.RootElement
                .EnumerateObject()
                .ToDictionary(x => x.Name, x => x.Value.Clone(), StringComparer.Ordinal);

            var result = await GodotTools.PhysicsUpdateBodyAsync(
                service,
                resolver,
                root,
                "scenes/Main.tscn",
                "Crate",
                properties);

            result.Success.Should().BeTrue();
            var sceneText = await files.ReadAsync(Path.Combine(root, "scenes", "Main.tscn"));
            sceneText.Should().Contain("collision_layer = 2");
            sceneText.Should().Contain("collision_mask = 3");
            sceneText.Should().Contain("gravity_scale = 0.7");
            sceneText.Should().Contain("lock_rotation = true");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies physics.validate reports missing collision shape issues.
    /// </summary>
    [Fact]
    public async Task PhysicsValidate_CommandShouldReportIssues()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            IPhysicsService service = new PhysicsService(files, resolver, new SceneGraphService(files, new SceneSerializer()));
            await GodotTools.PhysicsCreateBodyAsync(service, resolver, root, "scenes/Main.tscn", ".", "RigidBody3D", "Ghost", addCollisionShape: false);

            var result = await GodotTools.PhysicsValidateAsync(service, resolver, root);

            result.Success.Should().BeTrue();
            var issues = (List<PhysicsValidationIssueDto>)result.Data!;
            issues.Should().Contain(x => x.Rule == "missing-collision-shape" && x.NodePath == "Ghost");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }
}
