using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace GodotMCP.Tests.TestIsolation;

// Assembly initializer that creates an isolated temporary working directory for the
// entire test assembly and adjusts common environment variables so tests do not
// interact with the developer's real home / program files locations. The temp
// directory is removed on process exit. This keeps tests file-system effects
// contained and avoids manual cleanup in most cases.
internal static class AssemblyStartup
{
    private static readonly string TempRoot = Path.Combine(Path.GetTempPath(), "godotmcp-tests-" + Guid.NewGuid().ToString("N"));
    private static int sequence;

    public static string Root => TempRoot;

    public static string CreateSandboxDirectory(string prefix)
    {
        var id = System.Threading.Interlocked.Increment(ref sequence);
        var folder = Path.Combine(TempRoot, $"{prefix}-{id:D6}");
        Directory.CreateDirectory(folder);
        return folder;
    }

    [ModuleInitializer]
    public static void Initialize()
    {
        try
        {
            Directory.CreateDirectory(TempRoot);

            // Make test execution use the temporary folder as the current directory
            Environment.CurrentDirectory = TempRoot;

            // Ensure user-like folders point inside the temp root so code that queries
            // Environment.SpecialFolder or HOME/USERPROFILE will operate inside a sandbox.
            Environment.SetEnvironmentVariable("HOME", TempRoot);
            Environment.SetEnvironmentVariable("USERPROFILE", TempRoot);

            // Provide a ProgramFiles-like folder inside the temp root for Windows-specific tests
            var programFiles = Path.Combine(TempRoot, "ProgramFiles");
            Directory.CreateDirectory(programFiles);
            Environment.SetEnvironmentVariable("ProgramFiles", programFiles);

            // Avoid using any system-installed Godot by clearing GODOT_PATH for the test run.
            // Individual tests that need to exercise GODOT_PATH can set it explicitly via their
            // TestableSystemService or environment helpers.
            Environment.SetEnvironmentVariable("GODOT_PATH", null);

            // Register cleanup on process exit
            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                try
                {
                    if (Directory.Exists(TempRoot))
                        Directory.Delete(TempRoot, true);
                }
                catch
                {
                    // best-effort cleanup
                }
            };
        }
        catch
        {
            // If initialization fails, do not throw — fall back to default behavior.
        }
    }
}
