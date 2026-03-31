namespace GodotMCP.Core.Models;

public sealed class ImportFileModel
{
    public required string AssetPath { get; set; }
    public required string Importer { get; set; }
    public required string Type { get; set; }
    public Dictionary<string, string> Parameters { get; } = new(StringComparer.Ordinal);
}

public sealed class IntegrationMetadata
{
    public required string Name { get; set; }
    public required IntegrationProfile Profile { get; set; }
    public required string Source { get; set; }
    public required string GodotVersionRange { get; set; }
    public required string PlatformSupport { get; set; }
    public bool IsMaintained { get; set; }
}

/// <summary>
/// Standard result returned by tools and services exposed via MCP. Contains
/// a success flag, human-friendly message, optional data dictionary, and an
/// optional remediation suggestion for errors.
/// </summary>
/// <param name="Success">True when the operation succeeded.</param>
/// <param name="Message">Human readable message describing the outcome.</param>
/// <param name="Data">Optional key/value data returned by the operation.</param>
/// <param name="SuggestedRemediation">Short suggestion describing how to fix failures.</param>
public sealed record ToolResult(
    bool Success,
    string Message,
    Dictionary<string, string>? Data = null,
    string? SuggestedRemediation = null);
