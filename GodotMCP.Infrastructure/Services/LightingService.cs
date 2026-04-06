using System.Globalization;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;

namespace GodotMCP.Infrastructure.Services;

/// <summary>
/// Implements headless lighting operations by scanning and mutating scene graph nodes.
/// </summary>
/// <param name="fileService">File abstraction for project I/O.</param>
/// <param name="pathResolver">Path resolver scoped to the current project.</param>
/// <param name="sceneGraphService">Scene graph service for scene mutations and queries.</param>
public sealed class LightingService(
    IGodotFileService fileService,
    IPathResolver pathResolver,
    ISceneGraphService sceneGraphService) : ILightingService
{
    private static readonly StringComparer Comparer = StringComparer.Ordinal;

    /// <inheritdoc />
    public async Task<IReadOnlyList<LightNodeInfo>> ListAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        var rootResPath = ServiceHelpers.NormalizeDirectoryToResPath(pathResolver, rootPath);
        var lights = new List<LightNodeInfo>();

        foreach (var absoluteScenePath in fileService.EnumerateFiles(rootResPath, "*.tscn", recursive: true))
        {
            var sceneResPath = pathResolver.ToResPath(absoluteScenePath);
            var nodes = await sceneGraphService.ListNodesAsync(sceneResPath, cancellationToken).ConfigureAwait(false);
            lights.AddRange(ServiceHelpers.FlattenNodes(nodes)
                .Where(IsLightNode)
                .Select(x => ToLightInfo(sceneResPath, x)));
        }

        return lights;
    }

    /// <inheritdoc />
    public async Task<LightMutationResult> CreateAsync(LightCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsSupportedLightType(request.LightType))
        {
            return new LightMutationResult(false, "Unsupported light type. Supported: DirectionalLight3D, OmniLight3D, SpotLight3D, PointLight2D.");
        }

        var add = await sceneGraphService
            .AddNodeAsync(new SceneGraphAddNodeRequest(request.ScenePath, request.ParentNodePath, request.LightType, request.NodeName), cancellationToken)
            .ConfigureAwait(false);
        if (!add.Success)
        {
            return new LightMutationResult(false, add.Message);
        }

        var lightPath = ResolveChildPath(request.ParentNodePath, request.NodeName);
        var presetProperties = BuildPreset(request.Preset);
        if (presetProperties.Count > 0)
        {
            var set = await sceneGraphService
                .SetNodePropertiesAsync(new SceneGraphSetPropertiesRequest(request.ScenePath, lightPath, presetProperties), cancellationToken)
                .ConfigureAwait(false);
            if (!set.Success)
            {
                return new LightMutationResult(false, set.Message);
            }
        }

        var light = await FindLightAsync(request.ScenePath, lightPath, cancellationToken).ConfigureAwait(false);
        return new LightMutationResult(true, $"Light '{request.NodeName}' created.", light);
    }

    /// <inheritdoc />
    public async Task<LightMutationResult> UpdateAsync(LightUpdateRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Properties.Count == 0)
        {
            return new LightMutationResult(false, "properties must contain at least one entry.");
        }

        var validation = ValidateProperties(request.Properties);
        if (!validation.Success)
        {
            return new LightMutationResult(false, validation.Message);
        }

        var set = await sceneGraphService
            .SetNodePropertiesAsync(new SceneGraphSetPropertiesRequest(request.ScenePath, request.NodePath, request.Properties), cancellationToken)
            .ConfigureAwait(false);
        if (!set.Success)
        {
            return new LightMutationResult(false, set.Message);
        }

        var light = await FindLightAsync(request.ScenePath, request.NodePath, cancellationToken).ConfigureAwait(false);
        return new LightMutationResult(true, "Light updated.", light);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LightValidationIssue>> ValidateAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        var issues = new List<LightValidationIssue>();
        var lights = await ListAsync(rootPath, cancellationToken).ConfigureAwait(false);
        foreach (var light in lights)
        {
            if (light.Energy is <= 0)
            {
                issues.Add(new LightValidationIssue(
                    light.ScenePath,
                    "Warning",
                    $"Light '{light.NodePath}' has non-positive energy.",
                    "Set light_energy to a value greater than 0.",
                    "non-positive-energy",
                    light.ScenePath,
                    light.NodePath));
            }

            if (light.Energy is > 16)
            {
                issues.Add(new LightValidationIssue(
                    light.ScenePath,
                    "Warning",
                    $"Light '{light.NodePath}' uses very high energy.",
                    "Consider reducing light_energy to avoid blown highlights.",
                    "high-energy",
                    light.ScenePath,
                    light.NodePath));
            }

            if (light.ShadowsEnabled == true && light.Type == "PointLight2D")
            {
                issues.Add(new LightValidationIssue(
                    light.ScenePath,
                    "Info",
                    $"Light '{light.NodePath}' is a 2D light with shadow flag enabled.",
                    "Confirm this property is intended for your renderer setup.",
                    "2d-shadow-flag",
                    light.ScenePath,
                    light.NodePath));
            }
        }

        return issues;
    }

    /// <summary>
    /// Validates mutable light properties and value types.
    /// </summary>
    /// <param name="properties">Property map to validate.</param>
    /// <returns>Validation result payload.</returns>
    private static LightMutationResult ValidateProperties(IReadOnlyDictionary<string, object?> properties)
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "light_energy",
            "light_color",
            "shadow_enabled",
            "light_specular"
        };

        foreach (var (name, value) in properties)
        {
            if (!allowed.Contains(name))
            {
                return new LightMutationResult(false, $"Unsupported light property '{name}'.");
            }

            if (name is "light_energy" or "light_specular" && !TryGetNumber(value, out _))
            {
                return new LightMutationResult(false, $"Property '{name}' must be numeric.");
            }

            if (name == "shadow_enabled" && !TryGetBoolean(value, out _))
            {
                return new LightMutationResult(false, "Property 'shadow_enabled' must be boolean.");
            }

            if (name == "light_color" && value is null)
            {
                return new LightMutationResult(false, "Property 'light_color' must be non-null.");
            }
        }

        return new LightMutationResult(true, "Properties are valid.");
    }

    /// <summary>
    /// Determines whether a light type is supported for creation.
    /// </summary>
    /// <param name="type">Light type to evaluate.</param>
    /// <returns><see langword="true"/> when type is supported.</returns>
    private static bool IsSupportedLightType(string type)
        => type is "DirectionalLight3D" or "OmniLight3D" or "SpotLight3D" or "PointLight2D";

    /// <summary>
    /// Determines whether a scene graph node is a light node.
    /// </summary>
    /// <param name="node">Node to evaluate.</param>
    /// <returns><see langword="true"/> when node is a supported light type.</returns>
    private static bool IsLightNode(SceneGraphNodeInfo node)
        => node.Type.EndsWith("Light3D", StringComparison.Ordinal) || Comparer.Equals(node.Type, "PointLight2D");

    /// <summary>
    /// Maps a scene graph node into a light descriptor.
    /// </summary>
    /// <param name="scenePath">Scene path containing the node.</param>
    /// <param name="node">Scene graph node to map.</param>
    /// <returns>Mapped light descriptor.</returns>
    private static LightNodeInfo ToLightInfo(string scenePath, SceneGraphNodeInfo node)
    {
        var energy = ParseDouble(node.Properties.GetValueOrDefault("light_energy"));
        var color = node.Properties.GetValueOrDefault("light_color");
        var shadows = ParseBool(node.Properties.GetValueOrDefault("shadow_enabled"));
        return new LightNodeInfo(scenePath, node.NodePath, node.Type, energy, color, shadows);
    }

    /// <summary>
    /// Finds a light node by path and maps it to a descriptor.
    /// </summary>
    /// <param name="scenePath">Scene path containing the light.</param>
    /// <param name="lightPath">Light node path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Light descriptor when found; otherwise, <see langword="null"/>.</returns>
    private async Task<LightNodeInfo?> FindLightAsync(string scenePath, string lightPath, CancellationToken cancellationToken)
    {
        var nodes = await sceneGraphService.ListNodesAsync(scenePath, cancellationToken).ConfigureAwait(false);
        var node = ServiceHelpers.FlattenNodes(nodes).FirstOrDefault(x => x.NodePath == lightPath);
        if (node is null || !IsLightNode(node))
        {
            return null;
        }

        return ToLightInfo(scenePath, node);
    }

    /// <summary>
    /// Builds default property values for a known light preset.
    /// </summary>
    /// <param name="preset">Preset identifier.</param>
    /// <returns>Preset property map, or an empty map when preset is unknown.</returns>
    private static Dictionary<string, object?> BuildPreset(string? preset)
    {
        if (string.IsNullOrWhiteSpace(preset))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        return preset.Trim().ToLowerInvariant() switch
        {
            "sun" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["light_energy"] = 2.5,
                ["shadow_enabled"] = true,
                ["light_color"] = "Color(1, 0.95, 0.9, 1)"
            },
            "fill" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["light_energy"] = 0.8,
                ["shadow_enabled"] = false,
                ["light_color"] = "Color(0.7, 0.8, 1, 1)"
            },
            "spot" => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["light_energy"] = 4.0,
                ["shadow_enabled"] = true,
                ["light_color"] = "Color(1, 1, 1, 1)"
            },
            _ => new Dictionary<string, object?>(StringComparer.Ordinal)
        };
    }

    /// <summary>
    /// Converts an input value into a boolean when possible.
    /// </summary>
    /// <param name="value">Input value to convert.</param>
    /// <param name="boolValue">Converted boolean value when conversion succeeds.</param>
    /// <returns><see langword="true"/> when conversion succeeds.</returns>
    private static bool TryGetBoolean(object? value, out bool boolValue)
    {
        switch (value)
        {
            case bool b:
                boolValue = b;
                return true;
            case string s when bool.TryParse(s, out var parsed):
                boolValue = parsed;
                return true;
            default:
                boolValue = false;
                return false;
        }
    }

    /// <summary>
    /// Converts an input value into a floating-point number when possible.
    /// </summary>
    /// <param name="value">Input value to convert.</param>
    /// <param name="number">Converted number when conversion succeeds.</param>
    /// <returns><see langword="true"/> when conversion succeeds.</returns>
    private static bool TryGetNumber(object? value, out double number)
    {
        switch (value)
        {
            case byte b: number = b; return true;
            case short s: number = s; return true;
            case int i: number = i; return true;
            case long l: number = l; return true;
            case float f: number = f; return true;
            case double d: number = d; return true;
            case decimal m: number = (double)m; return true;
            case string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed):
                number = parsed;
                return true;
            default:
                number = 0;
                return false;
        }
    }

    /// <summary>
    /// Parses a serialized number string into a <see cref="double"/>.
    /// </summary>
    /// <param name="value">Serialized number text.</param>
    /// <returns>Parsed number when valid; otherwise, <see langword="null"/>.</returns>
    private static double? ParseDouble(string? value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;

    /// <summary>
    /// Parses a serialized boolean string into a <see cref="bool"/>.
    /// </summary>
    /// <param name="value">Serialized boolean text.</param>
    /// <returns>Parsed boolean when valid; otherwise, <see langword="null"/>.</returns>
    private static bool? ParseBool(string? value)
        => bool.TryParse(value, out var boolean) ? boolean : null;

    /// <summary>
    /// Resolves a child node path from parent path and child name.
    /// </summary>
    /// <param name="parentPath">Parent node path.</param>
    /// <param name="childName">Child node name.</param>
    /// <returns>Resolved child path.</returns>
    private static string ResolveChildPath(string parentPath, string childName)
        => parentPath is "." or "" ? childName : $"{parentPath}/{childName}";
}
