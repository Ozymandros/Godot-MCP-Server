using Xunit;
using GodotMCP.Application.Tools;
using GodotMCP.Core.Interfaces;
using GodotMCP.Infrastructure.Config;
using GodotMCP.Infrastructure.Integrations;
using GodotMCP.Infrastructure.Process;
using GodotMCP.Infrastructure.Serialization;
using GodotMCP.Infrastructure.Services;
using GodotMCP.Tests.TestIsolation;

namespace GodotMCP.Tests.Integration;

public class SdkEcosystemTests
{
    [Fact]
    public async Task DiscoverIntegrations_ShouldFindPluginCfg()
    {
        var root = AssemblyStartup.CreateSandboxDirectory("sdk");
        Directory.CreateDirectory(Path.Combine(root, "addons", "myplugin"));
        File.WriteAllText(Path.Combine(root, "project.godot"), "config_version=5");
        File.WriteAllText(Path.Combine(root, "addons", "myplugin", "plugin.cfg"), "[plugin]");
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
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Contains("myplugin", result.Data!.Keys);

        Assert.True(tools.VerifyIntegrationHealth("myplugin").Success);

        var installResult = await tools.InstallIntegrationAsync(
            "Analytics SDK",
            "https://example.com/analytics",
            Core.Models.IntegrationProfile.CommunitySdk);
        Assert.True(installResult.Success);

        var compatibility = tools.ListIntegrationCompatibility();
        Assert.True(compatibility.Success);
        Assert.NotNull(compatibility.Data);
        Assert.Contains("Analytics_SDK", compatibility.Data!.Keys);
    }

    [Fact]
    public async Task InstallIntegration_WithEmptyName_ShouldFail()
    {
        var root = AssemblyStartup.CreateSandboxDirectory("sdk");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "project.godot"), "config_version=5");
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
        Assert.False(result.Success);
    }
}
