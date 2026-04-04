using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    private static bool IsBlank(string? value) => string.IsNullOrWhiteSpace(value);

    private static ToolResult Invalid(string message, string? remediation = null)
        => new(false, message, SuggestedRemediation: remediation);

    private static bool IsValidResPath(IPathResolver pathResolver, string path)
    {
        try
        {
            _ = pathResolver.ResolveResPath(path);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
