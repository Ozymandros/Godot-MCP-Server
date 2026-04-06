namespace GodotMCP.Core.Models;

/// <summary>
/// Represents a light node discovered in a scene.
/// </summary>
/// <param name="ScenePath">Scene path containing the light.</param>
/// <param name="NodePath">Resolved node path in the scene hierarchy.</param>
/// <param name="Type">Light node type.</param>
/// <param name="Energy">Light energy/intensity when present.</param>
/// <param name="Color">Light color value when present.</param>
/// <param name="ShadowsEnabled">Whether shadows are enabled when present.</param>
public sealed record LightNodeInfo(
    string ScenePath,
    string NodePath,
    string Type,
    double? Energy,
    string? Color,
    bool? ShadowsEnabled);

/// <summary>
/// Input contract for creating a light node.
/// </summary>
/// <param name="ScenePath">Scene path to mutate.</param>
/// <param name="ParentNodePath">Parent node path where light is created.</param>
/// <param name="NodeName">Light node name.</param>
/// <param name="LightType">Light node type.</param>
/// <param name="Preset">Optional preset name.</param>
public sealed record LightCreateRequest(
    string ScenePath,
    string ParentNodePath,
    string NodeName,
    string LightType,
    string? Preset);

/// <summary>
/// Input contract for updating a light node.
/// </summary>
/// <param name="ScenePath">Scene path to mutate.</param>
/// <param name="NodePath">Light node path to update.</param>
/// <param name="Properties">Property updates to apply.</param>
public sealed record LightUpdateRequest(
    string ScenePath,
    string NodePath,
    IReadOnlyDictionary<string, object?> Properties);

/// <summary>
/// Represents the result of a lighting mutation operation.
/// </summary>
/// <param name="Success">Indicates whether the operation succeeded.</param>
/// <param name="Message">Human-readable status message.</param>
/// <param name="Light">Optional light snapshot after mutation.</param>
public sealed record LightMutationResult(
    bool Success,
    string Message,
    LightNodeInfo? Light = null);

/// <summary>
/// Represents a lint-style lighting validation issue.
/// </summary>
/// <param name="Path">Primary path associated with the issue.</param>
/// <param name="Severity">Issue severity.</param>
/// <param name="Message">Issue description.</param>
/// <param name="SuggestedFix">Suggested remediation text.</param>
/// <param name="Rule">Validation rule identifier.</param>
/// <param name="ScenePath">Related scene path.</param>
/// <param name="NodePath">Related node path.</param>
public sealed record LightValidationIssue(
    string Path,
    string Severity,
    string Message,
    string? SuggestedFix = null,
    string? Rule = null,
    string? ScenePath = null,
    string? NodePath = null);
