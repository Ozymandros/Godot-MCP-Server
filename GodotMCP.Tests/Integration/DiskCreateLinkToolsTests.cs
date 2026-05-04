using FluentAssertions;
using GodotMCP.Application.Tools;
using GodotMCP.Infrastructure.Serialization;
using GodotMCP.Infrastructure.Services;
using GodotMCP.Tests.Fixtures;

namespace GodotMCP.Tests.Integration;

/// <summary>
/// Integration tests for optional create+link parameters on disk-first tools.
/// </summary>
public class DiskCreateLinkToolsTests
{
    /// <summary>
    /// Verifies create_script with linkSceneFileName + linkNodePath attaches via ext_resource.
    /// </summary>
    [Fact]
    public async Task CreateScript_WithLink_AttachesToSceneNode()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "SceneGraphValid.tscn", "scenes/Main.tscn");
            var serializer = new SceneSerializer();
            var result = await GodotTools.CreateScriptAsync(
                files,
                resolver,
                root,
                "scripts/Linked.gd",
                "gd",
                "Node2D",
                "Linked",
                rawContent: null,
                sceneSerializer: serializer,
                linkSceneFileName: "Main.tscn",
                linkNodePath: "Player");

            result.Success.Should().BeTrue();
            var sceneText = await files.ReadAsync(Path.Combine(root, "scenes", "Main.tscn"));
            sceneText.Should().Contain("path=\"res://scripts/Linked.gd\"");
            sceneText.Should().Contain("script = ExtResource(");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies partial link parameters are rejected.
    /// </summary>
    [Fact]
    public async Task CreateScript_PartialLinkArgs_ShouldFail()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            var serializer = new SceneSerializer();
            var result = await GodotTools.CreateScriptAsync(
                files,
                resolver,
                root,
                "scripts/Orphan.gd",
                "gd",
                "Node",
                "Orphan",
                sceneSerializer: serializer,
                linkSceneFileName: "Main.tscn",
                linkNodePath: null);

            result.Success.Should().BeFalse();
            result.Message.Should().Contain("linkSceneFileName and linkNodePath");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies create_texture with linkMaterialFileName assigns texture on a material .tres.
    /// </summary>
    [Fact]
    public async Task CreateTexture_WithMaterialLink_AssignsAlbedo()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "materials"));
            var matPath = Path.Combine(root, "materials", "M.tres");
            await File.WriteAllTextAsync(
                matPath,
                """
                [gd_resource type="StandardMaterial3D" format=3]

                [resource]
                """);

            var pipeline = new ResourcePipelineService(files, resolver, new ResourceSerializer());
            var result = await GodotTools.CreateTextureAsync(
                files,
                resolver,
                new ImportFileGenerator(),
                root,
                "textures/tex.png",
                resourcePipelineService: pipeline,
                linkMaterialFileName: "materials/M.tres");

            result.Success.Should().BeTrue();
            var matText = await File.ReadAllTextAsync(matPath);
            matText.Should().Contain("albedo_texture = ExtResource(");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }
}
