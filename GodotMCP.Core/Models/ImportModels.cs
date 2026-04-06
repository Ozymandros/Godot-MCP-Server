namespace GodotMCP.Core.Models;

/// <summary>
/// Represents the data required to generate a Godot <c>.import</c> file.
/// </summary>
public sealed class ImportFileModel
{
    /// <summary>
    /// Gets or sets target asset path.
    /// </summary>
    public required string AssetPath { get; set; }

    /// <summary>
    /// Gets or sets importer identifier.
    /// </summary>
    public required string Importer { get; set; }

    /// <summary>
    /// Gets or sets output resource type.
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Gets optional importer parameters.
    /// </summary>
    public Dictionary<string, string> Parameters { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Describes discovered integration metadata for SDK ecosystem tooling.
/// </summary>
public sealed class IntegrationMetadata
{
    /// <summary>
    /// Gets or sets integration name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets integration profile.
    /// </summary>
    public required IntegrationProfile Profile { get; set; }

    /// <summary>
    /// Gets or sets integration source path or URL.
    /// </summary>
    public required string Source { get; set; }

    /// <summary>
    /// Gets or sets supported Godot version range.
    /// </summary>
    public required string GodotVersionRange { get; set; }

    /// <summary>
    /// Gets or sets supported platform descriptor.
    /// </summary>
    public required string PlatformSupport { get; set; }

    /// <summary>
    /// Gets or sets whether the integration is maintained.
    /// </summary>
    public bool IsMaintained { get; set; }
}

/// <summary>
/// Standard transport result for MCP tool commands.
/// </summary>
/// <param name="Success">Whether command execution succeeded.</param>
/// <param name="Message">Human-readable status message.</param>
/// <param name="Data">Optional command payload.</param>
/// <param name="SuggestedRemediation">Optional remediation guidance for failures.</param>
public sealed record ToolResult(
    bool Success,
    string Message,
    object? Data = null,
    string? SuggestedRemediation = null);
