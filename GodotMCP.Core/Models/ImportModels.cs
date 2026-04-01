namespace GodotMCP.Core.Models;

/// <summary>
/// Models used for import file generation and integration metadata.
/// </summary>
public sealed class ImportFileModel
{
    /// <summary>
    /// Path to the asset within the project (eg. <c>res://assets/sprite.png</c>).
    /// </summary>
    public required string AssetPath { get; set; }

    /// <summary>
    /// Importer id (for example "texture" or "wav").
    /// </summary>
    public required string Importer { get; set; }

    /// <summary>
    /// Target resource type (for example "Texture2D").
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Optional importer parameters serialized as key/value pairs.
    /// </summary>
    public Dictionary<string, string> Parameters { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Metadata describing an integration/plugin discovered in the project.
/// Contains identifying information, compatibility details and maintenance status.
/// </summary>
public sealed class IntegrationMetadata
{
    /// <summary>Name of the integration/plugin.</summary>
    public required string Name { get; set; }
    /// <summary>Profile describing how the integration should be installed.</summary>
    public required IntegrationProfile Profile { get; set; }
    /// <summary>Source URL or identifier for the integration.</summary>
    public required string Source { get; set; }
    /// <summary>Supported Godot version range (semver-ish string).</summary>
    public required string GodotVersionRange { get; set; }
    /// <summary>Platforms supported by the integration (comma-separated).</summary>
    public required string PlatformSupport { get; set; }
    /// <summary>Whether the integration is actively maintained.</summary>
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
