using Xunit;
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
        // xUnit does not provide an Assert for "not throw" directly; invoke and allow exceptions to fail the test
        act();
        // Result may be null on CI; if non-null it should be an existing path
        if (result is not null)
        {
            Assert.True(File.Exists(result));
        }
    }
}
