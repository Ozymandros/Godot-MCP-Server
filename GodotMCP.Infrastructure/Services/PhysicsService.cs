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
        var rootResPath = ServiceHelpers.NormalizeDirectoryToResPath(pathResolver, rootPath);
        var bodies = new List<PhysicsBodyInfo>();
        foreach (var absoluteScenePath in fileService.EnumerateFiles(rootResPath, "*.tscn", recursive: true))
        {
            var sceneResPath = pathResolver.ToResPath(absoluteScenePath);
            var nodes = await sceneGraphService.ListNodesAsync(sceneResPath, cancellationToken).ConfigureAwait(false);
            bodies.AddRange(ServiceHelpers.FlattenNodes(nodes)
                .Where(IsPhysicsBody)
                .Select(x => ToBodyInfo(sceneResPath, x)));
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
        var rootResPath = ServiceHelpers.NormalizeDirectoryToResPath(pathResolver, rootPath);

        foreach (var absoluteScenePath in fileService.EnumerateFiles(rootResPath, "*.tscn", recursive: true))
        {
            var sceneResPath = pathResolver.ToResPath(absoluteScenePath);
            var roots = await sceneGraphService.ListNodesAsync(sceneResPath, cancellationToken).ConfigureAwait(false);
            var nodes = ServiceHelpers.FlattenNodes(roots).ToList();

            foreach (var node in nodes.Where(IsPhysicsBody))
            {
                var info = ToBodyInfo(sceneResPath, node);
                if (info.CollisionLayer is null or <= 0)
                {
                    issues.Add(new PhysicsValidationIssue(
                        sceneResPath,
                        "Warning",
                        $"Body '{info.NodePath}' has invalid collision_layer.",
                        "Set collision_layer to a positive bitmask value.",
                        "invalid-collision-layer",
                        sceneResPath,
                        info.NodePath));
                }

                if (info.CollisionMask is null or <= 0)
                {
                    issues.Add(new PhysicsValidationIssue(
                        sceneResPath,
                        "Warning",
                        $"Body '{info.NodePath}' has invalid collision_mask.",
                        "Set collision_mask to a positive bitmask value.",
                        "invalid-collision-mask",
                        sceneResPath,
                        info.NodePath));
                }

                if (RequiresCollisionShape(info.Type) && !HasCollisionShape(nodes, info.NodePath, info.Type))
                {
                    issues.Add(new PhysicsValidationIssue(
                        sceneResPath,
                        "Warning",
                        $"Body '{info.NodePath}' has no collision shape child.",
                        "Add a CollisionShape node under the body.",
                        "missing-collision-shape",
                        sceneResPath,
                        info.NodePath));
                }
            }
        }

        return issues;
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
}
