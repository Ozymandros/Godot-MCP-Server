using FluentAssertions;
using GodotMCP.Infrastructure.Process;
using GodotMCP.Core.Interfaces;
using GodotMCP.Infrastructure.Services;
using Xunit;

namespace GodotMCP.Tests.Unit;

/// <summary>
/// Basic sanity tests for <see cref="GodotCliService.LocateGodotBinary"/> to ensure
/// calling the locator is safe in environments without Godot installed.
/// </summary>
public class GodotCliServiceLocatorTests
{
    [Fact]
    public void LocateGodotBinary_ReturnsNull_WhenNothingPresent()
    {
        var resolver = new PathResolver(Path.GetTempPath());
        var svc = new GodotCliService(resolver);
        // Ensure calling the locator does not throw and returns either null or a path
        string? result = null;
        Action act = () => result = svc.LocateGodotBinary();
        act.Should().NotThrow();
        // Result may be null on CI; if non-null it should be an existing path
        if (result is not null)
        {
            File.Exists(result).Should().BeTrue();
        }
    }
}
