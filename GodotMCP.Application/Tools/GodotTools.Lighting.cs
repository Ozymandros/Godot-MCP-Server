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
    /// Lists light nodes across scene files under a root path.
    /// </summary>
    /// <param name="lightingService">Lighting service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="projectRootPath">Project root path to scan.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing discovered lights.</returns>
    [McpServerTool(Name = "light.list"), Description("List lights across all scenes under a project root path.")]
    public static async Task<ToolResult> LightListAsync(
        ILightingService lightingService,
        IPathResolver pathResolver,
        [Description("Project root path to scan (res:// or absolute path under the project)."), Required] string projectRootPath,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidProjectPath(pathResolver, projectRootPath))
        {
            return Invalid("projectRootPath must be inside the current project.");
        }

        var lights = await lightingService.ListAsync(projectRootPath, cancellationToken).ConfigureAwait(false);
        var dto = lights.Select(MapLight).ToList();
        return new ToolResult(true, $"Found {dto.Count} light node(s).", dto);
    }

    /// <summary>
    /// Creates a light node in a scene.
    /// </summary>
    /// <param name="lightingService">Lighting service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="scenePath">Scene path where the light is created.</param>
    /// <param name="parentNodePath">Parent node path where the light is inserted.</param>
    /// <param name="lightType">Light type token.</param>
    /// <param name="nodeName">Light node name.</param>
    /// <param name="preset">Optional preset: sun, fill, spot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing operation status and optional light payload.</returns>
    [McpServerTool(Name = "light.create"), Description("Create a light node in a scene with an optional preset.")]
    public static async Task<ToolResult> LightCreateAsync(
        ILightingService lightingService,
        IPathResolver pathResolver,
        [Description("Scene path (res://...) where the light will be created."), Required] string scenePath,
        [Description("Parent node path where the light is added."), Required] string parentNodePath,
        [Description("Light type: DirectionalLight3D, OmniLight3D, SpotLight3D, PointLight2D."), Required] string lightType,
        [Description("Name for the new light node."), Required] string nodeName,
        [Description("Optional preset: sun, fill, spot.")] string? preset = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidResPath(pathResolver, scenePath) || IsBlank(parentNodePath) || IsBlank(lightType) || IsBlank(nodeName))
        {
            return Invalid("scenePath, parentNodePath, lightType, and nodeName are required.");
        }

        var result = await lightingService
            .CreateAsync(new LightCreateRequest(scenePath, parentNodePath, nodeName, lightType, preset), cancellationToken)
            .ConfigureAwait(false);

        return ToLightToolResult(result);
    }

    /// <summary>
    /// Updates selected properties on an existing light node.
    /// </summary>
    /// <param name="lightingService">Lighting service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="scenePath">Scene path containing the light.</param>
    /// <param name="nodePath">Light node path to update.</param>
    /// <param name="properties">Property updates with primitive JSON values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing operation status and optional light payload.</returns>
    [McpServerTool(Name = "light.update"), Description("Update selected properties on an existing light node.")]
    public static async Task<ToolResult> LightUpdateAsync(
        ILightingService lightingService,
        IPathResolver pathResolver,
        [Description("Scene path (res://...) containing the light."), Required] string scenePath,
        [Description("Light node path to update."), Required] string nodePath,
        [Description("Light properties to update. Supported: light_energy, light_color, shadow_enabled, light_specular."), Required]
        Dictionary<string, JsonElement>? properties,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidResPath(pathResolver, scenePath) || IsBlank(nodePath))
        {
            return Invalid("scenePath and nodePath are required.");
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

            var primitive = ToLightPrimitiveValue(value);
            if (primitive is null)
            {
                return Invalid($"Property '{key}' must be a primitive JSON value (string, number, or boolean).");
            }

            normalized[key] = primitive;
        }

        var result = await lightingService
            .UpdateAsync(new LightUpdateRequest(scenePath, nodePath, normalized), cancellationToken)
            .ConfigureAwait(false);

        return ToLightToolResult(result);
    }

    /// <summary>
    /// Validates lighting setup across scenes under a root path.
    /// </summary>
    /// <param name="lightingService">Lighting service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="projectRootPath">Project root path to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing lint-style lighting issues.</returns>
    [McpServerTool(Name = "light.validate"), Description("Validate lighting setup across scenes and return lint-style issues.")]
    public static async Task<ToolResult> LightValidateAsync(
        ILightingService lightingService,
        IPathResolver pathResolver,
        [Description("Project root path to validate (res:// or absolute path under the project)."), Required] string projectRootPath,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidProjectPath(pathResolver, projectRootPath))
        {
            return Invalid("projectRootPath must be inside the current project.");
        }

        var issues = await lightingService.ValidateAsync(projectRootPath, cancellationToken).ConfigureAwait(false);
        var dto = issues
            .Select(x => new LightValidationIssueDto(x.Path, x.Severity, x.Message, x.SuggestedFix, x.Rule, x.ScenePath, x.NodePath))
            .ToList();
        return new ToolResult(true, $"Lighting validation completed. Found {dto.Count} issue(s).", dto);
    }

    /// <summary>
    /// Converts a JSON element into a primitive CLR value for light updates.
    /// </summary>
    /// <param name="value">JSON value to convert.</param>
    /// <returns>Primitive CLR value when supported; otherwise, <see langword="null"/>.</returns>
    private static object? ToLightPrimitiveValue(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number when value.TryGetInt64(out var i) => i,
            JsonValueKind.Number when value.TryGetDouble(out var d) => d,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };

    /// <summary>
    /// Maps a domain light model into a transport DTO.
    /// </summary>
    /// <param name="light">Domain light model.</param>
    /// <returns>Mapped light DTO.</returns>
    private static LightNodeDto MapLight(LightNodeInfo light)
        => new(
            light.ScenePath,
            light.NodePath,
            light.Type,
            light.Energy,
            light.Color,
            light.ShadowsEnabled);

    /// <summary>
    /// Converts a domain light mutation result into an MCP tool result.
    /// </summary>
    /// <param name="result">Domain mutation result.</param>
    /// <returns>Transport-friendly tool result payload.</returns>
    private static ToolResult ToLightToolResult(LightMutationResult result)
    {
        var dto = result.Light is null ? null : MapLight(result.Light);
        return new ToolResult(result.Success, result.Message, dto);
    }
}

/// <summary>
/// Data transfer object describing a light node.
/// </summary>
/// <param name="ScenePath">Scene path containing the light.</param>
/// <param name="NodePath">Resolved node path in the scene hierarchy.</param>
/// <param name="Type">Light node type.</param>
/// <param name="Energy">Light energy/intensity when available.</param>
/// <param name="Color">Light color value when available.</param>
/// <param name="ShadowsEnabled">Whether shadows are enabled when available.</param>
public sealed record LightNodeDto(
    string ScenePath,
    string NodePath,
    string Type,
    double? Energy,
    string? Color,
    bool? ShadowsEnabled);

/// <summary>
/// Data transfer object describing a lighting validation issue.
/// </summary>
/// <param name="Path">Primary path associated with the issue.</param>
/// <param name="Severity">Issue severity.</param>
/// <param name="Message">Issue description.</param>
/// <param name="SuggestedFix">Suggested remediation text.</param>
/// <param name="Rule">Validation rule identifier.</param>
/// <param name="ScenePath">Related scene path.</param>
/// <param name="NodePath">Related node path.</param>
public sealed record LightValidationIssueDto(
    string Path,
    string Severity,
    string Message,
    string? SuggestedFix,
    string? Rule,
    string? ScenePath,
    string? NodePath);
