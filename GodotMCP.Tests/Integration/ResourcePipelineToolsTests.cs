using GodotMCP.Infrastructure.Services;

namespace GodotMCP.Tests.Integration;

/// <summary>
/// Integration tests for resource pipeline MCP commands.
/// </summary>
public class ResourcePipelineToolsTests
{
    /// <summary>
    /// Verifies write and read commands persist and return typed resource content.
    /// </summary>
    [Fact]
    public async Task ResourceWriteAndRead_CommandsShouldRoundTrip()
    {
        var (root, resolver, _) = FixtureFactory.CreateProject();
        try
        {
            IResourcePipelineService service = new ResourcePipelineService(
                new GodotFileService(resolver),
                resolver,
                new ResourceSerializer());

            var write = await GodotTools.ResourceWriteAsync(
                service,
                resolver,
                root,
                "materials/Hud.tres",
                "Resource",
                new Dictionary<string, string>
                {
                    ["theme_color"] = "Color(0.2, 0.3, 0.4)",
                    ["font_size"] = "18"
                });

            write.Success.Should().BeTrue();

            var read = await GodotTools.ResourceReadAsync(service, resolver, root, "materials/Hud.tres");
            read.Success.Should().BeTrue();
            var payload = (ResourceDocumentDto)read.Data!;
            payload.Type.Should().Be("Resource");
            payload.Properties["font_size"].Should().Be("18");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies property update command mutates only provided keys.
    /// </summary>
    [Fact]
    public async Task ResourceUpdateProperties_CommandShouldUpsertEntries()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await files.WriteAsync(Path.Combine("materials", "Test.tres"), "[gd_resource type=\"Resource\" format=3]\n\nvalue = 1");
            IResourcePipelineService service = new ResourcePipelineService(files, resolver, new ResourceSerializer());

            var result = await GodotTools.ResourceUpdatePropertiesAsync(
                service,
                resolver,
                root,
                "materials/Test.tres",
                new Dictionary<string, string>
                {
                    ["value"] = "2",
                    ["name"] = "Demo"
                });

            result.Success.Should().BeTrue();
            var updated = (Dictionary<string, string>)result.Data!;
            updated["value"].Should().Be("2");
            updated["name"].Should().Be("Demo");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies remove property command deletes an existing key.
    /// </summary>
    [Fact]
    public async Task ResourceRemoveProperty_CommandShouldDeleteKey()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await files.WriteAsync(Path.Combine("materials", "Test.tres"), "[gd_resource type=\"Resource\" format=3]\n\nkeep = 1\nremove = 2");
            IResourcePipelineService service = new ResourcePipelineService(files, resolver, new ResourceSerializer());

            var result = await GodotTools.ResourceRemovePropertyAsync(
                service,
                resolver,
                root,
                "materials/Test.tres",
                "remove");

            result.Success.Should().BeTrue();
            var updated = (Dictionary<string, string>)result.Data!;
            updated.Should().ContainKey("keep");
            updated.Should().NotContainKey("remove");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies read command fails cleanly when resource does not exist.
    /// </summary>
    [Fact]
    public async Task ResourceRead_CommandShouldFailForMissingFile()
    {
        var (root, resolver, _) = FixtureFactory.CreateProject();
        try
        {
            IResourcePipelineService service = new ResourcePipelineService(
                new GodotFileService(resolver),
                resolver,
                new ResourceSerializer());

            var result = await GodotTools.ResourceReadAsync(service, resolver, root, "materials/Missing.tres");

            result.Success.Should().BeFalse();
            result.Message.Should().Contain("not found");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies <c>resource.assign_texture</c> writes <c>res://</c> ext_resource and property.
    /// </summary>
    [Fact]
    public async Task ResourceAssignTexture_CommandShouldWireExtResource()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await files.WriteAsync(Path.Combine("materials", "Mat.tres"), "[gd_resource type=\"StandardMaterial3D\" format=3]\n\nalbedo_color = Color(1, 1, 1)");
            await files.WriteAsync(Path.Combine("textures", "t.png"), "x");
            IResourcePipelineService service = new ResourcePipelineService(files, resolver, new ResourceSerializer());

            var result = await GodotTools.ResourceAssignTextureAsync(
                service,
                resolver,
                root,
                "materials/Mat.tres",
                "textures/t.png",
                "albedo_texture",
                "Texture2D");

            result.Success.Should().BeTrue();
            var text = await files.ReadAsync(Path.Combine("materials", "Mat.tres"));
            text.Should().Contain("path=\"res://textures/t.png\"");
            text.Should().Contain("albedo_texture = ExtResource(\"1\")");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }
}
