using FluentAssertions;
using System.Runtime.InteropServices;
using GodotMCP.Infrastructure.Process;
using GodotMCP.Infrastructure.Services;

namespace GodotMCP.Tests.Unit;

/// <summary>
/// Unit tests that exercise <see cref="GodotCliService.LocateGodotBinary"/> behavior
/// using environment and PATH manipulation. These tests are best-effort and restore
/// environment variables after execution.
/// </summary>
public class GodotCliServiceLocatorUnitTests
{
    /// <summary>
    /// When <c>GODOT_PATH</c> is set to an existing file, the locator should return it.
    /// </summary>
    [Fact]
    public void LocateGodotBinary_UsesGODOT_PATH_WhenSet()
    {
        var root = Path.Combine(Path.GetTempPath(), "godot-locator-env", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var fakeExe = Path.Combine(root, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "godot.exe" : "godot");
        File.WriteAllText(fakeExe, "fake");
        try
        {
            var original = Environment.GetEnvironmentVariable("GODOT_PATH");
            try
            {
                Environment.SetEnvironmentVariable("GODOT_PATH", fakeExe);
                var resolver = new PathResolver(root);
                var svc = new GodotCliService(resolver);
                var found = svc.LocateGodotBinary();
                found.Should().NotBeNull();
                Path.GetFullPath(found!).Should().Be(Path.GetFullPath(fakeExe));
            }
            finally
            {
                Environment.SetEnvironmentVariable("GODOT_PATH", original);
            }
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    /// <summary>
    /// When a godot binary is present on PATH, the locator should find it.
    /// This test prepends a temporary directory on PATH containing a fake binary.
    /// </summary>
    [Fact]
    public void LocateGodotBinary_FindsOnPathCandidate()
    {
        var root = Path.Combine(Path.GetTempPath(), "godot-locator-path", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var fname = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "godot.exe" : "godot";
        var fakeExe = Path.Combine(root, fname);
        File.WriteAllText(fakeExe, "fake");

        var originalPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        try
        {
            // Prepend our temp dir to PATH
            var newPath = root + Path.PathSeparator + originalPath;
            Environment.SetEnvironmentVariable("PATH", newPath);

            var resolver = new PathResolver(root);
            var svc = new GodotCliService(resolver);
            var found = svc.LocateGodotBinary();
            found.Should().NotBeNull();
            Path.GetFullPath(found!).Should().StartWith(Path.GetFullPath(root));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
