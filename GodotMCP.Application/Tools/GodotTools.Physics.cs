using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using ModelContextProtocol.Server;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    /// <summary>
    /// Lists physics bodies across scenes under a project root path.
    /// </summary>
    /// <param name="physicsService">Physics service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="projectPath">Project directory to scan (absolute path or path relative to the configured project root).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing discovered physics bodies.</returns>
    [McpServerTool(Name = "physics.list_bodies"), Description("List physics bodies across all scenes under a project root path.")]
    public static async Task<ToolResult> PhysicsListBodiesAsync(
        IPhysicsService physicsService,
        IPathResolver pathResolver,
        [Description("Project directory to scan (absolute path or path relative to the configured project root)."), Required] string projectPath,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidProjectPath(pathResolver, projectPath))
        {
            return Invalid("projectPath must be inside the current project.");
        }

        var bodies = await physicsService.ListAsync(projectPath, cancellationToken).ConfigureAwait(false);
        var dto = bodies.Select(MapBody).ToList();
        return new ToolResult(true, $"Found {dto.Count} physics body node(s).", dto);
    }

    /// <summary>
    /// Creates a physics body in a scene and optionally adds a collision shape child.
    /// </summary>
    /// <param name="physicsService">Physics service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Scene file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="parentNodePath">Parent node path where body is inserted.</param>
    /// <param name="bodyType">Body type to create.</param>
    /// <param name="nodeName">Body node name.</param>
    /// <param name="addCollisionShape">Whether to auto-add a collision shape child.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing operation status and optional body snapshot.</returns>
    [McpServerTool(Name = "physics.create_body"), Description("Create a physics body node in a scene under projectPath/scenes/ (same contract as scene.add_node). Optionally add a collision shape child.")]
    public static async Task<ToolResult> PhysicsCreateBodyAsync(
        IPhysicsService physicsService,
        IGodotFileService fileService,
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Scene file name (e.g. Main.tscn) relative to projectPath/scenes/, or path starting with scenes/ — duplicate segments are merged."), Required] string fileName,
        [Description("Parent node path where the body is added."), Required] string parentNodePath,
        [Description("Body type: StaticBody3D, RigidBody3D, CharacterBody3D, Area3D, StaticBody2D, RigidBody2D, CharacterBody2D, Area2D."), Required] string bodyType,
        [Description("Name for the new body node."), Required] string nodeName,
        [Description("When true, also creates a CollisionShape child node.")] bool addCollisionShape = true,
        [Description("Root node type when the scene file is bootstrapped (Node2D for 2D bodies, Node3D for 3D).")] string root_type = "",
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(parentNodePath) || IsBlank(bodyType) || IsBlank(nodeName))
        {
            return Invalid("projectPath, fileName, parentNodePath, bodyType, and nodeName are required.");
        }
        var rootType = IsBlank(root_type) ? InferRootTypeForPhysicsBody(bodyType) : root_type.Trim();
        string scenePath;
        try
        {
            scenePath = await EnsureSceneReadyAsync(fileService, pathResolver, projectPath, fileName, rootType, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message, "Use projectPath + /scenes/ + fileName (with .tscn extension).");
        }

        var result = await physicsService
            .CreateBodyAsync(new PhysicsCreateBodyRequest(scenePath, parentNodePath, nodeName, bodyType, addCollisionShape), cancellationToken)
            .ConfigureAwait(false);

        return ToPhysicsToolResult(result);
    }

    /// <summary>
    /// Updates selected properties on an existing physics body.
    /// </summary>
    /// <param name="physicsService">Physics service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Scene file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="nodePath">Body node path to update.</param>
    /// <param name="properties">Property updates map.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing operation status and optional body snapshot.</returns>
    [McpServerTool(Name = "physics.update_body"), Description("Update selected properties on a physics body node in projectPath/scenes/ (same contract as scene.add_node).")]
    public static async Task<ToolResult> PhysicsUpdateBodyAsync(
        IPhysicsService physicsService,
        IGodotFileService fileService,
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Scene file name under projectPath/scenes/ (see physics.create_body)."), Required] string fileName,
        [Description("Body node path to update."), Required] string nodePath,
        [Description("Properties to update. Supported: collision_layer, collision_mask, gravity_scale, lock_rotation."), Required]
        Dictionary<string, JsonElement>? properties,
        [Description("Root node type when the scene file is bootstrapped.")] string root_type = "Node3D",
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(nodePath))
        {
            return Invalid("projectPath, fileName and nodePath are required.");
        }
        string scenePath;
        try
        {
            scenePath = await EnsureSceneReadyAsync(fileService, pathResolver, projectPath, fileName, root_type.Trim(), cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message, "Use projectPath + /scenes/ + fileName (with .tscn extension).");
        }

        if (properties is null || properties.Count == 0)
        {
            return Invalid("properties must contain at least one entry.");
        }

        var normalized = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in properties)
        {
            if (IsBlank(key))
            {
                return Invalid("Property keys must be non-empty strings.");
            }

            var primitive = ToPrimitiveValue(value);
            if (primitive is null)
            {
                return Invalid($"Property '{key}' must be a primitive JSON value (string, number, or boolean).");
            }

            normalized[key] = primitive;
        }

        var result = await physicsService
            .UpdateBodyAsync(new PhysicsUpdateBodyRequest(scenePath, nodePath, normalized), cancellationToken)
            .ConfigureAwait(false);

        return ToPhysicsToolResult(result);
    }

    /// <summary>
    /// Validates physics setup across scenes under a root path.
    /// </summary>
    /// <param name="physicsService">Physics service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="projectPath">Project directory to validate (absolute path or path relative to the configured project root).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing lint-style validation issues.</returns>
    [McpServerTool(Name = "physics.validate"), Description("Validate physics setup across scenes and return lint-style issues.")]
    public static async Task<ToolResult> PhysicsValidateAsync(
        IPhysicsService physicsService,
        IPathResolver pathResolver,
        [Description("Project directory to validate (absolute path or path relative to the configured project root)."), Required] string projectPath,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidProjectPath(pathResolver, projectPath))
        {
            return Invalid("projectPath must be inside the current project.");
        }

        var issues = await physicsService.ValidateAsync(projectPath, cancellationToken).ConfigureAwait(false);
        var dto = issues
            .Select(x => new PhysicsValidationIssueDto(x.Path, x.Severity, x.Message, x.SuggestedFix, x.Rule, x.ScenePath, x.NodePath))
            .ToList();
        return new ToolResult(true, $"Physics validation completed. Found {dto.Count} issue(s).", dto);
    }
    /// <summary>
    /// Maps a domain physics body model into a transport DTO.
    /// </summary>
    /// <param name="body">Domain body model.</param>
    /// <returns>Mapped transport DTO.</returns>
    private static PhysicsBodyDto MapBody(PhysicsBodyInfo body)
        => new(
            body.ScenePath,
            body.NodePath,
            body.Type,
            body.CollisionLayer,
            body.CollisionMask,
            body.GravityScale,
            body.LockRotation);

    /// <summary>
    /// Converts a domain mutation result into an MCP tool result.
    /// </summary>
    /// <param name="result">Domain mutation result.</param>
    /// <returns>Transport-friendly tool result payload.</returns>
    private static ToolResult ToPhysicsToolResult(PhysicsMutationResult result)
    {
        var dto = result.Body is null ? null : MapBody(result.Body);
        return new ToolResult(result.Success, result.Message, dto);
    }
}

