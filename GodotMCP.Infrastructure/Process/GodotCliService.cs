using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GodotMCP.Infrastructure.Process;

/// <summary>
/// Provides operations to run the Godot executable as a CLI tool.
/// </summary>
/// <remarks>
/// The service locates a Godot binary (preferring the <c>GODOT_PATH</c> environment
/// variable and falling back to common executable names on <c>PATH</c>) and
/// executes it with the provided arguments. Standard output and error are
/// captured and returned via <see cref="GodotMCP.Core.Models.ToolResult"/>.
/// </remarks>
public sealed class GodotCliService(IPathResolver pathResolver, ISystemService? system = null) : IGodotCliService
{
    private readonly ISystemService systemService = system ?? new DefaultSystemService();
    // Attempts to find a Godot executable. Preference order:
    // 1. $GODOT_PATH env var
    // 2. common binaries on PATH (godot, Godot, godot4)
    /// <summary>
    /// Locate a suitable Godot executable on the system.
    /// </summary>
    /// <returns>The path to the Godot executable if found; otherwise <c>null</c>.</returns>
    public string? LocateGodotBinary()
    {
        // 1) Environment override
        var env = systemService.GetEnvironmentVariable("GODOT_PATH");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
            return env;

        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        // 2) Try common PATH candidates first using the injectable system service
        var pathCandidates = new[] { "godot", "Godot", "godot4" };
        try
        {
            var pathEnv = systemService.GetEnvironmentVariable("PATH") ?? Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var pathEntries = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            foreach (var name in pathCandidates)
            {
                var fileName = name + (isWindows ? ".exe" : string.Empty);
                foreach (var p in pathEntries)
                {
                    try
                    {
                        var candidate = systemService.Combine(p, fileName);
                        if (systemService.FileExists(candidate)) return candidate;
                    }
                    catch { }
                }

                if (!isWindows)
                {
                    var which = TryWhichCommand(name);
                    if (!string.IsNullOrEmpty(which)) return which;
                }
            }
        }
        catch { }

        // 3) Platform-specific common install locations
        if (isWindows)
        {
            var programFiles = systemService.GetEnvironmentVariable("ProgramFiles")
                ?? systemService.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrEmpty(programFiles))
            {
                // Common installation folders
                var candidates = new[]
                {
                    systemService.Combine(programFiles, "Godot", "Godot.exe"),
                    systemService.Combine(programFiles, "Godot Engine", "Godot.exe"),
                    systemService.Combine(programFiles, "Godot Engine", "godot.exe")
                };

                foreach (var c in candidates)
                {
                    try { if (systemService.FileExists(c)) return c; } catch { }
                }

                // Also check ProgramFiles(x86)
                var programFilesX86 = systemService.GetEnvironmentVariable("ProgramFiles(x86)");
                if (!string.IsNullOrEmpty(programFilesX86))
                {
                    foreach (var c in new[] { systemService.Combine(programFilesX86, "Godot", "Godot.exe") })
                    {
                        try { if (systemService.FileExists(c)) return c; } catch { }
                    }
                }
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Check /Applications and user Applications for Godot.app bundles
            var macLocations = new[] { "/Applications", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Applications") };
            foreach (var loc in macLocations)
            {
                try
                {
                    if (!Directory.Exists(loc)) continue;
                    foreach (var app in Directory.EnumerateDirectories(loc, "Godot*.app", SearchOption.TopDirectoryOnly))
                    {
                        var exe = Path.Combine(app, "Contents", "MacOS");
                        if (Directory.Exists(exe))
                        {
                            foreach (var file in Directory.EnumerateFiles(exe))
                            {
                                if (File.Exists(file)) return file;
                            }
                        }
                    }
                }
                catch { }
            }
        }
        else
        {
            // Linux / Unix-like
            var linuxCandidates = new[] { "/usr/bin/godot", "/usr/local/bin/godot", "/snap/bin/godot", "/opt/godot/bin/godot" };
            foreach (var c in linuxCandidates)
            {
                try { if (systemService.FileExists(c)) return c; } catch { }
            }

            var home = systemService.GetEnvironmentVariable("HOME") ?? systemService.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home))
            {
                try
                {
                    var userHub = systemService.Combine(home, "Godot", "Hub", "Editor");
                    if (systemService.DirectoryExists(userHub))
                    {
                        foreach (var version in systemService.EnumerateDirectories(userHub, "*", SearchOption.TopDirectoryOnly).OrderByDescending(d => d))
                        {
                            var exe = systemService.Combine(version, "Godot");
                            if (systemService.FileExists(exe)) return exe;
                        }
                    }
                }
                catch { }
            }
        }

