using System.Text.Json;
using GodotMCP.Infrastructure.Services;

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
            IPhysicsService service = new PhysicsService(files, resolver, new SceneGraphService(files, new SceneSerializer(), resolver));

            var result = await GodotTools.PhysicsCreateBodyAsync(
                service,
                files,
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
            IPhysicsService service = new PhysicsService(files, resolver, new SceneGraphService(files, new SceneSerializer(), resolver));
            await GodotTools.PhysicsCreateBodyAsync(service, files, resolver, root, "scenes/Main.tscn", ".", "RigidBody3D", "Crate");

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
                files,
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
            IPhysicsService service = new PhysicsService(files, resolver, new SceneGraphService(files, new SceneSerializer(), resolver));
            await GodotTools.PhysicsCreateBodyAsync(service, files, resolver, root, "scenes/Main.tscn", ".", "RigidBody3D", "Ghost", addCollisionShape: false);

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

    [Fact]
    public async Task PhysicsShapeLifecycle_CommandsShouldManageShapeNodes()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            IPhysicsService service = new PhysicsService(files, resolver, new SceneGraphService(files, new SceneSerializer(), resolver));
            await GodotTools.PhysicsCreateBodyAsync(service, files, resolver, root, "scenes/Main.tscn", ".", "RigidBody3D", "Crate", addCollisionShape: false);

            using var shapeParamsJson = JsonDocument.Parse("""{"width":1.5,"height":2.0,"depth":3.0}""");
            var shapeParams = shapeParamsJson.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone(), StringComparer.Ordinal);
            var add = await GodotTools.PhysicsAddShapeAsync(
                service,
                files,
                resolver,
                root,
                "scenes/Main.tscn",
                "Crate",
                "CollisionShape3D",
                "Hitbox",
                "box",
                shapeParams);
            add.Success.Should().BeTrue();

            var flags = await GodotTools.PhysicsSetShapeFlagsAsync(service, files, resolver, root, "scenes/Main.tscn", "Crate/Hitbox", disabled: true);
            flags.Success.Should().BeTrue();

            var remove = await GodotTools.PhysicsRemoveShapeAsync(service, files, resolver, root, "scenes/Main.tscn", "Crate/Hitbox");
            remove.Success.Should().BeTrue();

            var sceneText = await files.ReadAsync(Path.Combine(root, "scenes", "Main.tscn"));
            sceneText.Should().Contain("[node name=\"Crate\" type=\"RigidBody3D\" parent=\".\"]");
            sceneText.Should().NotContain("[node name=\"Hitbox\" type=\"CollisionShape3D\" parent=\"Crate\"]");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    [Fact]
    public async Task PhysicsCollisionPolygonLifecycle_CommandsShouldManagePolygonNodes()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            IPhysicsService service = new PhysicsService(files, resolver, new SceneGraphService(files, new SceneSerializer(), resolver));
            await GodotTools.PhysicsCreateBodyAsync(service, files, resolver, root, "scenes/Main.tscn", ".", "StaticBody2D", "Floor", addCollisionShape: false);

            var add = await GodotTools.PhysicsAddCollisionPolygonAsync(
                service,
                files,
                resolver,
                root,
                "scenes/Main.tscn",
                "Floor",
                "CollisionPolygon2D",
                "FloorPoly",
                "PackedVector2Array(0,0, 64,0, 64,16, 0,16)");
            add.Success.Should().BeTrue();

            var update = await GodotTools.PhysicsUpdateCollisionPolygonAsync(
                service,
                files,
                resolver,
                root,
                "scenes/Main.tscn",
                "Floor/FloorPoly",
                polygonData: "PackedVector2Array(0,0, 128,0, 128,16, 0,16)");
            update.Success.Should().BeTrue();

            var remove = await GodotTools.PhysicsRemoveCollisionPolygonAsync(service, files, resolver, root, "scenes/Main.tscn", "Floor/FloorPoly");
            remove.Success.Should().BeTrue();
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    [Fact]
    public async Task PhysicsAreaTools_CommandsShouldUpdateAreaProperties()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            IPhysicsService service = new PhysicsService(files, resolver, new SceneGraphService(files, new SceneSerializer(), resolver));
            await GodotTools.PhysicsCreateBodyAsync(service, files, resolver, root, "scenes/Main.tscn", ".", "Area3D", "Trigger", addCollisionShape: false);

            var monitoring = await GodotTools.PhysicsAreaSetMonitoringAsync(service, files, resolver, root, "scenes/Main.tscn", "Trigger", monitoring: true, monitorable: false);
            monitoring.Success.Should().BeTrue();

            var priority = await GodotTools.PhysicsAreaSetPriorityAsync(service, files, resolver, root, "scenes/Main.tscn", "Trigger", priority: 3.5);
            priority.Success.Should().BeTrue();

            var overrideResult = await GodotTools.PhysicsAreaSetSpaceOverrideAsync(
                service,
                files,
                resolver,
                root,
                "scenes/Main.tscn",
                "Trigger",
                "combine_replace",
                gravity: 9.8,
                linear_damp: 0.2);
            overrideResult.Success.Should().BeTrue();

            var filters = await GodotTools.PhysicsAreaSetCollisionFiltersAsync(service, files, resolver, root, "scenes/Main.tscn", "Trigger", collision_layer: 2, collision_mask: 4);
            filters.Success.Should().BeTrue();

            var sceneText = await files.ReadAsync(Path.Combine(root, "scenes", "Main.tscn"));
            sceneText.Should().Contain("monitoring = true");
            sceneText.Should().Contain("monitorable = false");
            sceneText.Should().Contain("priority = 3.5");
            sceneText.Should().Contain("space_override = 2");
            sceneText.Should().Contain("collision_layer = 2");
            sceneText.Should().Contain("collision_mask = 4");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    [Fact]
    public async Task PhysicsAreaTools_ShouldRejectNonAreaNodes()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            IPhysicsService service = new PhysicsService(files, resolver, new SceneGraphService(files, new SceneSerializer(), resolver));
            await GodotTools.PhysicsCreateBodyAsync(service, files, resolver, root, "scenes/Main.tscn", ".", "RigidBody3D", "Crate", addCollisionShape: false);

            var result = await GodotTools.PhysicsAreaSetMonitoringAsync(service, files, resolver, root, "scenes/Main.tscn", "Crate", monitoring: true);
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Area node");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }
}
