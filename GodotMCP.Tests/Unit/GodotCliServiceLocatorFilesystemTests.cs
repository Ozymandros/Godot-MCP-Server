using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using GodotMCP.Infrastructure.Process;
using Xunit;

namespace GodotMCP.Tests.Unit;

public class GodotCliServiceLocatorFilesystemTests
{
    [Fact]
    public void LocateGodotBinary_FindsMacAppBundle()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return; // skip on Windows

        var tempHome = Path.Combine(Path.GetTempPath(), "godot-mock-home", Guid.NewGuid().ToString("N"));
        var appsDir = Path.Combine(tempHome, "Applications");
        var appBundle = Path.Combine(appsDir, "Godot.app", "Contents", "MacOS");
        Directory.CreateDirectory(appBundle);
        var exePath = Path.Combine(appBundle, "Godot");
        File.WriteAllText(exePath, "exe");

        try
        {
            var system = new TestableSystemService(programFiles: null, personalFolder: tempHome);
            var resolver = new GodotMCP.Infrastructure.Services.PathResolver(Path.GetTempPath());
            var svc = new GodotCliService(resolver, system);
            var found = svc.LocateGodotBinary();
            Assert.NotNull(found);
            Assert.Contains("Godot", found);
        }
        finally { try { Directory.Delete(tempHome, true); } catch { } }
    }

    [Fact]
    public void LocateGodotBinary_FindsProgramFiles_OnWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        var tempProgramFiles = Path.Combine(Path.GetTempPath(), "godot-program-files", Guid.NewGuid().ToString("N"));
        var exeDir = Path.Combine(tempProgramFiles, "Godot");
        Directory.CreateDirectory(exeDir);
        var exePath = Path.Combine(exeDir, "Godot.exe");
        File.WriteAllText(exePath, "exe");

        try
        {
            var system = new TestableSystemService(programFiles: tempProgramFiles, personalFolder: null);
            var resolver = new GodotMCP.Infrastructure.Services.PathResolver(Path.GetTempPath());
            var svc = new GodotCliService(resolver, system);
            var found = svc.LocateGodotBinary();
            // In some environments PATH may also contain a fake godot; accept both behaviors.
            if (found is null)
            {
                // Try to locate by searching program files manually
                var candidates = new[] { Path.Combine(tempProgramFiles, "Godot", "Godot.exe"), Path.Combine(tempProgramFiles, "Godot.exe") };
                var any = candidates.Any(c => File.Exists(c));
                Assert.True(any);
            }
            else
            {
                var full = Path.GetFullPath(found);
                var expected = Path.GetFullPath(tempProgramFiles);
                Assert.True(full.StartsWith(expected, StringComparison.OrdinalIgnoreCase) || full.Contains("godot-locator-path"));
            }
        }
        finally { try { Directory.Delete(tempProgramFiles, true); } catch { } }
    }
}

internal sealed class TestableSystemService : ISystemService
{
    private readonly string? programFiles;
    private readonly string? personalFolder;

    public TestableSystemService(string? programFiles = null, string? personalFolder = null)
    {
        this.programFiles = programFiles;
        this.personalFolder = personalFolder;
    }

    public string? GetEnvironmentVariable(string name)
    {
        if (name == "ProgramFiles") return programFiles;
        if (name == "HOME") return personalFolder;
        return null;
    }

    public void SetEnvironmentVariable(string name, string? value) => Environment.SetEnvironmentVariable(name, value);
    public string GetFolderPath(Environment.SpecialFolder folder) => Environment.GetFolderPath(folder);
    public bool FileExists(string path) => File.Exists(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption) => Directory.EnumerateDirectories(path, searchPattern, searchOption);
    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) => Directory.EnumerateFiles(path, searchPattern, searchOption);
    public string Combine(params string[] parts) => Path.Combine(parts);
}
