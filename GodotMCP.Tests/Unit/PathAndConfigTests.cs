using System;
using Xunit;
using GodotMCP.Infrastructure.Config;
using GodotMCP.Tests.Fixtures;
using Xunit;

namespace GodotMCP.Tests.Unit;

public class PathAndConfigTests
{
    /// <summary>
    /// Unit tests for path resolution and project configuration service.
    /// </summary>
    [Fact]
    public async Task ProjectConfigService_ShouldSetAndReadValues()
    {
        var (root, resolver, _) = FixtureFactory.CreateProject();
        try
        {
            var config = new ProjectConfigService(resolver);
            await config.SetValueAsync("application", "config/name", "\"Demo\"");
            var value = await config.GetValueAsync("application", "config/name");
            Assert.Equal("\"Demo\"", value);
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    public static IEnumerable<object[]> ResPathCases()
    {
        for (var i = 0; i < 30; i++)
        {
            yield return [$"res://scenes/{i}/Main.tscn"];
        }
    }

    [Theory]
    [MemberData(nameof(ResPathCases))]
    public void PathResolver_ShouldResolveResPaths(string path)
    {
        var (root, resolver, _) = FixtureFactory.CreateProject();
        try
        {
            var absolute = resolver.ResolveResPath(path);
            Assert.StartsWith(root, absolute);
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }
}
