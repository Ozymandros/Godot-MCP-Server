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
    /// Lists all Camera2D and Camera3D nodes in scenes under a project root path.
    /// </summary>
    /// <param name="cameraService">Camera service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="projectRootPath">Project root path to scan.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing discovered camera descriptors.</returns>
    [McpServerTool(Name = "camera.list"), Description("List Camera2D and Camera3D nodes across all scenes under a project root path.")]
    public static async Task<ToolResult> CameraListAsync(
        ICameraService cameraService,
        IPathResolver pathResolver,
        [Description("Project directory to scan (absolute path, relative to the configured project root, or legacy res://)."), Required] string projectPath,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidProjectPath(pathResolver, projectPath))
        {
            return Invalid("projectPath must be inside the current project.");
        }

        var result = await cameraService.ListAsync(projectPath, cancellationToken).ConfigureAwait(false);
        var dto = result
            .Select(c => new CameraNodeDto(
                c.ScenePath,
                c.NodePath,
                c.Type.ToString(),
                c.Fov,
                c.Size,
                c.Near,
                c.Far,
                c.Projection.ToString(),
                c.Current))
            .ToList();

        return new ToolResult(true, $"Found {dto.Count} camera node(s).", dto);
    }

    /// <summary>
    /// Creates a camera node in a scene and optionally applies a preset.
    /// </summary>
    /// <param name="cameraService">Camera service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="scenePath">Scene path where the camera is created.</param>
    /// <param name="nodePath">Node path for the new camera.</param>
    /// <param name="cameraType">Camera type token.</param>
    /// <param name="preset">Optional preset name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing creation status and created camera snapshot.</returns>
    [McpServerTool(Name = "camera.create"), Description("Create a Camera2D or Camera3D node in a scene and optionally apply a preset.")]
    public static async Task<ToolResult> CameraCreateAsync(
        ICameraService cameraService,
        IPathResolver pathResolver,
        [Description("Project directory (absolute path, relative to the configured project root, or legacy res://)."), Required] string projectPath,
        [Description("Scene file name or relative path under projectPath."), Required] string fileName,
        [Description("Target node path for the new camera, for example 'Player/CameraRig/MainCamera'."), Required] string nodePath,
        [Description("Camera type: 2d or 3d."), Required] string cameraType,
        [Description("Optional camera preset: cinematic, orthographic-ui, fps.")] string? preset = null,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(nodePath) || IsBlank(cameraType))
        {
            return Invalid("projectPath, fileName, nodePath, and cameraType are required.");
        }
        string scenePath;
        try
        {
            scenePath = ResolveProjectFilePath(pathResolver, projectPath, fileName);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        if (!TryParseCameraType(cameraType, out var type))
        {
            return Invalid("cameraType must be one of: 2d, camera2d, 3d, camera3d.");
        }

        if (!IsSupportedPreset(preset))
        {
            return Invalid("preset must be one of: cinematic, orthographic-ui, fps.");
        }

        var response = await cameraService.CreateAsync(new CameraCreateRequest(scenePath, nodePath, type, preset), cancellationToken)
            .ConfigureAwait(false);
        if (!response.Success)
        {
            return new ToolResult(false, response.Message);
        }

        var dto = response.Camera is null
            ? null
            : new CameraNodeDto(
                response.Camera.ScenePath,
                response.Camera.NodePath,
                response.Camera.Type.ToString(),
                response.Camera.Fov,
                response.Camera.Size,
                response.Camera.Near,
                response.Camera.Far,
                response.Camera.Projection.ToString(),
                response.Camera.Current);

        return new ToolResult(true, response.Message, dto);
    }

    /// <summary>
    /// Updates selected camera properties in a scene.
    /// </summary>
    /// <param name="cameraService">Camera service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="scenePath">Scene path containing the camera.</param>
    /// <param name="nodePath">Camera node path.</param>
    /// <param name="properties">Property updates to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing update status and updated camera snapshot.</returns>
    [McpServerTool(Name = "camera.update"), Description("Update properties of an existing camera node in a scene.")]
    public static async Task<ToolResult> CameraUpdateAsync(
        ICameraService cameraService,
        IPathResolver pathResolver,
        [Description("Project directory (absolute path, relative to the configured project root, or legacy res://)."), Required] string projectPath,
        [Description("Scene file name or relative path under projectPath."), Required] string fileName,
        [Description("Node path of the camera to update."), Required] string nodePath,
        [Description("Camera properties to update. Supported: current, fov, size, near, far, projection."), Required]
        Dictionary<string, JsonElement>? properties,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(nodePath))
        {
            return Invalid("projectPath, fileName and nodePath are required.");
        }
        string scenePath;
        try
        {
            scenePath = ResolveProjectFilePath(pathResolver, projectPath, fileName);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        if (properties is null || properties.Count == 0)
        {
            return Invalid("properties must contain at least one camera property.");
        }

        var normalizedProperties = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in properties)
        {
            if (IsBlank(key))
            {
                return Invalid("Property keys must be non-empty strings.");
            }

            var normalizedValue = ToPrimitiveValue(value);
            if (normalizedValue is null)
            {
                return Invalid($"Property '{key}' must be a primitive JSON value (string, number, or boolean).");
            }

            normalizedProperties[key] = normalizedValue;
        }

        var response = await cameraService.UpdateAsync(
                new CameraUpdateRequest(scenePath, nodePath, normalizedProperties),
                cancellationToken)
            .ConfigureAwait(false);

        if (!response.Success)
        {
            return new ToolResult(false, response.Message);
        }

        var dto = response.Camera is null
            ? null
            : new CameraNodeDto(
                response.Camera.ScenePath,
                response.Camera.NodePath,
                response.Camera.Type.ToString(),
                response.Camera.Fov,
                response.Camera.Size,
                response.Camera.Near,
                response.Camera.Far,
                response.Camera.Projection.ToString(),
                response.Camera.Current);

        return new ToolResult(true, response.Message, dto);
    }

    /// <summary>
    /// Validates camera configuration across scenes and returns lint-style issues.
    /// </summary>
    /// <param name="cameraService">Camera service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="projectRootPath">Project root path to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing validation issues.</returns>
    [McpServerTool(Name = "camera.validate"), Description("Validate camera nodes across scenes and return lint-style issues.")]
    public static async Task<ToolResult> CameraValidateAsync(
        ICameraService cameraService,
        IPathResolver pathResolver,
        [Description("Project directory to validate (absolute path, relative to the configured project root, or legacy res://)."), Required] string projectPath,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidProjectPath(pathResolver, projectPath))
        {
            return Invalid("projectPath must be inside the current project.");
        }

        var issues = await cameraService.ValidateAsync(projectPath, cancellationToken).ConfigureAwait(false);
        var dto = issues
            .Select(i => new CameraValidationIssueDto(i.Path, i.Severity, i.Message, i.SuggestedFix, i.Rule, i.ScenePath, i.NodePath))
            .ToList();

        return new ToolResult(true, $"Camera validation completed. Found {dto.Count} issue(s).", dto);
    }

    /// <summary>
    /// Validates that a project path is inside the current project root.
    /// </summary>
    /// <param name="pathResolver">Path resolver to enforce project boundaries.</param>
    /// <param name="path">Path to validate.</param>
    /// <returns><see langword="true" /> when the path is valid and in-project.</returns>
    private static bool IsValidProjectPath(IPathResolver pathResolver, string path)
    {
        if (IsBlank(path))
        {
            return false;
        }

        try
        {
            if (Path.IsPathRooted(path))
            {
                pathResolver.EnsureInsideProject(path);
                return true;
            }

            _ = pathResolver.ResolvePath(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks whether a preset name is supported.
    /// </summary>
    /// <param name="preset">Preset to validate.</param>
    /// <returns><see langword="true" /> when preset is null/empty or supported.</returns>
    private static bool IsSupportedPreset(string? preset)
    {
        if (string.IsNullOrWhiteSpace(preset))
        {
            return true;
        }

        return preset.Trim().ToLowerInvariant() is "cinematic" or "orthographic-ui" or "fps";
    }

    /// <summary>
    /// Parses a camera type token into a <see cref="CameraNodeType" /> value.
    /// </summary>
    /// <param name="value">Camera type token.</param>
    /// <param name="type">Parsed camera type.</param>
    /// <returns><see langword="true" /> when parsing succeeds.</returns>
    private static bool TryParseCameraType(string value, out CameraNodeType type)
    {
        type = CameraNodeType.Camera3D;
        switch (value.Trim().ToLowerInvariant())
        {
            case "2d":
            case "camera2d":
                type = CameraNodeType.Camera2D;
                return true;
            case "3d":
            case "camera3d":
                type = CameraNodeType.Camera3D;
                return true;
            default:
                return false;
        }
    }
}

/// <summary>
/// Data transfer object describing a camera node.
/// </summary>
/// <param name="ScenePath">Scene path containing the camera.</param>
/// <param name="NodePath">Resolved node path in the scene.</param>
/// <param name="Type">Camera type name.</param>
/// <param name="Fov">Field-of-view value when available.</param>
/// <param name="Size">Orthographic size when available.</param>
/// <param name="Near">Near clipping plane value when available.</param>
/// <param name="Far">Far clipping plane value when available.</param>
/// <param name="Projection">Projection mode string.</param>
/// <param name="Current">Whether the camera is current/active.</param>
public sealed record CameraNodeDto(
    string ScenePath,
    string NodePath,
    string Type,
    double? Fov,
    double? Size,
    double? Near,
    double? Far,
    string Projection,
    bool Current);

/// <summary>
/// Data transfer object describing a camera validation issue.
/// </summary>
/// <param name="Path">Primary path associated with the issue.</param>
/// <param name="Severity">Issue severity level.</param>
/// <param name="Message">Issue message.</param>
/// <param name="SuggestedFix">Suggested remediation.</param>
/// <param name="Rule">Validation rule identifier.</param>
/// <param name="ScenePath">Related scene path.</param>
/// <param name="NodePath">Related node path.</param>
public sealed record CameraValidationIssueDto(
    string Path,
    string Severity,
    string Message,
    string? SuggestedFix,
    string? Rule,
    string? ScenePath,
    string? NodePath);