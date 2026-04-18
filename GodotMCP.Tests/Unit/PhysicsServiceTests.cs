namespace GodotMCP.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="PhysicsService"/> behavior.
/// </summary>
public class PhysicsServiceTests
{
    /// <summary>
    /// Verifies creating a body with default settings adds body and collision shape.
    /// </summary>
    [Fact]
    public async Task CreateBodyAsync_ShouldCreateBodyAndCollisionShape()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            var service = CreateService(files, resolver);

            var result = await service.CreateBodyAsync(new PhysicsCreateBodyRequest(
                Path.Combine(root, "scenes", "Main.tscn"),
                ".",
                "Crate",
                "RigidBody3D"));

            result.Success.Should().BeTrue();
            var sceneText = await files.ReadAsync(Path.Combine(root, "scenes", "Main.tscn"));
            sceneText.Should().Contain("[node name=\"Crate\" type=\"RigidBody3D\" parent=\".\"]");
            sceneText.Should().Contain("[node name=\"CollisionShape\" type=\"CollisionShape3D\" parent=\"Crate\"]");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies update rejects invalid collision layer values.
    /// </summary>
    [Fact]
    public async Task UpdateBodyAsync_ShouldRejectInvalidCollisionLayer()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            var service = CreateService(files, resolver);
            await service.CreateBodyAsync(new PhysicsCreateBodyRequest(Path.Combine(root, "scenes", "Main.tscn"), ".", "Crate", "RigidBody3D"));

            var result = await service.UpdateBodyAsync(new PhysicsUpdateBodyRequest(
                Path.Combine(root, "scenes", "Main.tscn"),
                "Crate",
                new Dictionary<string, object?>
                {
                    ["collision_layer"] = 0
                }));

            result.Success.Should().BeFalse();
            result.Message.Should().Contain("greater than 0");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies validation reports missing collision shapes for bodies.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_ShouldReportMissingCollisionShape()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            var service = CreateService(files, resolver);
            await service.CreateBodyAsync(new PhysicsCreateBodyRequest(
                Path.Combine(root, "scenes", "Main.tscn"),
                ".",
                "Ghost",
                "RigidBody3D",
                AddCollisionShape: false));

            var issues = await service.ValidateAsync(root);

            issues.Should().Contain(x => x.Rule == "missing-collision-shape" && x.NodePath == "Ghost");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    private static PhysicsService CreateService(IGodotFileService files, IPathResolver resolver)
        => new(files, resolver, new SceneGraphService(files, new SceneSerializer()));
}
