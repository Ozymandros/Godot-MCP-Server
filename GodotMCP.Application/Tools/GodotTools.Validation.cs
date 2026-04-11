using System.Text.Json;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    /// <summary>
    /// Checks whether a value is null, empty, or whitespace.
    /// </summary>
    /// <param name="value">Value to inspect.</param>
    /// <returns><see langword="true"/> when value is blank.</returns>
    private static bool IsBlank(string? value) => string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// Creates a failed <see cref="ToolResult"/> with optional remediation guidance.
    /// </summary>
    /// <param name="message">Error message for the tool response.</param>
    /// <param name="remediation">Optional remediation text.</param>
    /// <returns>Failed tool result payload.</returns>
    private static ToolResult Invalid(string message, string? remediation = null)
        => new(false, message, SuggestedRemediation: remediation);

    /// <summary>
    /// Validates that a path can be resolved as a project-scoped <c>res://</c> path.
    /// </summary>
    /// <param name="pathResolver">Path resolver scoped to the current project.</param>
    /// <param name="path">Path to validate.</param>
    /// <returns><see langword="true"/> when path is valid and project-scoped.</returns>
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

    /// <summary>
    /// Converts a JSON element into a supported primitive CLR value.
    /// </summary>
    /// <param name="value">JSON value to convert.</param>
    /// <returns>Primitive CLR value when supported; otherwise, <see langword="null"/>.</returns>
    private static object? ToPrimitiveValue(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number when value.TryGetInt64(out var i) => i,
            JsonValueKind.Number when value.TryGetDouble(out var d) => d,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
}
