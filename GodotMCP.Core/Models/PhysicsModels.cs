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
/// Mutation result for shape/collision operations.
/// </summary>
public sealed record PhysicsShapeMutationResult(
    bool Success,
    string Message,
    string? ScenePath = null,
    string? ShapeNodePath = null);

/// <summary>
/// Creates a collision shape node under a body/area node.
/// </summary>
public sealed record PhysicsAddShapeRequest(
    string ScenePath,
    string BodyNodePath,
    string ShapeNodeType,
    string ShapeNodeName,
    string ShapeKind,
    IReadOnlyDictionary<string, object?> ShapeParameters,
    IReadOnlyDictionary<string, object?> NodeProperties);

/// <summary>
/// Updates shape-related properties on a collision shape node.
/// </summary>
public sealed record PhysicsUpdateShapeRequest(
    string ScenePath,
    string ShapeNodePath,
    IReadOnlyDictionary<string, object?> ShapeParameters,
    IReadOnlyDictionary<string, object?> NodeProperties);

/// <summary>
/// Removes a collision shape node.
/// </summary>
public sealed record PhysicsRemoveShapeRequest(
    string ScenePath,
    string ShapeNodePath);

/// <summary>
/// Creates a collision polygon node.
/// </summary>
public sealed record PhysicsAddCollisionPolygonRequest(
    string ScenePath,
    string BodyNodePath,
    string PolygonNodeType,
    string PolygonNodeName,
    string PolygonData,
    IReadOnlyDictionary<string, object?> NodeProperties);

/// <summary>
/// Updates a collision polygon node.
/// </summary>
public sealed record PhysicsUpdateCollisionPolygonRequest(
    string ScenePath,
    string PolygonNodePath,
    string? PolygonData,
    IReadOnlyDictionary<string, object?> NodeProperties);

/// <summary>
/// Removes a collision polygon node.
/// </summary>
public sealed record PhysicsRemoveCollisionPolygonRequest(
    string ScenePath,
    string PolygonNodePath);

/// <summary>
/// Assigns a shape resource expression to a collision shape node.
/// </summary>
public sealed record PhysicsAssignShapeResourceRequest(
    string ScenePath,
    string ShapeNodePath,
    string ShapeExpression);

/// <summary>
/// Sets shape/collision flags on a shape or polygon node.
/// </summary>
public sealed record PhysicsSetShapeFlagsRequest(
    string ScenePath,
    string ShapeNodePath,
    bool? Disabled,
    bool? OneWayCollision,
    double? OneWayCollisionMargin,
    bool? PlatformOnLeave);

/// <summary>
/// Updates monitoring flags on an Area2D/Area3D node.
/// </summary>
public sealed record PhysicsAreaSetMonitoringRequest(
    string ScenePath,
    string AreaNodePath,
    bool Monitoring,
    bool Monitorable);

/// <summary>
/// Updates priority on an Area2D/Area3D node.
/// </summary>
public sealed record PhysicsAreaSetPriorityRequest(
    string ScenePath,
    string AreaNodePath,
    double Priority);

/// <summary>
/// Updates space override mode and optional gravity/damping overrides on an Area2D/Area3D node.
/// </summary>
public sealed record PhysicsAreaSetSpaceOverrideRequest(
    string ScenePath,
    string AreaNodePath,
    string SpaceOverrideMode,
    double? Gravity = null,
    double? GravityPointUnitDistance = null,
    double? LinearDamp = null,
    double? AngularDamp = null);

/// <summary>
/// Updates collision filter masks on an Area2D/Area3D node.
/// </summary>
public sealed record PhysicsAreaSetCollisionFiltersRequest(
    string ScenePath,
    string AreaNodePath,
    int CollisionLayer,
    int CollisionMask);

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
