using GodotMCP.Core.Models;

namespace GodotMCP.Core.Interfaces;

public interface IGodotCliService
{
    Task<ToolResult> RunAsync(string arguments, CancellationToken cancellationToken = default);
}
