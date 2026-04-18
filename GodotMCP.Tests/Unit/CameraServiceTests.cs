using FluentAssertions;
using GodotMCP.Core.Models;
using GodotMCP.Infrastructure.Serialization;
using GodotMCP.Infrastructure.Services;
using GodotMCP.Tests.Fixtures;

namespace GodotMCP.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="CameraService" /> behavior.
/// </summary>
public class CameraServiceTests
{
    /// <summary>
    /// Verifies that listing cameras returns both Camera2D and Camera3D nodes.
    /// </summary>
    [Fact]
    public async Task ListAsync_ShouldReturnCamera2DAndCamera3DNodes()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "CamerasValid.tscn", "scenes/Main.tscn");
            var service = new CameraService(files, resolver, new SceneSerializer());

            var result = await service.ListAsync(root);

            result.Should().HaveCount(2);
            result.Should().Contain(x => x.Type == CameraNodeType.Camera3D && x.NodePath == "MainCamera");
            result.Should().Contain(x => x.Type == CameraNodeType.Camera2D && x.NodePath == "UiCamera");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies that creation applies the orthographic UI preset values.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ShouldApplyOrthographicPreset()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "CamerasValid.tscn", "scenes/Main.tscn");
            var service = new CameraService(files, resolver, new SceneSerializer());

            var result = await service.CreateAsync(new CameraCreateRequest(
                Path.Combine(root, "scenes", "Main.tscn"),
                "Cinematic",
                CameraNodeType.Camera3D,
                "orthographic-ui"));

            result.Success.Should().BeTrue();

            var sceneText = await files.ReadAsync(Path.Combine(root, "scenes", "Main.tscn"));
            sceneText.Should().Contain("[node name=\"Cinematic\" type=\"Camera3D\" parent=\".\"]");
            sceneText.Should().Contain("projection = 1");
            sceneText.Should().Contain("size = 16");
            sceneText.Should().Contain("near = 0.01");
            sceneText.Should().Contain("far = 4096");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies that unsupported camera properties are rejected during updates.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ShouldRejectUnsupportedProperties()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "CamerasValid.tscn", "scenes/Main.tscn");
            var service = new CameraService(files, resolver, new SceneSerializer());

            var result = await service.UpdateAsync(new CameraUpdateRequest(
                Path.Combine(root, "scenes", "Main.tscn"),
                "MainCamera",
                new Dictionary<string, object?>
                {
                    ["lens"] = 50
                }));

            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Unsupported camera property");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies that validation reports all expected rule violations.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_ShouldReturnExpectedValidationRules()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "CamerasInvalid.tscn", "scenes/Broken.tscn");
            var service = new CameraService(files, resolver, new SceneSerializer());

            var issues = await service.ValidateAsync(root);

            issues.Should().Contain(i => i.Rule == "multiple-current-cameras");
            issues.Should().Contain(i => i.Rule == "invalid-near-far");
            issues.Should().Contain(i => i.Rule == "missing-parent");
            issues.Should().Contain(i => i.Rule == "unsupported-projection");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }
}
