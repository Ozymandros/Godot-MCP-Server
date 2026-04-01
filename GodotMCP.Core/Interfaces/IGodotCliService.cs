using GodotMCP.Core.Models;

namespace GodotMCP.Core.Interfaces;

/// <summary>
/// Executes the Godot command-line executable and returns a <see cref="ToolResult"/>
/// containing captured stdout/stderr and execution status.
/// </summary>
public interface IGodotCliService
{
    /// <summary>
    /// Run the Godot CLI with the given arguments.
    /// </summary>
    Task<ToolResult> RunAsync(string arguments, CancellationToken cancellationToken = default);
}
