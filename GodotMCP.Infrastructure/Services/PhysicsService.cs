using System.Globalization;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;

namespace GodotMCP.Infrastructure.Services;

/// <summary>
/// Implements headless physics body operations on top of scene graph APIs.
/// </summary>
/// <param name="fileService">File abstraction for project I/O.</param>
/// <param name="pathResolver">Path resolver scoped to the current project.</param>
/// <param name="sceneGraphService">Scene graph service for listing and mutating nodes.</param>
public sealed class PhysicsService(
    IGodotFileService fileService,
    IPathResolver pathResolver,
    ISceneGraphService sceneGraphService) : IPhysicsService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<PhysicsBodyInfo>> ListAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        var rootDir = ServiceHelpers.NormalizeProjectDirectory(pathResolver, rootPath);
        var bodies = new List<PhysicsBodyInfo>();
        foreach (var absoluteScenePath in fileService.EnumerateFiles(rootDir, "*.tscn", recursive: true))
        {
            var nodes = await sceneGraphService.ListNodesAsync(absoluteScenePath, cancellationToken).ConfigureAwait(false);
            bodies.AddRange(ServiceHelpers.FlattenNodes(nodes)
                .Where(IsPhysicsBody)
                .Select(x => ToBodyInfo(absoluteScenePath, x)));
        }

        return bodies;
    }

    /// <inheritdoc />
    public async Task<PhysicsMutationResult> CreateBodyAsync(PhysicsCreateBodyRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsSupportedBodyType(request.BodyType))
        {
            return new PhysicsMutationResult(false, "Unsupported body type. Supported: StaticBody3D, RigidBody3D, CharacterBody3D, Area3D, StaticBody2D, RigidBody2D, CharacterBody2D, Area2D.");
        }

        var addBody = await sceneGraphService
            .AddNodeAsync(new SceneGraphAddNodeRequest(request.ScenePath, request.ParentNodePath, request.BodyType, request.NodeName), cancellationToken)
            .ConfigureAwait(false);
        if (!addBody.Success)
        {
            return new PhysicsMutationResult(false, addBody.Message);
        }

        var bodyPath = ResolveChildPath(request.ParentNodePath, request.NodeName);
        var defaultProperties = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["collision_layer"] = 1,
            ["collision_mask"] = 1
        };
        var init = await sceneGraphService
            .SetNodePropertiesAsync(new SceneGraphSetPropertiesRequest(request.ScenePath, bodyPath, defaultProperties), cancellationToken)
            .ConfigureAwait(false);
        if (!init.Success)
        {
            return new PhysicsMutationResult(false, init.Message);
        }

        if (request.AddCollisionShape)
        {
            var shapeType = request.BodyType.EndsWith("2D", StringComparison.Ordinal) ? "CollisionShape2D" : "CollisionShape3D";
            var addShape = await sceneGraphService
                .AddNodeAsync(new SceneGraphAddNodeRequest(request.ScenePath, bodyPath, shapeType, "CollisionShape"), cancellationToken)
                .ConfigureAwait(false);
            if (!addShape.Success)
            {
                return new PhysicsMutationResult(false, addShape.Message);
            }
        }

        var body = await FindBodyAsync(request.ScenePath, bodyPath, cancellationToken).ConfigureAwait(false);
        return new PhysicsMutationResult(true, $"Physics body '{request.NodeName}' created.", body);
    }

    /// <inheritdoc />
    public async Task<PhysicsMutationResult> UpdateBodyAsync(PhysicsUpdateBodyRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Properties.Count == 0)
        {
            return new PhysicsMutationResult(false, "properties must contain at least one entry.");
        }

        var validation = ValidateProperties(request.Properties);
        if (!validation.Success)
        {
            return validation;
        }

        var set = await sceneGraphService
            .SetNodePropertiesAsync(new SceneGraphSetPropertiesRequest(request.ScenePath, request.NodePath, request.Properties), cancellationToken)
            .ConfigureAwait(false);
        if (!set.Success)
        {
            return new PhysicsMutationResult(false, set.Message);
        }

        var body = await FindBodyAsync(request.ScenePath, request.NodePath, cancellationToken).ConfigureAwait(false);
        return new PhysicsMutationResult(true, "Physics body updated.", body);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PhysicsValidationIssue>> ValidateAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        var issues = new List<PhysicsValidationIssue>();
        var rootDir = ServiceHelpers.NormalizeProjectDirectory(pathResolver, rootPath);

        foreach (var absoluteScenePath in fileService.EnumerateFiles(rootDir, "*.tscn", recursive: true))
        {
            var roots = await sceneGraphService.ListNodesAsync(absoluteScenePath, cancellationToken).ConfigureAwait(false);
            var nodes = ServiceHelpers.FlattenNodes(roots).ToList();

            foreach (var node in nodes.Where(IsPhysicsBody))
            {
                var info = ToBodyInfo(absoluteScenePath, node);
                if (info.CollisionLayer is null or <= 0)
                {
                    issues.Add(new PhysicsValidationIssue(
                        absoluteScenePath,
                        "Warning",
                        $"Body '{info.NodePath}' has invalid collision_layer.",
                        "Set collision_layer to a positive bitmask value.",
                        "invalid-collision-layer",
                        absoluteScenePath,
                        info.NodePath));
                }

                if (info.CollisionMask is null or <= 0)
                {
                    issues.Add(new PhysicsValidationIssue(
                        absoluteScenePath,
                        "Warning",
                        $"Body '{info.NodePath}' has invalid collision_mask.",
                        "Set collision_mask to a positive bitmask value.",
                        "invalid-collision-mask",
                        absoluteScenePath,
                        info.NodePath));
                }

                if (RequiresCollisionShape(info.Type) && !HasCollisionShape(nodes, info.NodePath, info.Type))
                {
                    issues.Add(new PhysicsValidationIssue(
                        absoluteScenePath,
                        "Warning",
                        $"Body '{info.NodePath}' has no collision shape child.",
                        "Add a CollisionShape node under the body.",
                        "missing-collision-shape",
                        absoluteScenePath,
                        info.NodePath));
                }
            }
        }

        return issues;
    }

    /// <inheritdoc />
    public async Task<PhysicsShapeMutationResult> AddShapeAsync(PhysicsAddShapeRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsCollisionShapeType(request.ShapeNodeType))
        {
            return new PhysicsShapeMutationResult(false, "shapeNodeType must be CollisionShape2D or CollisionShape3D.");
        }

        var body = await FindNodeAsync(request.ScenePath, request.BodyNodePath, cancellationToken).ConfigureAwait(false);
        if (body is null)
        {
            return new PhysicsShapeMutationResult(false, $"Body/area '{request.BodyNodePath}' was not found.");
        }

        if (!IsDimensionCompatible(body.Type, request.ShapeNodeType))
        {
            return new PhysicsShapeMutationResult(false, $"Node '{request.BodyNodePath}' ({body.Type}) is incompatible with '{request.ShapeNodeType}'.");
        }

        var add = await sceneGraphService
            .AddNodeAsync(new SceneGraphAddNodeRequest(request.ScenePath, request.BodyNodePath, request.ShapeNodeType, request.ShapeNodeName), cancellationToken)
            .ConfigureAwait(false);
        if (!add.Success)
        {
            return new PhysicsShapeMutationResult(false, add.Message);
        }

        var shapePath = ResolveChildPath(request.BodyNodePath, request.ShapeNodeName);
        var props = new Dictionary<string, object?>(StringComparer.Ordinal);
        var shapeExpr = BuildShapeExpression(request.ShapeKind, request.ShapeNodeType, request.ShapeParameters, out var error);
        if (error is not null)
        {
            return new PhysicsShapeMutationResult(false, error);
        }

        props["shape"] = shapeExpr;
        foreach (var (k, v) in request.NodeProperties)
        {
            props[k] = v;
        }

        var set = await sceneGraphService
            .SetNodePropertiesAsync(new SceneGraphSetPropertiesRequest(request.ScenePath, shapePath, props), cancellationToken)
            .ConfigureAwait(false);
        if (!set.Success)
        {
            return new PhysicsShapeMutationResult(false, set.Message);
        }

        return new PhysicsShapeMutationResult(true, $"Shape '{shapePath}' created.", request.ScenePath, shapePath);
    }

    /// <inheritdoc />
    public async Task<PhysicsShapeMutationResult> UpdateShapeAsync(PhysicsUpdateShapeRequest request, CancellationToken cancellationToken = default)
    {
        var node = await FindNodeAsync(request.ScenePath, request.ShapeNodePath, cancellationToken).ConfigureAwait(false);
        if (node is null || !IsCollisionShapeType(node.Type))
        {
            return new PhysicsShapeMutationResult(false, $"Shape node '{request.ShapeNodePath}' was not found.");
        }

        var props = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (request.ShapeParameters.Count > 0)
        {
            var currentKind = InferShapeKindFromExpression(node.Properties.GetValueOrDefault("shape"));
            var expr = BuildShapeExpression(currentKind ?? "box", node.Type, request.ShapeParameters, out var error);
            if (error is not null)
            {
                return new PhysicsShapeMutationResult(false, error);
            }

            props["shape"] = expr;
        }

        foreach (var (k, v) in request.NodeProperties)
        {
            props[k] = v;
        }

        if (props.Count == 0)
        {
            return new PhysicsShapeMutationResult(false, "No shape updates were provided.");
        }

        var set = await sceneGraphService
            .SetNodePropertiesAsync(new SceneGraphSetPropertiesRequest(request.ScenePath, request.ShapeNodePath, props), cancellationToken)
            .ConfigureAwait(false);
        return new PhysicsShapeMutationResult(set.Success, set.Message, request.ScenePath, request.ShapeNodePath);
    }

    /// <inheritdoc />
    public async Task<PhysicsShapeMutationResult> RemoveShapeAsync(PhysicsRemoveShapeRequest request, CancellationToken cancellationToken = default)
    {
        var remove = await sceneGraphService
            .RemoveNodeAsync(new SceneGraphRemoveNodeRequest(request.ScenePath, request.ShapeNodePath), cancellationToken)
            .ConfigureAwait(false);
        return new PhysicsShapeMutationResult(remove.Success, remove.Message, request.ScenePath, request.ShapeNodePath);
    }

    /// <inheritdoc />
    public async Task<PhysicsShapeMutationResult> AddCollisionPolygonAsync(PhysicsAddCollisionPolygonRequest request, CancellationToken cancellationToken = default)
    {
        if (request.PolygonNodeType is not ("CollisionPolygon2D" or "CollisionPolygon3D"))
        {
            return new PhysicsShapeMutationResult(false, "polygonNodeType must be CollisionPolygon2D or CollisionPolygon3D.");
        }

        var body = await FindNodeAsync(request.ScenePath, request.BodyNodePath, cancellationToken).ConfigureAwait(false);
        if (body is null)
        {
            return new PhysicsShapeMutationResult(false, $"Body/area '{request.BodyNodePath}' was not found.");
        }

        if (!IsDimensionCompatible(body.Type, request.PolygonNodeType))
        {
            return new PhysicsShapeMutationResult(false, $"Node '{request.BodyNodePath}' ({body.Type}) is incompatible with '{request.PolygonNodeType}'.");
        }

        var add = await sceneGraphService
            .AddNodeAsync(new SceneGraphAddNodeRequest(request.ScenePath, request.BodyNodePath, request.PolygonNodeType, request.PolygonNodeName), cancellationToken)
            .ConfigureAwait(false);
        if (!add.Success)
        {
            return new PhysicsShapeMutationResult(false, add.Message);
        }

        var polygonPath = ResolveChildPath(request.BodyNodePath, request.PolygonNodeName);
        var props = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["polygon"] = request.PolygonData
        };
        foreach (var (k, v) in request.NodeProperties)
        {
            props[k] = v;
        }

        var set = await sceneGraphService
            .SetNodePropertiesAsync(new SceneGraphSetPropertiesRequest(request.ScenePath, polygonPath, props), cancellationToken)
            .ConfigureAwait(false);
        return new PhysicsShapeMutationResult(set.Success, set.Message, request.ScenePath, polygonPath);
    }

    /// <inheritdoc />
    public async Task<PhysicsShapeMutationResult> UpdateCollisionPolygonAsync(PhysicsUpdateCollisionPolygonRequest request, CancellationToken cancellationToken = default)
    {
        var node = await FindNodeAsync(request.ScenePath, request.PolygonNodePath, cancellationToken).ConfigureAwait(false);
        if (node is null || node.Type is not ("CollisionPolygon2D" or "CollisionPolygon3D"))
        {
            return new PhysicsShapeMutationResult(false, $"Polygon node '{request.PolygonNodePath}' was not found.");
        }

        var props = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(request.PolygonData))
        {
            props["polygon"] = request.PolygonData;
        }
        foreach (var (k, v) in request.NodeProperties)
        {
            props[k] = v;
        }

        if (props.Count == 0)
        {
            return new PhysicsShapeMutationResult(false, "No polygon updates were provided.");
        }

        var set = await sceneGraphService
            .SetNodePropertiesAsync(new SceneGraphSetPropertiesRequest(request.ScenePath, request.PolygonNodePath, props), cancellationToken)
            .ConfigureAwait(false);
        return new PhysicsShapeMutationResult(set.Success, set.Message, request.ScenePath, request.PolygonNodePath);
    }

    /// <inheritdoc />
    public async Task<PhysicsShapeMutationResult> RemoveCollisionPolygonAsync(PhysicsRemoveCollisionPolygonRequest request, CancellationToken cancellationToken = default)
        => await RemoveShapeAsync(new PhysicsRemoveShapeRequest(request.ScenePath, request.PolygonNodePath), cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<PhysicsShapeMutationResult> AssignShapeResourceAsync(PhysicsAssignShapeResourceRequest request, CancellationToken cancellationToken = default)
    {
        var node = await FindNodeAsync(request.ScenePath, request.ShapeNodePath, cancellationToken).ConfigureAwait(false);
        if (node is null || !IsCollisionShapeType(node.Type))
        {
            return new PhysicsShapeMutationResult(false, $"Shape node '{request.ShapeNodePath}' was not found.");
        }

        var set = await sceneGraphService
            .SetNodePropertiesAsync(
                new SceneGraphSetPropertiesRequest(
                    request.ScenePath,
                    request.ShapeNodePath,
                    new Dictionary<string, object?>(StringComparer.Ordinal) { ["shape"] = request.ShapeExpression }),
                cancellationToken)
            .ConfigureAwait(false);
        return new PhysicsShapeMutationResult(set.Success, set.Message, request.ScenePath, request.ShapeNodePath);
    }

    /// <inheritdoc />
    public async Task<PhysicsShapeMutationResult> SetShapeFlagsAsync(PhysicsSetShapeFlagsRequest request, CancellationToken cancellationToken = default)
    {
        var node = await FindNodeAsync(request.ScenePath, request.ShapeNodePath, cancellationToken).ConfigureAwait(false);
        if (node is null
            || (node.Type != "CollisionShape2D"
                && node.Type != "CollisionShape3D"
                && node.Type != "CollisionPolygon2D"
                && node.Type != "CollisionPolygon3D"))
        {
            return new PhysicsShapeMutationResult(false, $"Shape/polygon node '{request.ShapeNodePath}' was not found.");
        }

        var props = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (request.Disabled.HasValue) props["disabled"] = request.Disabled.Value;
        if (request.OneWayCollision.HasValue) props["one_way_collision"] = request.OneWayCollision.Value;
        if (request.OneWayCollisionMargin.HasValue) props["one_way_collision_margin"] = request.OneWayCollisionMargin.Value;
        if (request.PlatformOnLeave.HasValue) props["platform_on_leave"] = request.PlatformOnLeave.Value;
        if (props.Count == 0)
        {
            return new PhysicsShapeMutationResult(false, "No shape flags were provided.");
        }

        var set = await sceneGraphService
            .SetNodePropertiesAsync(new SceneGraphSetPropertiesRequest(request.ScenePath, request.ShapeNodePath, props), cancellationToken)
            .ConfigureAwait(false);
        return new PhysicsShapeMutationResult(set.Success, set.Message, request.ScenePath, request.ShapeNodePath);
    }

    /// <summary>
    /// Determines whether a node type is supported for physics body creation.
    /// </summary>
    /// <param name="type">Node type to validate.</param>
    /// <returns><see langword="true"/> when supported; otherwise, <see langword="false"/>.</returns>
    private static bool IsSupportedBodyType(string type)
        => type is "StaticBody3D" or "RigidBody3D" or "CharacterBody3D" or "Area3D"
            or "StaticBody2D" or "RigidBody2D" or "CharacterBody2D" or "Area2D";

    /// <summary>
    /// Determines whether a scene graph node represents a physics body.
    /// </summary>
    /// <param name="node">Node to evaluate.</param>
    /// <returns><see langword="true"/> when node is a physics body or area; otherwise, <see langword="false"/>.</returns>
    private static bool IsPhysicsBody(SceneGraphNodeInfo node)
        => node.Type.EndsWith("Body3D", StringComparison.Ordinal)
            || node.Type.EndsWith("Body2D", StringComparison.Ordinal)
            || node.Type is "Area3D" or "Area2D";

    /// <summary>
    /// Maps a scene graph node into a physics body descriptor.
    /// </summary>
    /// <param name="scenePath">Scene path containing the node.</param>
    /// <param name="node">Scene graph node.</param>
    /// <returns>Physics body descriptor.</returns>
    private static PhysicsBodyInfo ToBodyInfo(string scenePath, SceneGraphNodeInfo node)
        => new(
            scenePath,
            node.NodePath,
            node.Type,
            ParseInt(node.Properties.GetValueOrDefault("collision_layer")),
            ParseInt(node.Properties.GetValueOrDefault("collision_mask")),
            ParseDouble(node.Properties.GetValueOrDefault("gravity_scale")),
            ParseBool(node.Properties.GetValueOrDefault("lock_rotation")));

    /// <summary>
    /// Finds and maps a physics body by node path.
    /// </summary>
    /// <param name="scenePath">Scene path containing the body.</param>
    /// <param name="nodePath">Body node path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Physics body descriptor when found; otherwise, <see langword="null"/>.</returns>
    private async Task<PhysicsBodyInfo?> FindBodyAsync(string scenePath, string nodePath, CancellationToken cancellationToken)
    {
        var roots = await sceneGraphService.ListNodesAsync(scenePath, cancellationToken).ConfigureAwait(false);
        var node = ServiceHelpers.FlattenNodes(roots).FirstOrDefault(x => x.NodePath == nodePath);
        return node is null || !IsPhysicsBody(node) ? null : ToBodyInfo(scenePath, node);
    }

    /// <summary>
    /// Validates property updates for physics body mutation requests.
    /// </summary>
    /// <param name="properties">Property map to validate.</param>
    /// <returns>Validation result represented as a mutation result payload.</returns>
    private static PhysicsMutationResult ValidateProperties(IReadOnlyDictionary<string, object?> properties)
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "collision_layer",
            "collision_mask",
            "gravity_scale",
            "lock_rotation"
        };

        foreach (var (name, value) in properties)
        {
            if (!allowed.Contains(name))
            {
                return new PhysicsMutationResult(false, $"Unsupported physics property '{name}'.");
            }

            if (name is "collision_layer" or "collision_mask")
            {
                if (!TryGetInt(value, out var mask))
                {
                    return new PhysicsMutationResult(false, $"Property '{name}' must be an integer.");
                }

                else if (mask <= 0)
                {
                    return new PhysicsMutationResult(false, $"Property '{name}' must be greater than 0.");
                }
            }

            if (name == "gravity_scale" && !TryGetNumber(value, out _))
            {
                return new PhysicsMutationResult(false, "Property 'gravity_scale' must be numeric.");
            }

            if (name == "lock_rotation" && !TryGetBoolean(value, out _))
            {
                return new PhysicsMutationResult(false, "Property 'lock_rotation' must be boolean.");
            }
        }

        return new PhysicsMutationResult(true, "Properties are valid.");
    }

    /// <summary>
    /// Determines whether a body type requires a collision shape child.
    /// </summary>
    /// <param name="type">Body type to evaluate.</param>
    /// <returns><see langword="true"/> when a collision shape is expected.</returns>
    private static bool RequiresCollisionShape(string type)
        => type is "StaticBody3D" or "RigidBody3D" or "CharacterBody3D" or "Area3D"
            or "StaticBody2D" or "RigidBody2D" or "CharacterBody2D" or "Area2D";

    /// <summary>
    /// Checks whether a body node has at least one matching collision shape descendant.
    /// </summary>
    /// <param name="nodes">Flattened scene graph nodes.</param>
    /// <param name="bodyPath">Body node path.</param>
    /// <param name="bodyType">Body type used to infer collision shape type.</param>
    /// <returns><see langword="true"/> when a matching collision shape exists.</returns>
    private static bool HasCollisionShape(IReadOnlyList<SceneGraphNodeInfo> nodes, string bodyPath, string bodyType)
    {
        var expectedType = bodyType.EndsWith("2D", StringComparison.Ordinal) ? "CollisionShape2D" : "CollisionShape3D";
        var prefix = bodyPath + "/";
        return nodes.Any(x => x.Type == expectedType && x.NodePath.StartsWith(prefix, StringComparison.Ordinal));
    }

    /// <summary>
    /// Converts an input value into a boolean when possible.
    /// </summary>
    /// <param name="value">Input value to convert.</param>
    /// <param name="result">Converted boolean value when conversion succeeds.</param>
    /// <returns><see langword="true"/> when conversion succeeds.</returns>
    private static bool TryGetBoolean(object? value, out bool result)
    {
        switch (value)
        {
            case bool b:
                result = b;
                return true;
            case string s when bool.TryParse(s, out var parsed):
                result = parsed;
                return true;
            default:
                result = false;
                return false;
        }
    }

    /// <summary>
    /// Converts an input value into a 32-bit integer when possible.
    /// </summary>
    /// <param name="value">Input value to convert.</param>
    /// <param name="result">Converted integer value when conversion succeeds.</param>
    /// <returns><see langword="true"/> when conversion succeeds.</returns>
    private static bool TryGetInt(object? value, out int result)
    {
        switch (value)
        {
            case int i:
                result = i;
                return true;
            case long l when l is >= int.MinValue and <= int.MaxValue:
                result = (int)l;
                return true;
            case double d when Math.Abs(d % 1) < 0.000001 && d is >= int.MinValue and <= int.MaxValue:
                result = (int)d;
                return true;
            case string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                result = parsed;
                return true;
            default:
                result = 0;
                return false;
        }
    }

    /// <summary>
    /// Converts an input value into a floating-point number when possible.
    /// </summary>
    /// <param name="value">Input value to convert.</param>
    /// <param name="result">Converted floating-point value when conversion succeeds.</param>
    /// <returns><see langword="true"/> when conversion succeeds.</returns>
    private static bool TryGetNumber(object? value, out double result)
    {
        switch (value)
        {
            case byte b: result = b; return true;
            case short s: result = s; return true;
            case int i: result = i; return true;
            case long l: result = l; return true;
            case float f: result = f; return true;
            case double d: result = d; return true;
            case decimal m: result = (double)m; return true;
            case string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed):
                result = parsed;
                return true;
            default:
                result = 0;
                return false;
        }
    }

    /// <summary>
    /// Parses a serialized integer string into an <see cref="int"/>.
    /// </summary>
    /// <param name="value">Serialized integer text.</param>
    /// <returns>Parsed integer value when valid; otherwise, <see langword="null"/>.</returns>
    private static int? ParseInt(string? value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;

    /// <summary>
    /// Parses a serialized number string into a <see cref="double"/>.
    /// </summary>
    /// <param name="value">Serialized number text.</param>
    /// <returns>Parsed floating-point value when valid; otherwise, <see langword="null"/>.</returns>
    private static double? ParseDouble(string? value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;

    /// <summary>
    /// Parses a serialized boolean string into a <see cref="bool"/>.
    /// </summary>
    /// <param name="value">Serialized boolean text.</param>
    /// <returns>Parsed boolean value when valid; otherwise, <see langword="null"/>.</returns>
    private static bool? ParseBool(string? value)
        => bool.TryParse(value, out var boolean) ? boolean : null;

    /// <summary>
    /// Resolves a child node path using a parent path and child name.
    /// </summary>
    /// <param name="parentPath">Parent node path.</param>
    /// <param name="childName">Child node name.</param>
    /// <returns>Resolved child node path.</returns>
    private static string ResolveChildPath(string parentPath, string childName)
        => parentPath is "." or "" ? childName : $"{parentPath}/{childName}";

    private async Task<SceneGraphNodeInfo?> FindNodeAsync(string scenePath, string nodePath, CancellationToken cancellationToken)
    {
        var roots = await sceneGraphService.ListNodesAsync(scenePath, cancellationToken).ConfigureAwait(false);
        return ServiceHelpers.FlattenNodes(roots).FirstOrDefault(x => x.NodePath == nodePath);
    }

    private static bool IsCollisionShapeType(string type)
        => type is "CollisionShape2D" or "CollisionShape3D";

    private static bool Is2DType(string type)
        => type.EndsWith("2D", StringComparison.Ordinal);

    private static bool Is3DType(string type)
        => type.EndsWith("3D", StringComparison.Ordinal);

    private static bool IsDimensionCompatible(string parentType, string childType)
    {
        if (Is2DType(parentType) && Is3DType(childType))
        {
            return false;
        }

        if (Is3DType(parentType) && Is2DType(childType))
        {
            return false;
        }

        return true;
    }

    private static string? InferShapeKindFromExpression(string? shapeExpression)
    {
        if (string.IsNullOrWhiteSpace(shapeExpression))
        {
            return null;
        }

        var s = shapeExpression.Trim();
        if (s.Contains("RectangleShape2D", StringComparison.Ordinal)) return "rectangle";
        if (s.Contains("CircleShape2D", StringComparison.Ordinal)) return "circle";
        if (s.Contains("CapsuleShape2D", StringComparison.Ordinal)) return "capsule";
        if (s.Contains("BoxShape3D", StringComparison.Ordinal)) return "box";
        if (s.Contains("SphereShape3D", StringComparison.Ordinal)) return "sphere";
        if (s.Contains("CapsuleShape3D", StringComparison.Ordinal)) return "capsule";
        if (s.Contains("CylinderShape3D", StringComparison.Ordinal)) return "cylinder";
        return null;
    }

    private static string BuildShapeExpression(string shapeKind, string shapeNodeType, IReadOnlyDictionary<string, object?> shapeParameters, out string? error)
    {
        error = null;
        var kind = shapeKind.Trim().ToLowerInvariant();
        var is2D = shapeNodeType == "CollisionShape2D";

        string num(string name, double fallback)
        {
            if (!shapeParameters.TryGetValue(name, out var raw) || !TryGetNumber(raw, out var n))
            {
                return fallback.ToString("0.0###", CultureInfo.InvariantCulture);
            }

            return n.ToString("0.0###", CultureInfo.InvariantCulture);
        }

        return (kind, is2D) switch
        {
            ("rectangle", true) => $"RectangleShape2D.new().set(\"size\", Vector2({num("width", 32)}, {num("height", 32)}))",
            ("circle", true) => $"CircleShape2D.new().set(\"radius\", {num("radius", 16)})",
            ("capsule", true) => $"CapsuleShape2D.new().set(\"radius\", {num("radius", 8)}).set(\"height\", {num("height", 32)})",
            ("box", false) => $"BoxShape3D.new().set(\"size\", Vector3({num("width", 1)}, {num("height", 1)}, {num("depth", 1)}))",
            ("sphere", false) => $"SphereShape3D.new().set(\"radius\", {num("radius", 0.5)})",
            ("capsule", false) => $"CapsuleShape3D.new().set(\"radius\", {num("radius", 0.5)}).set(\"height\", {num("height", 2)})",
            ("cylinder", false) => $"CylinderShape3D.new().set(\"radius\", {num("radius", 0.5)}).set(\"height\", {num("height", 2)})",
            ("convex", false) => "ConvexPolygonShape3D.new()",
            ("concave", false) => "ConcavePolygonShape3D.new()",
            _ => BuildShapeExpressionError(kind, is2D, out error)
        };
    }

    private static string BuildShapeExpressionError(string kind, bool is2D, out string? error)
    {
        error = is2D
            ? $"Unsupported 2D shape kind '{kind}'. Supported: rectangle, circle, capsule."
            : $"Unsupported 3D shape kind '{kind}'. Supported: box, sphere, capsule, cylinder, convex, concave.";
        return string.Empty;
    }
}
