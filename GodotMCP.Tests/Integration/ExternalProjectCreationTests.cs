namespace GodotMCP.Tests.Integration;

/// <summary>
/// Integration tests for project initialization behaviors when <c>project.godot</c> is missing.
/// </summary>
public class ExternalProjectCreationTests
{
    /// <summary>
    /// Verifies that when a project folder lacks <c>project.godot</c>, the server auto-creates
    /// a minimal project file and tooling can immediately mutate it (for example, adding an autoload).
    /// </summary>
    [Fact]
    public async Task MissingProjectFile_ShouldBeAutoCreated_AndAllowAutoloadMutation()
    {
        var root = Path.Combine(Path.GetTempPath(), "GodotMcpExternal", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            IPathResolver resolver = new PathResolver(root);
            IGodotFileService files = new GodotFileService(resolver);
            var config = new ProjectConfigService(resolver);

            var projectFile = Path.Combine(root, "project.godot");
            var mainSceneFile = Path.Combine(root, "scenes", "Main.tscn");
            if (File.Exists(projectFile)) File.Delete(projectFile);

            // Calling GetProjectInfoAsync should create a project.godot and initialize a main scene.
            var info = await GodotTools.GetProjectInfoAsync(files, resolver, config, root);
            info.Success.Should().BeTrue();
            File.Exists(projectFile).Should().BeTrue();
            File.Exists(mainSceneFile).Should().BeTrue();
            var createdMainScene = await File.ReadAllTextAsync(mainSceneFile);
            createdMainScene.Should().Contain("[node name=\"Main\" type=\"Node2D\"]");

            // Add an autoload entry; the tool should update the project's project.godot in-place.
            var autoloadResult = await GodotTools.ConfigureAutoloadAsync(resolver, config, root, "Singleton", "scenes/Main.tscn", true);
            autoloadResult.Success.Should().BeTrue();

            var text = await File.ReadAllTextAsync(projectFile);
            text.Should().Contain("run/main_scene=\"res://scenes/Main.tscn\"");
            text.Should().Contain("[autoload]");
            text.Should().Contain("Singleton=");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
