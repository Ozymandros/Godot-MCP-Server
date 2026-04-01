using System;
using System.Collections.Generic;
using Xunit;
using GodotMCP.Application.Tools;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using GodotMCP.Infrastructure.Serialization;
using GodotMCP.Infrastructure.Services;
using GodotMCP.Infrastructure.Integrations;
using GodotMCP.Infrastructure.Process;
using GodotMCP.Infrastructure.Config;
using GodotMCP.Tests.Fixtures;

namespace GodotMCP.Tests.Unit;

public class GodotToolsIntegrationsTests
{
    [Fact]
    public void VerifyIntegrationHealth_FindsIntegration_IgnoresCase()
    {
        var (_, resolver, files) = FixtureFactory.CreateProject();
        var inspector = new FakeInspector(new IntegrationMetadata
        {
            Name = "MyPlugin",
            Profile = IntegrationProfile.ProjectLocalPlugin,
            Source = "res://addons/myplugin/plugin.cfg",
            GodotVersionRange = "4.x",
            PlatformSupport = "all",
            IsMaintained = true
        });

        var tools = new GodotTools(
            files,
            resolver,
            new SceneSerializer(),
            new ResourceSerializer(),
            new ImportFileGenerator(),
            new ProjectConfigService(resolver),
            new GodotCliService(resolver),
            inspector);

        var resultLower = tools.VerifyIntegrationHealth("myplugin");
        var resultMixed = tools.VerifyIntegrationHealth("MyPlugin");

        Assert.True(resultLower.Success, "Expected case-insensitive match to succeed");
        Assert.True(resultMixed.Success, "Expected exact-case match to succeed");
    }

    [Fact]
    public void ListIntegrationCompatibility_IncludesMaintenanceAndSource()
    {
        var (_, resolver, files) = FixtureFactory.CreateProject();
        var inspector = new FakeInspector(
            new IntegrationMetadata
            {
                Name = "one",
                Profile = IntegrationProfile.CommunitySdk,
                Source = "https://example.com/one",
                GodotVersionRange = "4.x",
                PlatformSupport = "windows,linux",
                IsMaintained = true
            },
            new IntegrationMetadata
            {
                Name = "two",
                Profile = IntegrationProfile.VendorSdk,
                Source = "https://example.com/two",
                GodotVersionRange = "3.x",
                PlatformSupport = "android",
                IsMaintained = false
            }
        );

        var tools = new GodotTools(
            files,
            resolver,
            new SceneSerializer(),
            new ResourceSerializer(),
            new ImportFileGenerator(),
            new ProjectConfigService(resolver),
            new GodotCliService(resolver),
            inspector);

        var result = tools.ListIntegrationCompatibility();
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Contains("one", result.Data!.Keys);
        Assert.Contains("two", result.Data.Keys);
        Assert.Contains("maintenance=active", result.Data["one"]);
        Assert.Contains("maintenance=stale", result.Data["two"]);
    }

    private sealed class FakeInspector : IIntegrationInspector
    {
        private readonly IReadOnlyList<IntegrationMetadata> items;

        public FakeInspector(params IntegrationMetadata[] items)
        {
            this.items = new List<IntegrationMetadata>(items);
        }

        public IReadOnlyList<IntegrationMetadata> Discover() => items;
    }
}
