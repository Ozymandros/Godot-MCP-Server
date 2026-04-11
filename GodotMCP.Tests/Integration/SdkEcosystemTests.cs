namespace GodotMCP.Tests.Integration;

/// <summary>
/// Integration tests for SDK ecosystem discovery and installation tools.
/// </summary>
public class SdkEcosystemTests
{
    /// <summary>
    /// Verifies discovery, health verification, install stub, and compatibility listing flows.
    /// </summary>
    [Fact]
    public async Task DiscoverIntegrations_ShouldFindPluginCfg()
    {
        var root = Path.Combine(Path.GetTempPath(), "GodotMcpSdk", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "addons", "myplugin"));
        File.WriteAllText(Path.Combine(root, "project.godot"), "config_version=5");
        File.WriteAllText(Path.Combine(root, "addons", "myplugin", "plugin.cfg"), "[plugin]");

        try
        {
            IPathResolver resolver = new PathResolver(root);
            IGodotFileService files = new GodotFileService(resolver);
            var config = new ProjectConfigService(resolver);
            var inspector = new IntegrationInspector(resolver);

            var result = GodotTools.DiscoverIntegrations(inspector);
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            ((Dictionary<string, string>)result.Data!).Keys.Should().Contain("myplugin");

            GodotTools.VerifyIntegrationHealth(inspector, "myplugin").Success.Should().BeTrue();

            var installResult = await GodotTools.InstallIntegrationAsync(
                files,
                config,
                "Analytics SDK",
                "https://example.com/analytics",
                Core.Models.IntegrationProfile.CommunitySdk);
            installResult.Success.Should().BeTrue();

            var compatibility = GodotTools.ListIntegrationCompatibility(inspector);
            compatibility.Success.Should().BeTrue();
            compatibility.Data.Should().NotBeNull();
            ((Dictionary<string, string>)compatibility.Data!).Keys.Should().Contain("Analytics_SDK");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    /// <summary>
    /// Verifies integration installation fails when required name input is blank.
    /// </summary>
    [Fact]
    public async Task InstallIntegration_WithEmptyName_ShouldFail()
    {
        var root = Path.Combine(Path.GetTempPath(), "GodotMcpSdk", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "project.godot"), "config_version=5");

        try
        {
            IPathResolver resolver = new PathResolver(root);
            IGodotFileService files = new GodotFileService(resolver);
            var config = new ProjectConfigService(resolver);

            var result = await GodotTools.InstallIntegrationAsync(files, config, "", "https://example.com", Core.Models.IntegrationProfile.VendorSdk);
            result.Success.Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
