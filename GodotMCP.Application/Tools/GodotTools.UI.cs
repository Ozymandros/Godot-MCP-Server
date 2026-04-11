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
    /// Lists UI control nodes in a scene.
    /// </summary>
    /// <param name="uiService">UI service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="scenePath">Scene path to inspect.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing a list of UI controls.</returns>
    [McpServerTool(Name = "ui.list_controls"), Description("List UI controls in a scene with node paths and properties.")]
    public static async Task<ToolResult> UiListControlsAsync(
        IUiService uiService,
        IPathResolver pathResolver,
        [Description("Scene path (res://...) to inspect."), Required] string scenePath,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidResPath(pathResolver, scenePath))
        {
            return Invalid("scenePath must be a valid project-relative path.");
        }

        var controls = await uiService.ListControlsAsync(scenePath, cancellationToken).ConfigureAwait(false);
        var dto = controls.Select(MapControl).ToList();
        return new ToolResult(true, $"Listed {dto.Count} UI control(s).", dto);
    }

    /// <summary>
    /// Adds a UI control node under a parent path.
    /// </summary>
    /// <param name="uiService">UI service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="scenePath">Scene path to mutate.</param>
    /// <param name="parentNodePath">Parent node path where control is added.</param>
    /// <param name="controlType">Godot control type to add.</param>
    /// <param name="controlName">Control name.</param>
    /// <param name="properties">Optional initial control properties.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing operation status and optional control payload.</returns>
    [McpServerTool(Name = "ui.add_control"), Description("Add a UI control node under a parent path in a scene.")]
    public static async Task<ToolResult> UiAddControlAsync(
        IUiService uiService,
        IPathResolver pathResolver,
        [Description("Scene path (res://...) to modify."), Required] string scenePath,
        [Description("Parent node path (for example: ., UI, UI/HUD)."), Required] string parentNodePath,
        [Description("Control type (for example: Control, Button, Label, PanelContainer)."), Required] string controlType,
        [Description("Control name."), Required] string controlName,
        [Description("Optional initial property map. Values must be primitive JSON values.")] Dictionary<string, JsonElement>? properties = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidResPath(pathResolver, scenePath) || IsBlank(parentNodePath) || IsBlank(controlType) || IsBlank(controlName))
        {
            return Invalid("scenePath, parentNodePath, controlType, and controlName are required.");
        }

        string? errorMessage = null;
        var normalizedProperties = properties is null
            ? null
            : NormalizeUiProperties(properties, out errorMessage);
        if (properties is not null && normalizedProperties is null)
        {
            return Invalid(errorMessage!);
        }

        var result = await uiService
            .AddControlAsync(new UiAddControlRequest(scenePath, parentNodePath, controlType, controlName, normalizedProperties), cancellationToken)
            .ConfigureAwait(false);

        return ToUiToolResult(result);
    }

    /// <summary>
    /// Applies a layout preset to a control node.
    /// </summary>
    /// <param name="uiService">UI service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="scenePath">Scene path to mutate.</param>
    /// <param name="controlNodePath">Control node path to update.</param>
    /// <param name="preset">Layout preset name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing operation status and optional control payload.</returns>
    [McpServerTool(Name = "ui.set_layout_preset"), Description("Apply a layout preset to a UI control. Supported presets: full_rect, top_left, center.")]
    public static async Task<ToolResult> UiSetLayoutPresetAsync(
        IUiService uiService,
        IPathResolver pathResolver,
        [Description("Scene path (res://...) to modify."), Required] string scenePath,
        [Description("Control node path to update."), Required] string controlNodePath,
        [Description("Preset name: full_rect, top_left, or center."), Required] string preset,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidResPath(pathResolver, scenePath) || IsBlank(controlNodePath) || IsBlank(preset))
        {
            return Invalid("scenePath, controlNodePath, and preset are required.");
        }

        var result = await uiService
            .SetLayoutPresetAsync(new UiSetLayoutRequest(scenePath, controlNodePath, preset), cancellationToken)
            .ConfigureAwait(false);

        return ToUiToolResult(result);
    }

    /// <summary>
    /// Updates selected UI control properties.
    /// </summary>
    /// <param name="uiService">UI service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="scenePath">Scene path to mutate.</param>
    /// <param name="controlNodePath">Control node path to update.</param>
    /// <param name="properties">Property updates with primitive JSON values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing operation status and optional control payload.</returns>
    [McpServerTool(Name = "ui.set_control_properties"), Description("Update selected properties for a UI control node.")]
    public static async Task<ToolResult> UiSetControlPropertiesAsync(
        IUiService uiService,
        IPathResolver pathResolver,
        [Description("Scene path (res://...) to modify."), Required] string scenePath,
        [Description("Control node path to update."), Required] string controlNodePath,
        [Description("Property map to update. Values must be primitive JSON values."), Required] Dictionary<string, JsonElement>? properties,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidResPath(pathResolver, scenePath) || IsBlank(controlNodePath))
        {
            return Invalid("scenePath and controlNodePath are required.");
        }

        if (properties is null || properties.Count == 0)
        {
            return Invalid("properties must contain at least one entry.");
        }

        var normalizedProperties = NormalizeUiProperties(properties, out var errorMessage);
        if (normalizedProperties is null)
        {
            return Invalid(errorMessage!);
        }

        var result = await uiService
            .SetPropertiesAsync(new UiSetPropertiesRequest(scenePath, controlNodePath, normalizedProperties), cancellationToken)
            .ConfigureAwait(false);

        return ToUiToolResult(result);
    }

    /// <summary>
    /// Normalizes and validates UI property updates from JSON payload.
    /// </summary>
    /// <param name="properties">Incoming JSON property payload.</param>
    /// <param name="errorMessage">Validation error text when normalization fails.</param>
    /// <returns>Normalized property map when valid; otherwise, <see langword="null"/>.</returns>
    private static Dictionary<string, object?>? NormalizeUiProperties(Dictionary<string, JsonElement> properties, out string? errorMessage)
    {
        errorMessage = null;
        var normalized = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in properties)
        {
            if (IsBlank(key))
            {
                errorMessage = "Property keys must be non-empty strings.";
                return null;
            }

            var primitive = ToPrimitiveValue(value);
            if (primitive is null)
            {
                errorMessage = $"Property '{key}' must be a primitive JSON value (string, number, or boolean).";
                return null;
            }

            normalized[key] = primitive;
        }

        return normalized;
    }
    /// <summary>
    /// Converts a UI mutation result into an MCP tool result payload.
    /// </summary>
    /// <param name="result">UI mutation result from the domain layer.</param>
    /// <returns>Transport-friendly tool result.</returns>
    private static ToolResult ToUiToolResult(UiMutationResult result)
    {
        var dto = result.Control is null ? null : MapControl(result.Control);
        return new ToolResult(result.Success, result.Message, dto);
    }

    /// <summary>
    /// Maps a domain UI control model into a transport DTO.
    /// </summary>
    /// <param name="control">Domain control model.</param>
    /// <returns>Mapped UI control DTO.</returns>
    private static UiControlDto MapControl(UiControlInfo control)
        => new(
            control.Name,
            control.Type,
            control.NodePath,
            control.Parent,
            control.Properties.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal));
}

/// <summary>
/// Transport payload describing a UI control node.
/// </summary>
/// <param name="Name">Control node name.</param>
/// <param name="Type">Control node type.</param>
/// <param name="NodePath">Resolved node path.</param>
/// <param name="Parent">Resolved parent node path.</param>
/// <param name="Properties">Control property map.</param>
public sealed record UiControlDto(
    string Name,
    string Type,
    string NodePath,
    string Parent,
    Dictionary<string, string> Properties);
