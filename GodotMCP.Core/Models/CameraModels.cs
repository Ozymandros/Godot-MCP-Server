namespace GodotMCP.Core.Models;

/// <summary>
/// Represents supported camera node kinds in scenes.
/// </summary>
public enum CameraNodeType
{
    /// <summary>
    /// A 2D camera node.
    /// </summary>
    Camera2D,

    /// <summary>
    /// A 3D camera node.
    /// </summary>
    Camera3D
}

/// <summary>
/// Represents known camera projection modes.
/// </summary>
public enum CameraProjection
{
    /// <summary>
    /// Perspective projection.
    /// </summary>
    Perspective = 0,

    /// <summary>
    /// Orthographic projection.
    /// </summary>
    Orthographic = 1,

    /// <summary>
    /// Frustum projection.
    /// </summary>
    Frustum = 2,

    /// <summary>
    /// Projection mode that is missing or not supported.
    /// </summary>
    Unsupported = -1
}

/// <summary>
/// Describes a camera node discovered in a scene file.
/// </summary>
/// <param name="ScenePath">Scene path containing the camera node.</param>
/// <param name="NodePath">Resolved node path inside the scene.</param>
/// <param name="Type">Camera node type.</param>
/// <param name="Fov">Camera field of view when available.</param>
/// <param name="Size">Camera orthographic size when available.</param>
/// <param name="Near">Near clipping plane distance when available.</param>
/// <param name="Far">Far clipping plane distance when available.</param>
/// <param name="Projection">Projection mode.</param>
/// <param name="Current">Whether the camera is currently active.</param>
public sealed record CameraNodeInfo(
    string ScenePath,
    string NodePath,
    CameraNodeType Type,
    double? Fov,
    double? Size,
    double? Near,
    double? Far,
    CameraProjection Projection,
    bool Current);

/// <summary>
/// Input contract for creating a camera in a scene.
/// </summary>
/// <param name="ScenePath">Scene path where the camera will be created.</param>
/// <param name="NodePath">Target node path for the new camera.</param>
/// <param name="Type">Camera node type to create.</param>
/// <param name="Preset">Optional preset name applied after creation.</param>
public sealed record CameraCreateRequest(
    string ScenePath,
    string NodePath,
    CameraNodeType Type,
    string? Preset);

/// <summary>
/// Input contract for updating camera properties in a scene.
/// </summary>
/// <param name="ScenePath">Scene path containing the target camera.</param>
/// <param name="NodePath">Node path identifying the target camera.</param>
/// <param name="Properties">Property map with only the values to update.</param>
public sealed record CameraUpdateRequest(
    string ScenePath,
    string NodePath,
    IReadOnlyDictionary<string, object?> Properties);

/// <summary>
/// Represents a camera mutation outcome.
/// </summary>
/// <param name="Success">Indicates whether the mutation succeeded.</param>
/// <param name="Message">Human-readable operation result message.</param>
/// <param name="Camera">Updated camera snapshot when available.</param>
public sealed record CameraMutationResult(
    bool Success,
    string Message,
    CameraNodeInfo? Camera = null);

/// <summary>
/// Represents a lint-style camera validation issue.
/// </summary>
/// <param name="Path">Primary path associated with the issue.</param>
/// <param name="Severity">Issue severity level.</param>
/// <param name="Message">Issue description.</param>
/// <param name="SuggestedFix">Optional remediation guidance.</param>
/// <param name="Rule">Optional validation rule identifier.</param>
/// <param name="ScenePath">Optional scene path associated with the issue.</param>
/// <param name="NodePath">Optional node path associated with the issue.</param>
public sealed record CameraValidationIssue(
    string Path,
    string Severity,
    string Message,
    string? SuggestedFix = null,
    string? Rule = null,
    string? ScenePath = null,
    string? NodePath = null);