using GodotMCP.Core.Models;

namespace GodotMCP.Core.Interfaces;

/// <summary>
/// Executes Godot CLI commands in a project context.
/// </summary>
public interface IGodotCliService
{
    /// <summary>
    /// Runs a Godot CLI command.
    /// </summary>
    /// <param name="arguments">Raw command arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing execution outcome.</returns>
    Task<ToolResult> RunAsync(string arguments, CancellationToken cancellationToken = default);
}
