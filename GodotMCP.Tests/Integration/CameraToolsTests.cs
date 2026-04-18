using System.Text.Json;
using FluentAssertions;
using GodotMCP.Application.Tools;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using GodotMCP.Infrastructure.Serialization;
using GodotMCP.Infrastructure.Services;
using GodotMCP.Tests.Fixtures;

namespace GodotMCP.Tests.Integration;

/// <summary>
/// Integration tests for camera MCP tool commands.
/// </summary>
public class CameraToolsTests
{
    /// <summary>
    /// Verifies that the list command returns all cameras from fixture scenes.
    /// </summary>
    [Fact]
    public async Task CameraList_CommandShouldReturnAllCameras()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "CamerasValid.tscn", "scenes/Main.tscn");
            ICameraService cameraService = new CameraService(files, resolver, new SceneSerializer());

            var result = await GodotTools.CameraListAsync(cameraService, resolver, root);

            result.Success.Should().BeTrue();
            var cameras = (List<CameraNodeDto>)result.Data!;
            cameras.Should().HaveCount(2);
            cameras.Should().Contain(c => c.Type == nameof(CameraNodeType.Camera3D));
            cameras.Should().Contain(c => c.Type == nameof(CameraNodeType.Camera2D));
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies that create command persists preset properties in the scene file.
    /// </summary>
    [Fact]
    public async Task CameraCreate_CommandShouldPersistPresetValues()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "CamerasValid.tscn", "scenes/Main.tscn");
            ICameraService cameraService = new CameraService(files, resolver, new SceneSerializer());

            var result = await GodotTools.CameraCreateAsync(cameraService, resolver, root, "scenes/Main.tscn", "GameplayCamera", "3d", "fps");

            result.Success.Should().BeTrue();
            var sceneText = await files.ReadAsync(Path.Combine(root, "scenes", "Main.tscn"));
            sceneText.Should().Contain("[node name=\"GameplayCamera\" type=\"Camera3D\" parent=\".\"]");
            sceneText.Should().Contain("fov = 90");
            sceneText.Should().Contain("near = 0.05");
            sceneText.Should().Contain("far = 1000");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies that update command mutates only supplied camera properties.
    /// </summary>
    [Fact]
    public async Task CameraUpdate_CommandShouldUpdateSelectedPropertiesOnly()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "CamerasValid.tscn", "scenes/Main.tscn");
            ICameraService cameraService = new CameraService(files, resolver, new SceneSerializer());

            using var payload = JsonDocument.Parse("""
{
  "fov": 65,
  "current": false,
  "projection": "orthographic"
}
""");

            var properties = payload.RootElement
                .EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.Ordinal);

            var result = await GodotTools.CameraUpdateAsync(cameraService, resolver, root, "scenes/Main.tscn", "MainCamera", properties);

            result.Success.Should().BeTrue();
            var sceneText = await files.ReadAsync(Path.Combine(root, "scenes", "Main.tscn"));
            sceneText.Should().Contain("fov = 65");
            sceneText.Should().Contain("current = false");
            sceneText.Should().Contain("projection = 1");
            sceneText.Should().Contain("far = 500");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Verifies that validate command emits lint-style issues for invalid scenes.
    /// </summary>
    [Fact]
    public async Task CameraValidate_CommandShouldReturnLintStyleIssues()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await FixtureFactory.CopySceneFixtureAsync(root, "CamerasInvalid.tscn", "scenes/Invalid.tscn");
            ICameraService cameraService = new CameraService(files, resolver, new SceneSerializer());

            var result = await GodotTools.CameraValidateAsync(cameraService, resolver, root);

            result.Success.Should().BeTrue();
            var issues = (List<CameraValidationIssueDto>)result.Data!;
            issues.Should().NotBeEmpty();
            issues.Should().OnlyContain(i => !string.IsNullOrWhiteSpace(i.Path));
            issues.Should().Contain(i => i.Rule == "multiple-current-cameras");
            issues.Should().Contain(i => i.Rule == "unsupported-projection");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }
}
