namespace GodotMCP.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="LightingService"/> behavior.
/// </summary>
public class LightingServiceTests
{
    /// <summary>
    /// Verifies creating a light with preset persists expected properties.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ShouldAddLightWithPresetProperties()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            var service = CreateService(files, resolver);

            var result = await service.CreateAsync(new LightCreateRequest(
                Path.Combine(root, "scenes", "Main.tscn"),
                ".",
                "Sun",
                "DirectionalLight3D",
                "sun"));

            result.Success.Should().BeTrue();
            result.Light.Should().NotBeNull();
            result.Light!.Type.Should().Be("DirectionalLight3D");
            result.Light.Energy.Should().BeGreaterThan(0);
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies updating unsupported properties is rejected.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ShouldRejectUnsupportedProperty()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            var service = CreateService(files, resolver);
            await service.CreateAsync(new LightCreateRequest(Path.Combine(root, "scenes", "Main.tscn"), ".", "Lamp", "OmniLight3D", null));

            var result = await service.UpdateAsync(new LightUpdateRequest(
                Path.Combine(root, "scenes", "Main.tscn"),
                "Lamp",
                new Dictionary<string, object?>
                {
                    ["unknown_property"] = 1
                }));

            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Unsupported light property");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies validation reports non-positive energy lights.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_ShouldReportNonPositiveEnergy()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            var service = CreateService(files, resolver);
            await service.CreateAsync(new LightCreateRequest(Path.Combine(root, "scenes", "Main.tscn"), ".", "Lamp", "OmniLight3D", null));
            await service.UpdateAsync(new LightUpdateRequest(
                Path.Combine(root, "scenes", "Main.tscn"),
                "Lamp",
                new Dictionary<string, object?>
                {
                    ["light_energy"] = 0
                }));

            var issues = await service.ValidateAsync(root);

            issues.Should().Contain(x => x.Rule == "non-positive-energy");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    private static LightingService CreateService(IGodotFileService files, IPathResolver resolver)
    {
        var graph = new SceneGraphService(files, new SceneSerializer());
        return new LightingService(files, resolver, graph);
    }
}
