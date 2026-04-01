using Xunit;
using System.Runtime.InteropServices;
using GodotMCP.Infrastructure.Process;
using GodotMCP.Infrastructure.Services;
using GodotMCP.Tests.TestIsolation;

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
        var root = AssemblyStartup.CreateSandboxDirectory("godot-locator-env");
        var fakeExe = Path.Combine(root, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "godot.exe" : "godot");
        File.WriteAllText(fakeExe, "fake");
        var original = Environment.GetEnvironmentVariable("GODOT_PATH");
        try
        {
            Environment.SetEnvironmentVariable("GODOT_PATH", fakeExe);
            var resolver = new PathResolver(root);
            var svc = new GodotCliService(resolver);
            var found = svc.LocateGodotBinary();
            Assert.NotNull(found);
            Assert.Equal(Path.GetFullPath(fakeExe), Path.GetFullPath(found!));
        }
        finally { Environment.SetEnvironmentVariable("GODOT_PATH", original); }
    }

    /// <summary>
    /// When a godot binary is present on PATH, the locator should find it.
    /// This test prepends a temporary directory on PATH containing a fake binary.
    /// </summary>
    [Fact]
    public void LocateGodotBinary_FindsOnPathCandidate()
    {
        var root = AssemblyStartup.CreateSandboxDirectory("godot-locator-path");
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
            Assert.NotNull(found);
            Assert.StartsWith(Path.GetFullPath(root), Path.GetFullPath(found!));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }
}
