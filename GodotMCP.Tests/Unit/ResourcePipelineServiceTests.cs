namespace GodotMCP.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="ResourcePipelineService"/> behavior.
/// </summary>
public class ResourcePipelineServiceTests
{
    /// <summary>
    /// Verifies that reading a resource returns type and properties.
    /// </summary>
    [Fact]
    public async Task ReadAsync_ShouldReturnTypeAndProperties()
    {
        var (root, _, files) = FixtureFactory.CreateProject();
        try
        {
            await files.WriteAsync(Path.Combine("materials", "Test.tres"), "[gd_resource type=\"StandardMaterial3D\" format=3]\n\nalbedo_color = Color(1, 1, 1)");
            var service = CreateService(files, root);

            var document = await service.ReadAsync(Path.Combine("materials", "Test.tres"));

            document.Type.Should().Be("StandardMaterial3D");
            document.Properties["albedo_color"].Should().Be("Color(1, 1, 1)");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies that writing a resource persists serialized content.
    /// </summary>
    [Fact]
    public async Task WriteAsync_ShouldPersistResourceFile()
    {
        var (root, _, files) = FixtureFactory.CreateProject();
        try
        {
            var service = CreateService(files, root);

            await service.WriteAsync(
                Path.Combine("materials", "NewMat.tres"),
                new ResourceDocument("StandardMaterial3D", new Dictionary<string, string>
                {
                    ["metallic"] = "0.35",
                    ["roughness"] = "0.7"
                }));

            var text = await files.ReadAsync(Path.Combine("materials", "NewMat.tres"));
            text.Should().Contain("[gd_resource type=\"StandardMaterial3D\"");
            text.Should().Contain("format=3");
            text.Should().Contain("metallic = 0.35");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies property updates overwrite existing values and add missing keys.
    /// </summary>
    [Fact]
    public async Task UpdatePropertiesAsync_ShouldUpsertProperties()
    {
        var (root, _, files) = FixtureFactory.CreateProject();
        try
        {
            await files.WriteAsync(Path.Combine("materials", "Test.tres"), "[gd_resource type=\"Resource\" format=3]\n\nvalue = 1");
            var service = CreateService(files, root);

            var result = await service.UpdatePropertiesAsync(Path.Combine("materials", "Test.tres"), new Dictionary<string, string>
            {
                ["value"] = "2",
                ["enabled"] = "true"
            });

            result.Success.Should().BeTrue();
            result.Properties!["value"].Should().Be("2");
            result.Properties["enabled"].Should().Be("true");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies removing an existing property updates persisted content.
    /// </summary>
    [Fact]
    public async Task RemovePropertyAsync_ShouldDeleteExistingProperty()
    {
        var (root, _, files) = FixtureFactory.CreateProject();
        try
        {
            await files.WriteAsync(Path.Combine("materials", "Test.tres"), "[gd_resource type=\"Resource\" format=3]\n\nkeep = 1\nremove = 2");
            var service = CreateService(files, root);

            var result = await service.RemovePropertyAsync(Path.Combine("materials", "Test.tres"), "remove");

            result.Success.Should().BeTrue();
            result.Properties!.Should().ContainKey("keep");
            result.Properties.Should().NotContainKey("remove");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies that invalid file extensions are rejected.
    /// </summary>
    [Fact]
    public async Task ReadAsync_ShouldRejectNonResourceExtension()
    {
        var (root, _, files) = FixtureFactory.CreateProject();
        try
        {
            var service = CreateService(files, root);

            var act = async () => await service.ReadAsync(Path.Combine("materials", "Bad.txt"));

            await act.Should().ThrowAsync<InvalidOperationException>();
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    private static ResourcePipelineService CreateService(IGodotFileService files, string root)
    {
        var resolver = new PathResolver(root);
        return new ResourcePipelineService(files, resolver, new ResourceSerializer());
    }
}
