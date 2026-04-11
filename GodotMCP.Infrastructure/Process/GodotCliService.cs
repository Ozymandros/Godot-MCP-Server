using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;

namespace GodotMCP.Infrastructure.Process;

/// <summary>
/// Executes Godot headless CLI commands.
/// </summary>
/// <param name="pathResolver">Project path resolver used as working directory context.</param>
public sealed class GodotCliService(IPathResolver pathResolver) : IGodotCliService
{
    /// <inheritdoc />
    public async Task<ToolResult> RunAsync(string arguments, CancellationToken cancellationToken = default)
    {
        var godotBinary = Environment.GetEnvironmentVariable("GODOT_PATH");
        if (string.IsNullOrWhiteSpace(godotBinary))
        {
            return new ToolResult(false, "GODOT_PATH is not configured.", SuggestedRemediation: "Set GODOT_PATH to your Godot 4.x executable.");
        }

        var psi = new global::System.Diagnostics.ProcessStartInfo
        {
            FileName = godotBinary,
            Arguments = arguments,
            WorkingDirectory = pathResolver.ProjectRoot,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        using var process = new global::System.Diagnostics.Process { StartInfo = psi };
        process.Start();
        var stdOut = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var output = await stdOut.ConfigureAwait(false);
        var error = await stdErr.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            return new ToolResult(false, $"Godot CLI failed with code {process.ExitCode}: {error}");
        }

        return new ToolResult(true, "Godot CLI command completed.", new Dictionary<string, string>
        {
            ["stdout"] = output,
            ["stderr"] = error
        });
    }
}
