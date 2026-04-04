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

public sealed record ToolResult(
    bool Success,
    string Message,
    object? Data = null,
    string? SuggestedRemediation = null);