/// <summary>
/// Data transfer object describing a physics body node.
/// </summary>
/// <param name="ScenePath">Scene path containing the body.</param>
/// <param name="NodePath">Resolved body node path.</param>
/// <param name="Type">Body node type.</param>
/// <param name="CollisionLayer">Collision layer bitmask value.</param>
/// <param name="CollisionMask">Collision mask bitmask value.</param>
/// <param name="GravityScale">Gravity scale value.</param>
/// <param name="LockRotation">Rotation lock state.</param>
public sealed record PhysicsBodyDto(
    string ScenePath,
    string NodePath,
    string Type,
    int? CollisionLayer,
    int? CollisionMask,
    double? GravityScale,
    bool? LockRotation);

/// <summary>
/// Data transfer object describing a physics validation issue.
/// </summary>
/// <param name="Path">Primary issue path.</param>
/// <param name="Severity">Issue severity.</param>
/// <param name="Message">Issue message.</param>
/// <param name="SuggestedFix">Suggested remediation text.</param>
/// <param name="Rule">Validation rule identifier.</param>
/// <param name="ScenePath">Related scene path.</param>
/// <param name="NodePath">Related node path.</param>
public sealed record PhysicsValidationIssueDto(
    string Path,
    string Severity,
    string Message,
    string? SuggestedFix,
    string? Rule,
    string? ScenePath,
    string? NodePath);
