namespace GodotMCP.Core.Models;

/// <summary>
/// Represents a serialized Godot resource document.
/// </summary>
/// <param name="Type">Godot resource type name.</param>
/// <param name="Properties">Top-level resource property dictionary.</param>
public sealed record ResourceDocument(
    string Type,
    Dictionary<string, string> Properties)
{
    /// <summary>
    /// Gets optional external resource declarations for richer .tres/.res files.
    /// </summary>
    public List<ExtResource> ExternalResources { get; init; } = [];

    /// <summary>
    /// Gets optional sub-resource declarations (nested resources).
    /// </summary>
    public List<SubResource> SubResources { get; init; } = [];

    /// <summary>
    /// Gets or sets serialized format version (default 3).
    /// </summary>
    public int Format { get; init; } = 3;
}

/// <summary>
/// Represents the result of a resource property mutation operation.
/// </summary>
/// <param name="Success">Indicates whether the operation succeeded.</param>
/// <param name="Message">Human-readable operation status.</param>
/// <param name="Properties">Updated property dictionary when available.</param>
public sealed record ResourcePropertyMutationResult(
    bool Success,
    string Message,
    Dictionary<string, string>? Properties = null);
