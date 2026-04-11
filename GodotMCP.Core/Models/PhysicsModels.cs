namespace GodotMCP.Core.Models;

/// <summary>
/// Represents a physics body discovered in a scene.
/// </summary>
/// <param name="ScenePath">Scene path containing the body.</param>
/// <param name="NodePath">Resolved node path for the body.</param>
/// <param name="Type">Physics body type.</param>
/// <param name="CollisionLayer">Collision layer bitmask value when available.</param>
/// <param name="CollisionMask">Collision mask bitmask value when available.</param>
/// <param name="GravityScale">Gravity scale when available.</param>
/// <param name="LockRotation">Whether rotation is locked when available.</param>
public sealed record PhysicsBodyInfo(
    string ScenePath,
    string NodePath,
    string Type,
    int? CollisionLayer,
    int? CollisionMask,
    double? GravityScale,
    bool? LockRotation);

/// <summary>
/// Input contract for creating a physics body.
/// </summary>
/// <param name="ScenePath">Scene path to mutate.</param>
/// <param name="ParentNodePath">Parent node path where the body is created.</param>
/// <param name="NodeName">Body node name.</param>
/// <param name="BodyType">Body node type.</param>
/// <param name="AddCollisionShape">Whether to create a collision shape child automatically.</param>
public sealed record PhysicsCreateBodyRequest(
    string ScenePath,
    string ParentNodePath,
    string NodeName,
    string BodyType,
    bool AddCollisionShape = true);

/// <summary>
/// Input contract for updating selected physics body properties.
/// </summary>
/// <param name="ScenePath">Scene path to mutate.</param>
/// <param name="NodePath">Target body node path.</param>
/// <param name="Properties">Property updates to apply.</param>
public sealed record PhysicsUpdateBodyRequest(
    string ScenePath,
    string NodePath,
    IReadOnlyDictionary<string, object?> Properties);

/// <summary>
/// Represents the result of a physics mutation operation.
/// </summary>
/// <param name="Success">Indicates whether the operation succeeded.</param>
/// <param name="Message">Human-readable status message.</param>
/// <param name="Body">Optional body snapshot after mutation.</param>
public sealed record PhysicsMutationResult(
    bool Success,
    string Message,
    PhysicsBodyInfo? Body = null);

/// <summary>
/// Represents a lint-style physics validation issue.
/// </summary>
/// <param name="Path">Primary path associated with the issue.</param>
/// <param name="Severity">Issue severity.</param>
/// <param name="Message">Issue description.</param>
/// <param name="SuggestedFix">Suggested remediation.</param>
/// <param name="Rule">Validation rule identifier.</param>
/// <param name="ScenePath">Related scene path.</param>
/// <param name="NodePath">Related body node path.</param>
public sealed record PhysicsValidationIssue(
    string Path,
    string Severity,
    string Message,
    string? SuggestedFix = null,
    string? Rule = null,
    string? ScenePath = null,
    string? NodePath = null);
