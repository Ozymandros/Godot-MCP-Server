using System.Linq;
using FluentAssertions;
using GodotMCP.Core;
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
    public void PathResolver_ShouldRejectResUri()
    {
        var (root, resolver, _) = FixtureFactory.CreateProject();
        try
        {
            var act = () => resolver.ResolvePath("engine://scenes/Main.tscn");
            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    [Fact]
    public void PathResolver_ShouldAcceptAbsoluteAndPartialPaths()
    {
        var (root, resolver, _) = FixtureFactory.CreateProject();
        try
        {
            var absoluteInput = Path.Combine(root, "scenes", "Absolute.tscn");
            var absoluteResolved = resolver.ResolvePath(absoluteInput);
            absoluteResolved.Should().Be(Path.GetFullPath(absoluteInput));

            var partialResolved = resolver.ResolvePath("./scenes/../scenes/Partial.tscn");
            partialResolved.Should().Be(Path.GetFullPath(Path.Combine(root, "scenes", "Partial.tscn")));
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    [Fact]
    public void PathResolver_ShouldDeduplicateOverlappingSegments()
    {
        var (root, resolver, _) = FixtureFactory.CreateProject();
        try
        {
            var assetsRoot = Path.Combine(root, "Assets");
            Directory.CreateDirectory(assetsRoot);
            var assetsResolver = new Infrastructure.Services.PathResolver(assetsRoot);

            var resolved = assetsResolver.ResolvePath("Assets/Scripts/Player.cs");
            resolved.Should().Be(Path.GetFullPath(Path.Combine(assetsRoot, "Scripts", "Player.cs")));
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    [Fact]
    public void PathResolver_ShouldTreatSingleLeadingSeparatorAsProjectRelative()
    {
        var (root, resolver, _) = FixtureFactory.CreateProject();
        try
        {
            var slashResolved = resolver.ResolvePath("/scenes/LeadingSlash.tscn");
            slashResolved.Should().Be(Path.GetFullPath(Path.Combine(root, "scenes", "LeadingSlash.tscn")));

            var backslashResolved = resolver.ResolvePath("\\scenes\\LeadingBackslash.tscn");
            backslashResolved.Should().Be(Path.GetFullPath(Path.Combine(root, "scenes", "LeadingBackslash.tscn")));
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    [Fact]
    public void PathResolver_ShouldTreatMultipleLeadingSeparatorsAsProjectRelative()
    {
        var (root, resolver, _) = FixtureFactory.CreateProject();
        try
        {
            var tripleSlash = resolver.ResolvePath("///scenes/TripleLeading.tscn");
            tripleSlash.Should().Be(Path.GetFullPath(Path.Combine(root, "scenes", "TripleLeading.tscn")));
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    [Fact]
    public void PathResolver_GetProjectRelativePath_ShouldMatchResolvedLeadingSeparatorPaths()
    {
        var (root, resolver, _) = FixtureFactory.CreateProject();
        try
        {
            var abs = resolver.ResolvePath("\\scenes\\RelCheck.tscn");
            resolver.GetProjectRelativePath(abs).Replace('\\', '/').Should().Be("scenes/RelCheck.tscn");
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    [Fact]
    public void ProjectPathSyntax_ShouldNormalizeRelativeFileNameToken()
    {
        if (OperatingSystem.IsWindows())
        {
            ProjectPathSyntax.NormalizeRelativePathTokenForCombine("\\scenes\\A.tscn").Should().Be("scenes/A.tscn");
            ProjectPathSyntax.NormalizeRelativePathTokenForCombine("/scenes/A.tscn").Should().Be("scenes/A.tscn");
        }

        ProjectPathSyntax.NormalizeRelativePathTokenForCombine("scenes/A.tscn").Should().Be("scenes/A.tscn");
    }

    [Fact]
    public void ProjectPathSyntax_ShouldCollapseDuplicateSeparatorsInFragments()
    {
        ProjectPathSyntax.CollapseDuplicateDirectorySeparators("scenes//foo//bar.tscn").Should().Be("scenes/foo/bar.tscn");
        ProjectPathSyntax.CollapseDuplicateDirectorySeparators(@"scenes\\foo\\bar.tscn").Should().Be(@"scenes\foo\bar.tscn");
        ProjectPathSyntax.CollapseDuplicateDirectorySeparators(@"\\server\\share\\a.tscn").Should().Be(@"\\server\share\a.tscn");
    }

    [Fact]
    public void PathResolver_ShouldResolveDuplicateSlashSegmentsLikeSingleSeparators()
    {
        var (root, resolver, _) = FixtureFactory.CreateProject();
        try
        {
            var a = resolver.ResolvePath("scenes//Dup//Main.tscn");
            var b = resolver.ResolvePath("scenes/Dup/Main.tscn");
            a.Should().Be(b);
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    [Fact]
    public void PathResolver_ShouldRejectPathsOutsideProject()
    {
        var (root, resolver, _) = FixtureFactory.CreateProject();
        try
        {
            var act = () => resolver.ResolvePath("../outside.txt");
            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// Many spellings of the same project-relative <c>games/GamesStyle.tscn</c> (Unix vs Windows, optional separators).
    /// </summary>
    [Fact]
    public void PathResolver_GamesPathStyles_ShouldResolveToSameProjectFile()
    {
        var (root, resolver, _) = FixtureFactory.CreateProject();
        try
        {
            var expected = Path.GetFullPath(Path.Combine(root, "games", "GamesStyle.tscn"));
            var inputs = new List<string>
            {
                "games/GamesStyle.tscn",
                "games//GamesStyle.tscn",
                "./games/GamesStyle.tscn",
                "games/./GamesStyle.tscn",
                "/games/GamesStyle.tscn",
                "///games/GamesStyle.tscn",
            };

            if (OperatingSystem.IsWindows())
            {
                inputs.AddRange(
                [
                    @"games\GamesStyle.tscn",
                    @"\games\GamesStyle.tscn",
                    @"\games\\GamesStyle.tscn",
                    "/games\\GamesStyle.tscn",
                    Path.Combine(root, "games", "GamesStyle.tscn"),
                ]);
            }
            else
            {
                inputs.Add("//games/GamesStyle.tscn");
            }

            foreach (var input in inputs.Distinct())
            {
                resolver.ResolvePath(input).Should().Be(expected, $"input: {input}");
            }
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// On Windows, <c>//games/...</c> is treated as UNC (server name <c>games</c>), not as project-relative.
    /// </summary>
    [Fact]
    public void PathResolver_Windows_ShouldNotTreatDoubleSlashGamesAsProjectRelative()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var (root, resolver, _) = FixtureFactory.CreateProject();
        try
        {
            var act = () => resolver.ResolvePath("//games/GamesStyle.tscn");
            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    /// <summary>
    /// On Windows, <c>\\games\...</c> is UNC (host <c>games</c>), not <c>\games</c> under the current drive.
    /// </summary>
    [Fact]
    public void PathResolver_Windows_ShouldNotTreatUncBackslashGamesAsProjectRelative()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var (root, resolver, _) = FixtureFactory.CreateProject();
        try
        {
            var act = () => resolver.ResolvePath(@"\\games\share\GamesStyle.tscn");
            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    [Theory]
    [MemberData(nameof(GamesPathCollapseEquivalenceCases))]
    public void PathResolver_GamesPathCollapse_ShouldMatchNormalizedForm(string pathA, string pathB)
    {
        var (root, resolver, _) = FixtureFactory.CreateProject();
        try
        {
            resolver.ResolvePath(pathA).Should().Be(resolver.ResolvePath(pathB));
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }

    public static TheoryData<string, string> GamesPathCollapseEquivalenceCases()
    {
        var data = new TheoryData<string, string>
        {
            { "games//x//y.tscn", "games/x/y.tscn" },
            { @"games\\x\\y.tscn", "games/x/y.tscn" },
        };

        if (OperatingSystem.IsWindows())
        {
            data.Add(@"games\\x/y.tscn", "games/x/y.tscn");
        }

        return data;
    }
}
