using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;

namespace GodotMCP.Infrastructure.Services;

/// <summary>
/// Implements UI control operations on top of scene graph services.
/// </summary>
/// <param name="sceneGraphService">Scene graph service dependency.</param>
public sealed class UiService(ISceneGraphService sceneGraphService) : IUiService
{
    private static readonly HashSet<string> UiNodeTypes = new(StringComparer.Ordinal)
    {
        "Control",
        "CanvasLayer",
        "Button",
        "Label",
        "LineEdit",
        "TextEdit",
        "RichTextLabel",
        "TextureRect",
        "ColorRect",
        "NinePatchRect",
        "Panel",
        "PanelContainer",
        "MarginContainer",
        "CenterContainer",
        "HBoxContainer",
        "VBoxContainer",
        "GridContainer",
        "ScrollContainer",
        "TabContainer",
        "Window"
    };

    /// <inheritdoc />
    public async Task<IReadOnlyList<UiControlInfo>> ListControlsAsync(string scenePath, CancellationToken cancellationToken = default)
    {
        var nodes = await sceneGraphService.ListNodesAsync(scenePath, cancellationToken).ConfigureAwait(false);
        var controls = new List<UiControlInfo>();
        foreach (var node in Flatten(nodes))
        {
            if (!IsUiNode(node.Type))
            {
                continue;
            }

            controls.Add(MapControl(node));
        }

        return controls;
    }

    /// <inheritdoc />
    public async Task<UiMutationResult> AddControlAsync(UiAddControlRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsUiNode(request.ControlType))
        {
            return new UiMutationResult(false, $"Control type '{request.ControlType}' is not a recognized UI control type.");
        }

        var add = await sceneGraphService.AddNodeAsync(
            new SceneGraphAddNodeRequest(request.ScenePath, request.ParentNodePath, request.ControlType, request.ControlName),
            cancellationToken).ConfigureAwait(false);
        if (!add.Success)
        {
            return new UiMutationResult(false, add.Message);
        }

        var controlPath = ResolveChildPath(request.ParentNodePath, request.ControlName);
        if (request.Properties is { Count: > 0 })
        {
            var set = await sceneGraphService.SetNodePropertiesAsync(
                new SceneGraphSetPropertiesRequest(request.ScenePath, controlPath, request.Properties),
                cancellationToken).ConfigureAwait(false);
            if (!set.Success)
            {
                return new UiMutationResult(false, set.Message);
            }
        }

        return await ResolveControlResultAsync(request.ScenePath, controlPath, "Control added.", cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<UiMutationResult> SetLayoutPresetAsync(UiSetLayoutRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetLayoutPreset(request.Preset, out var layoutProperties))
        {
            return new UiMutationResult(false, "Unsupported layout preset. Supported values: full_rect, top_left, center.");
        }

        var set = await sceneGraphService.SetNodePropertiesAsync(
            new SceneGraphSetPropertiesRequest(request.ScenePath, request.ControlNodePath, layoutProperties),
            cancellationToken).ConfigureAwait(false);
        if (!set.Success)
        {
            return new UiMutationResult(false, set.Message);
        }

        return await ResolveControlResultAsync(request.ScenePath, request.ControlNodePath, "Layout preset applied.", cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<UiMutationResult> SetPropertiesAsync(UiSetPropertiesRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Properties.Count == 0)
        {
            return new UiMutationResult(false, "properties must contain at least one entry.");
        }

        var set = await sceneGraphService.SetNodePropertiesAsync(
            new SceneGraphSetPropertiesRequest(request.ScenePath, request.ControlNodePath, request.Properties),
            cancellationToken).ConfigureAwait(false);
        if (!set.Success)
        {
            return new UiMutationResult(false, set.Message);
        }

        return await ResolveControlResultAsync(request.ScenePath, request.ControlNodePath, "Control properties updated.", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves a named layout preset into concrete control properties.
    /// </summary>
    /// <param name="preset">Preset identifier to resolve.</param>
    /// <param name="properties">Resolved preset properties when supported.</param>
    /// <returns><see langword="true"/> when preset is supported; otherwise, <see langword="false"/>.</returns>
    private static bool TryGetLayoutPreset(string preset, out IReadOnlyDictionary<string, object?> properties)
    {
        switch (preset.Trim().ToLowerInvariant())
        {
            case "full_rect":
                properties = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["anchor_left"] = 0,
                    ["anchor_top"] = 0,
                    ["anchor_right"] = 1,
                    ["anchor_bottom"] = 1,
                    ["offset_left"] = 0,
                    ["offset_top"] = 0,
                    ["offset_right"] = 0,
                    ["offset_bottom"] = 0
                };
                return true;
            case "top_left":
                properties = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["anchor_left"] = 0,
                    ["anchor_top"] = 0,
                    ["anchor_right"] = 0,
                    ["anchor_bottom"] = 0,
                    ["offset_left"] = 0,
                    ["offset_top"] = 0
                };
                return true;
            case "center":
                properties = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["anchor_left"] = 0.5,
                    ["anchor_top"] = 0.5,
                    ["anchor_right"] = 0.5,
                    ["anchor_bottom"] = 0.5,
                    ["offset_left"] = 0,
                    ["offset_top"] = 0,
                    ["offset_right"] = 0,
                    ["offset_bottom"] = 0
                };
                return true;
            default:
                properties = new Dictionary<string, object?>(StringComparer.Ordinal);
                return false;
        }
    }

