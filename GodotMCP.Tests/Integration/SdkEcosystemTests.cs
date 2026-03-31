using FluentAssertions;
using GodotMCP.Application.Tools;
using GodotMCP.Core.Interfaces;
using GodotMCP.Infrastructure.Config;
using GodotMCP.Infrastructure.Integrations;
using GodotMCP.Infrastructure.Process;
using GodotMCP.Infrastructure.Serialization;
using GodotMCP.Infrastructure.Services;

namespace GodotMCP.Tests.Integration;

public class SdkEcosystemTests
{
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
            var tools = new GodotTools(
                files,
                resolver,
                new SceneSerializer(),
                new ResourceSerializer(),
                new ImportFileGenerator(),
                new ProjectConfigService(resolver),
                new GodotCliService(resolver),
                new IntegrationInspector(resolver));

            var result = tools.DiscoverIntegrations();
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Keys.Should().Contain("myplugin");

            tools.VerifyIntegrationHealth("myplugin").Success.Should().BeTrue();

            var installResult = await tools.InstallIntegrationAsync(
                "Analytics SDK",
                "https://example.com/analytics",
                Core.Models.IntegrationProfile.CommunitySdk);
            installResult.Success.Should().BeTrue();

            var compatibility = tools.ListIntegrationCompatibility();
            compatibility.Success.Should().BeTrue();
            compatibility.Data.Should().NotBeNull();
            compatibility.Data!.Keys.Should().Contain("Analytics_SDK");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

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
            var tools = new GodotTools(
                files,
                resolver,
                new SceneSerializer(),
                new ResourceSerializer(),
                new ImportFileGenerator(),
                new ProjectConfigService(resolver),
                new GodotCliService(resolver),
                new IntegrationInspector(resolver));

            var result = await tools.InstallIntegrationAsync("", "https://example.com", Core.Models.IntegrationProfile.VendorSdk);
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
