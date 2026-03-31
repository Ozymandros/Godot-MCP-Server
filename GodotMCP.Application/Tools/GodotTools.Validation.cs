using GodotMCP.Core.Models;

namespace GodotMCP.Application.Tools;

public partial class GodotTools
{
    private static bool IsBlank(string? value) => string.IsNullOrWhiteSpace(value);

    private ToolResult Invalid(string message, string? remediation = null)
        => new(false, message, SuggestedRemediation: remediation);

    private bool IsValidResPath(string path)
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