    /// <summary>
    /// Resolves an updated control snapshot after a mutation.
    /// </summary>
    /// <param name="scenePath">Scene path containing the control.</param>
    /// <param name="controlNodePath">Control node path.</param>
    /// <param name="successMessage">Success message to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>UI mutation result with optional control payload.</returns>
    private async Task<UiMutationResult> ResolveControlResultAsync(string scenePath, string controlNodePath, string successMessage, CancellationToken cancellationToken)
    {
        var controls = await ListControlsAsync(scenePath, cancellationToken).ConfigureAwait(false);
        var control = controls.FirstOrDefault(x => string.Equals(x.NodePath, controlNodePath, StringComparison.Ordinal));
        return control is null
            ? new UiMutationResult(true, successMessage)
            : new UiMutationResult(true, successMessage, control);
    }

    /// <summary>
    /// Determines whether a node type is considered a UI node.
    /// </summary>
    /// <param name="type">Node type to evaluate.</param>
    /// <returns><see langword="true"/> when type is UI-related; otherwise, <see langword="false"/>.</returns>
    private static bool IsUiNode(string type)
        => type.Contains("Control", StringComparison.OrdinalIgnoreCase)
            || UiNodeTypes.Contains(type);

    /// <summary>
    /// Maps a scene graph node into a UI control descriptor.
    /// </summary>
    /// <param name="node">Scene graph node to map.</param>
    /// <returns>Mapped UI control descriptor.</returns>
    private static UiControlInfo MapControl(SceneGraphNodeInfo node)
        => new(
            node.Name,
            node.Type,
            node.NodePath,
            node.Parent,
            node.Properties.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal));

    /// <summary>
    /// Resolves a child node path from parent path and child name.
    /// </summary>
    /// <param name="parentPath">Parent node path.</param>
    /// <param name="childName">Child node name.</param>
    /// <returns>Resolved child path.</returns>
    private static string ResolveChildPath(string parentPath, string childName)
        => parentPath is "." or "" ? childName : $"{parentPath}/{childName}";

    /// <summary>
    /// Flattens recursive scene graph nodes into a single sequence.
    /// </summary>
    /// <param name="children">Root nodes to traverse.</param>
    /// <returns>Flattened recursive node sequence.</returns>
    private static IEnumerable<SceneGraphNodeInfo> Flatten(IReadOnlyList<SceneGraphNodeInfo> children)
    {
        foreach (var child in children)
        {
            yield return child;
            foreach (var descendant in Flatten(child.Children))
            {
                yield return descendant;
            }
        }
    }
}
