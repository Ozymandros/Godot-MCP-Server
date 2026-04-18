using FluentAssertions;
using GodotMCP.Infrastructure.Config;
using GodotMCP.Tests.Fixtures;

namespace GodotMCP.Tests.Unit;

public class PathAndConfigTests
{
    [Fact]
    public async Task ProjectConfigService_ShouldSetAndReadValues()
    {
        var (root, resolver, _) = FixtureFactory.CreateProject();
        try
        {
            var config = new ProjectConfigService(resolver);
            await config.SetValueAsync("application", "config/name", "\"Demo\"");
            var value = await config.GetValueAsync("application", "config/name");
            value.Should().Be("\"Demo\"");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    public static IEnumerable<object[]> ProjectPathCases()
    {
        for (var i = 0; i < 30; i++)
        {
            yield return [$"scenes/{i}/Main.tscn"];
        }
    }

    [Theory]
    [MemberData(nameof(ProjectPathCases))]
    public void PathResolver_ShouldResolveProjectRelativePaths(string path)
    {
        var (root, resolver, _) = FixtureFactory.CreateProject();
        try
        {
            var absolute = resolver.ResolvePath(path);
            absolute.Should().StartWith(root);
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    [Fact]
    public void PathResolver_ShouldResolveLegacyResUri()
    {
        var (root, resolver, _) = FixtureFactory.CreateProject();
        try
        {
            var absolute = resolver.ResolvePath(Path.Combine(root, "scenes", "Main.tscn"));
            absolute.Should().StartWith(root);
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }
}