        return null;
    }

    private static void TryEnsureExecutable(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"+x \"{path}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = System.Diagnostics.Process.Start(psi);
                p?.WaitForExit(500);
            }
        }
        catch
        {
            // best-effort
        }
    }

    /// <summary>
    /// Performs a simple lookup for <paramref name="fileName"/> on the <c>PATH</c>.
    /// </summary>
    /// <param name="fileName">File name to locate (may include extension).</param>
    /// <returns>Full path to the file if found; otherwise <c>null</c>.</returns>
    private static string? Which(string fileName)
    {
        // Simple PATH lookup
        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? Array.Empty<string>();
        foreach (var p in paths)
        {
            try
            {
                var candidate = Path.Combine(p, fileName);
                if (File.Exists(candidate)) return candidate;
            }
            catch
            {
                // ignore invalid path entries
            }
        }
        ;

        return null;
    }

    private static string? TryWhichCommand(string name)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "which",
                Arguments = name,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = System.Diagnostics.Process.Start(psi);
            if (p is null) return null;
            var outp = p.StandardOutput.ReadToEnd();
            p.WaitForExit(1500);
            outp = outp?.Trim();
            if (!string.IsNullOrEmpty(outp) && File.Exists(outp)) return outp;
        }
        catch { }

        return null;
    }

    /// <summary>
    /// Executes the Godot CLI with the given <paramref name="arguments"/> and
    /// captures standard output and error.
    /// </summary>
    /// <param name="arguments">Arguments to pass to the Godot executable (e.g. <c>--version</c>).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="ToolResult"/> containing execution status and captured output.</returns>
    public async Task<ToolResult> RunAsync(string arguments, CancellationToken cancellationToken = default)
    {
        var godotBinary = LocateGodotBinary();
        if (string.IsNullOrWhiteSpace(godotBinary))
        {
            var tried = Environment.GetEnvironmentVariable("GODOT_PATH") is null ? "GODOT_PATH + common PATH candidates" : "GODOT_PATH";
            return new ToolResult(false, "Godot executable not found.", Data: new Dictionary<string, string>
            {
                ["tried"] = tried
            }, SuggestedRemediation: "Set GODOT_PATH to your Godot 4.x executable or ensure 'godot' is on PATH.");
        }
        ;

        // Ensure the binary is executable on Unix-like systems (chmod +x)
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                TryEnsureExecutable(godotBinary);
            }
        }
        catch { }

        var psi = new ProcessStartInfo
        {
            FileName = godotBinary,
            Arguments = arguments,
            WorkingDirectory = pathResolver.ProjectRoot,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            UseShellExecute = false
        };

        using var process = new System.Diagnostics.Process { StartInfo = psi };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"Failed to start Godot process: {ex.Message}");
        }

        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var output = await stdOutTask.ConfigureAwait(false);
        var error = await stdErrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
            return new ToolResult(false, $"Godot CLI failed with code {process.ExitCode}.", new Dictionary<string, string>
            {
                ["stdout"] = output,
                ["stderr"] = error
            });

        return new ToolResult(true, "Godot CLI command completed.", new Dictionary<string, string>
        {
            ["stdout"] = output,
            ["stderr"] = error
        });
    }
}
