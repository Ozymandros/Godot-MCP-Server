using GodotMCP.Core.Models;

namespace GodotMCP.Core.Interfaces;

/// <summary>
/// Provides a simple mechanism to invoke bundled Godot GDScript operations.
/// Implementations extract an embedded GDScript, write a payload file, invoke
/// the Godot CLI to run the script, and return the execution result.
/// </summary>
public interface IGodotOperationsRunner
{
    /// <summary>
    /// Run a named operation with a JSON payload. The bundled script may decide
    /// how to interpret operation names and payload content.
    /// </summary>
    Task<ToolResult> RunOperationAsync(string operationName, string payloadJson, CancellationToken cancellationToken = default);
}
