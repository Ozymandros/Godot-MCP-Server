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
public sealed class GodotCliService(IPathResolver pathResolver) : IGodotCliService
{
    // Attempts to find a Godot executable. Preference order:
    // 1. $GODOT_PATH env var
    // 2. common binaries on PATH (godot, Godot, godot4)
    /// <summary>
    /// Locate a suitable Godot executable on the system.
    /// </summary>
    /// <returns>The path to the Godot executable if found; otherwise <c>null</c>.</returns>
    private string? LocateGodotBinary()
    {
        var env = Environment.GetEnvironmentVariable("GODOT_PATH");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env;
        }

        // Candidate names to try on PATH
        var candidates = new[] { "godot", "Godot", "godot4" };
        // On Windows try .exe suffix if necessary
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        foreach (var name in candidates)
        {
            var fileName = name + (isWindows ? ".exe" : string.Empty);
            try
            {
                var full = Which(fileName);
                if (!string.IsNullOrEmpty(full)) return full;
            }
            catch
            {
                // ignore and try next
            }
        }

        return null;
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
            return new ToolResult(false, "Godot executable not found.", Data: new Dictionary<string,string>
            {
                ["tried"] = tried
            }, SuggestedRemediation: "Set GODOT_PATH to your Godot 4.x executable or ensure 'godot' is on PATH.");
        }

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
        {
            return new ToolResult(false, $"Godot CLI failed with code {process.ExitCode}.", new Dictionary<string,string>
            {
                ["stdout"] = output,
                ["stderr"] = error
            });
        }

        return new ToolResult(true, "Godot CLI command completed.", new Dictionary<string, string>
        {
            ["stdout"] = output,
            ["stderr"] = error
        });
    }
}
