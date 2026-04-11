namespace GodotMCP.Core.Models;

/// <summary>
/// Represents a UI control node snapshot in a scene.
/// </summary>
/// <param name="Name">Control node name.</param>
/// <param name="Type">Godot control node type.</param>
/// <param name="NodePath">Resolved node path.</param>
/// <param name="Parent">Resolved parent node path.</param>
/// <param name="Properties">Serialized control properties.</param>
public sealed record UiControlInfo(
    string Name,
    string Type,
    string NodePath,
    string Parent,
    IReadOnlyDictionary<string, string> Properties);

/// <summary>
/// Input contract for creating a UI control.
/// </summary>
/// <param name="ScenePath">Scene path to mutate.</param>
/// <param name="ParentNodePath">Target parent node path.</param>
/// <param name="ControlType">Godot control type to create.</param>
/// <param name="ControlName">Control name.</param>
/// <param name="Properties">Optional initial properties.</param>
public sealed record UiAddControlRequest(
    string ScenePath,
    string ParentNodePath,
    string ControlType,
    string ControlName,
    IReadOnlyDictionary<string, object?>? Properties = null);

/// <summary>
/// Input contract for applying a predefined layout preset.
/// </summary>
/// <param name="ScenePath">Scene path to mutate.</param>
/// <param name="ControlNodePath">Target control node path.</param>
/// <param name="Preset">Layout preset identifier.</param>
public sealed record UiSetLayoutRequest(
    string ScenePath,
    string ControlNodePath,
    string Preset);

/// <summary>
/// Input contract for updating selected control properties.
/// </summary>
/// <param name="ScenePath">Scene path to mutate.</param>
/// <param name="ControlNodePath">Target control node path.</param>
/// <param name="Properties">Properties to update.</param>
public sealed record UiSetPropertiesRequest(
    string ScenePath,
    string ControlNodePath,
    IReadOnlyDictionary<string, object?> Properties);

/// <summary>
/// Represents the result of a UI mutation operation.
/// </summary>
/// <param name="Success">Indicates whether the operation succeeded.</param>
/// <param name="Message">Human-readable result message.</param>
/// <param name="Control">Optional control snapshot after mutation.</param>
public sealed record UiMutationResult(
    bool Success,
    string Message,
    UiControlInfo? Control = null);
