using System.Globalization;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;

namespace GodotMCP.Infrastructure.Services;

/// <summary>
/// Implements headless camera operations by reading and writing scene files.
/// </summary>
/// <param name="fileService">File abstraction for project I/O.</param>
/// <param name="pathResolver">Path resolver scoped to the current project.</param>
/// <param name="sceneSerializer">Serializer used for scene parsing and emission.</param>
public sealed class CameraService(
    IGodotFileService fileService,
    IPathResolver pathResolver,
    ISceneSerializer sceneSerializer) : ICameraService
{
    /// <summary>
    /// Ordinal comparer used for deterministic string comparisons.
    /// </summary>
    private static readonly StringComparer Comparer = StringComparer.Ordinal;

    /// <inheritdoc />
    public async Task<IReadOnlyList<CameraNodeInfo>> ListAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        var rootResPath = NormalizeDirectoryToResPath(rootPath);
        var cameras = new List<CameraNodeInfo>();

        foreach (var absoluteScenePath in fileService.EnumerateFiles(rootResPath, "*.tscn", recursive: true))
        {
            var sceneResPath = pathResolver.ToResPath(absoluteScenePath);
            var sceneText = await fileService.ReadAsync(sceneResPath, cancellationToken).ConfigureAwait(false);
            var scene = sceneSerializer.Deserialize(sceneText);
            cameras.AddRange(GetCamerasForScene(sceneResPath, scene));
        }

        return cameras;
    }

    /// <inheritdoc />
    public async Task<CameraMutationResult> CreateAsync(CameraCreateRequest request, CancellationToken cancellationToken = default)
    {
        var sceneText = await fileService.ReadAsync(request.ScenePath, cancellationToken).ConfigureAwait(false);
        var scene = sceneSerializer.Deserialize(sceneText);

        var pathIndex = BuildPathIndex(scene);
        var normalizedNodePath = NormalizeNodePath(request.NodePath);
        if (string.IsNullOrWhiteSpace(normalizedNodePath))
        {
            return new CameraMutationResult(false, "nodePath must point to a valid node location.");
        }

        if (pathIndex.ContainsKey(normalizedNodePath))
        {
            return new CameraMutationResult(false, $"Node '{request.NodePath}' already exists.");
        }

        var (parentPath, nodeName) = SplitNodePath(normalizedNodePath);
        if (string.IsNullOrWhiteSpace(nodeName))
        {
            return new CameraMutationResult(false, "nodePath must include a node name.");
        }

        if (!ParentExists(scene, pathIndex, parentPath))
        {
            return new CameraMutationResult(false, $"Parent path '{parentPath}' was not found in scene.");
        }

        var node = new GodotNode
        {
            Name = nodeName,
            Type = request.Type == CameraNodeType.Camera2D ? "Camera2D" : "Camera3D",
            Parent = string.IsNullOrEmpty(parentPath) ? "." : parentPath
        };

        ApplyPreset(node, request.Type, request.Preset);

        scene.Nodes.Add(node);
        await fileService.WriteAsync(request.ScenePath, sceneSerializer.Serialize(scene), cancellationToken).ConfigureAwait(false);

        var info = ToCameraInfo(request.ScenePath, normalizedNodePath, node);
        return new CameraMutationResult(true, $"Camera created at '{request.NodePath}'.", info);
    }

    /// <inheritdoc />
    public async Task<CameraMutationResult> UpdateAsync(CameraUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var sceneText = await fileService.ReadAsync(request.ScenePath, cancellationToken).ConfigureAwait(false);
        var scene = sceneSerializer.Deserialize(sceneText);
        var pathIndex = BuildPathIndex(scene);

        var normalizedNodePath = NormalizeNodePath(request.NodePath);
        if (!pathIndex.TryGetValue(normalizedNodePath, out var node))
        {
            return new CameraMutationResult(false, $"Camera node '{request.NodePath}' not found.");
        }

        if (!IsCameraNode(node))
        {
            return new CameraMutationResult(false, $"Node '{request.NodePath}' is not a camera node.");
        }

        var validation = ValidateAndApplyProperties(node, request.Properties);
        if (!validation.Success)
        {
            return new CameraMutationResult(false, validation.Message);
        }

        await fileService.WriteAsync(request.ScenePath, sceneSerializer.Serialize(scene), cancellationToken).ConfigureAwait(false);
        var info = ToCameraInfo(request.ScenePath, normalizedNodePath, node);
        return new CameraMutationResult(true, "Camera updated.", info);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CameraValidationIssue>> ValidateAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        var rootResPath = NormalizeDirectoryToResPath(rootPath);
        var issues = new List<CameraValidationIssue>();

        foreach (var absoluteScenePath in fileService.EnumerateFiles(rootResPath, "*.tscn", recursive: true))
        {
            var sceneResPath = pathResolver.ToResPath(absoluteScenePath);
            var sceneText = await fileService.ReadAsync(sceneResPath, cancellationToken).ConfigureAwait(false);
            var scene = sceneSerializer.Deserialize(sceneText);

            var pathIndex = BuildPathIndex(scene);
            var cameraInfos = new List<(GodotNode Node, string Path, CameraNodeInfo Info)>();
            foreach (var (path, node) in pathIndex)
            {
                if (IsCameraNode(node))
                {
                    cameraInfos.Add((node, path, ToCameraInfo(sceneResPath, path, node)));
                }
            }

            var currentCameras = cameraInfos.Where(x => x.Info.Current).ToList();
            if (currentCameras.Count > 1)
            {
                issues.Add(new CameraValidationIssue(
                    sceneResPath,
                    "Error",
                    "More than one camera is marked as current in the same scene.",
                    "Keep only one active current camera per scene.",
                    Rule: "multiple-current-cameras",
                    ScenePath: sceneResPath));
            }

            foreach (var camera in cameraInfos)
            {
                if (!HasValidParent(scene, camera.Node))
                {
                    issues.Add(new CameraValidationIssue(
                        sceneResPath,
                        "Error",
                        $"Camera '{camera.Path}' has a missing parent '{camera.Node.Parent}'.",
                        "Update parent path to an existing node.",
                        Rule: "missing-parent",
                        ScenePath: sceneResPath,
                        NodePath: camera.Path));
                }

                if (!HasValidNearFar(camera.Info))
                {
                    issues.Add(new CameraValidationIssue(
                        sceneResPath,
                        "Error",
                        $"Camera '{camera.Path}' has invalid near/far values.",
                        "Ensure near is positive and far is greater than near.",
                        Rule: "invalid-near-far",
                        ScenePath: sceneResPath,
                        NodePath: camera.Path));
                }

                if (camera.Info.Type == CameraNodeType.Camera3D && camera.Info.Projection == CameraProjection.Unsupported)
                {
                    issues.Add(new CameraValidationIssue(
                        sceneResPath,
                        "Error",
                        $"Camera '{camera.Path}' has an unsupported projection mode.",
                        "Use perspective (0), orthographic (1), or frustum (2).",
                        Rule: "unsupported-projection",
                        ScenePath: sceneResPath,
                        NodePath: camera.Path));
                }
            }
        }

        return issues;
    }

    /// <summary>
    /// Validates and applies an update property bag against a camera node.
    /// </summary>
    /// <param name="node">Target camera node.</param>
    /// <param name="properties">Property updates to validate and apply.</param>
    /// <returns>A mutation result describing validation status.</returns>
    private static CameraMutationResult ValidateAndApplyProperties(GodotNode node, IReadOnlyDictionary<string, object?> properties)
    {
        if (properties.Count == 0)
        {
            return new CameraMutationResult(false, "No properties were provided for update.");
        }

        foreach (var (name, value) in properties)
        {
            if (!TryApplyProperty(node, name, value, out var error))
            {
                return new CameraMutationResult(false, error ?? "Invalid camera property update.");
            }
        }

        return new CameraMutationResult(true, "Camera properties updated.");
    }

    /// <summary>
    /// Applies a single camera property update after validating its value type.
    /// </summary>
    /// <param name="node">Target camera node.</param>
    /// <param name="propertyName">Property name to update.</param>
    /// <param name="value">Incoming property value.</param>
    /// <param name="error">Validation error when update fails.</param>
    /// <returns><see langword="true" /> when the property is supported and applied; otherwise, <see langword="false" />.</returns>
    private static bool TryApplyProperty(GodotNode node, string propertyName, object? value, out string? error)
    {
        error = null;
        var name = propertyName.Trim();

        if (name.Length == 0)
        {
            error = "Property name cannot be empty.";
            return false;
        }

        switch (name)
        {
            case "current":
                if (!TryGetBoolean(value, out var current))
                {
                    error = "Property 'current' must be a boolean.";
                    return false;
                }

                if (Comparer.Equals(node.Type, "Camera2D"))
                {
                    node.Properties["enabled"] = current ? "true" : "false";
                }
                else
                {
                    node.Properties["current"] = current ? "true" : "false";
                }

                return true;

            case "fov":
            case "size":
            case "near":
            case "far":
                if (!TryGetNumber(value, out var numericValue))
                {
                    error = $"Property '{name}' must be numeric.";
                    return false;
                }

                node.Properties[name] = numericValue.ToString("0.0###", CultureInfo.InvariantCulture);
                return true;

            case "projection":
                if (!TryParseProjection(value, out var projection))
                {
                    error = "Property 'projection' must be one of: perspective, orthographic, frustum, 0, 1, 2.";
                    return false;
                }

                node.Properties["projection"] = ((int)projection).ToString(CultureInfo.InvariantCulture);
                return true;

            default:
                error = $"Unsupported camera property '{name}'.";
                return false;
        }
    }

    /// <summary>
    /// Applies a known preset to a camera node.
    /// </summary>
    /// <param name="node">Camera node to mutate.</param>
    /// <param name="type">Camera node type.</param>
    /// <param name="preset">Preset name to apply.</param>
    private static void ApplyPreset(GodotNode node, CameraNodeType type, string? preset)
    {
        if (string.IsNullOrWhiteSpace(preset))
        {
            return;
        }

        if (type == CameraNodeType.Camera2D)
        {
            node.Properties["enabled"] = "true";
            return;
        }

        switch (preset.Trim().ToLowerInvariant())
        {
            case "cinematic":
                node.Properties["projection"] = ((int)CameraProjection.Perspective).ToString(CultureInfo.InvariantCulture);
                node.Properties["fov"] = "70";
                node.Properties["near"] = "0.05";
                node.Properties["far"] = "2000";
                break;

            case "orthographic-ui":
                node.Properties["projection"] = ((int)CameraProjection.Orthographic).ToString(CultureInfo.InvariantCulture);
                node.Properties["size"] = "16";
                node.Properties["near"] = "0.01";
                node.Properties["far"] = "4096";
                break;

            case "fps":
                node.Properties["projection"] = ((int)CameraProjection.Perspective).ToString(CultureInfo.InvariantCulture);
                node.Properties["fov"] = "90";
                node.Properties["near"] = "0.05";
                node.Properties["far"] = "1000";
                break;
        }
    }

    /// <summary>
    /// Extracts camera descriptors from a scene.
    /// </summary>
    /// <param name="scenePath">Scene path used in the resulting camera descriptors.</param>
    /// <param name="scene">Parsed scene model.</param>
    /// <returns>An enumerable of camera descriptors.</returns>
    private static IEnumerable<CameraNodeInfo> GetCamerasForScene(string scenePath, GodotScene scene)
    {
        var pathIndex = BuildPathIndex(scene);
        foreach (var (path, node) in pathIndex)
        {
            if (IsCameraNode(node))
            {
                yield return ToCameraInfo(scenePath, path, node);
            }
        }
    }

    /// <summary>
    /// Maps a camera node into a transport-safe camera descriptor.
    /// </summary>
    /// <param name="scenePath">Scene path where the node was found.</param>
    /// <param name="nodePath">Resolved path to the node in the scene hierarchy.</param>
    /// <param name="node">Camera node data.</param>
    /// <returns>A camera descriptor record.</returns>
    private static CameraNodeInfo ToCameraInfo(string scenePath, string nodePath, GodotNode node)
    {
        var type = Comparer.Equals(node.Type, "Camera2D") ? CameraNodeType.Camera2D : CameraNodeType.Camera3D;
        var current = type == CameraNodeType.Camera2D
            ? ReadBool(node.Properties.GetValueOrDefault("enabled")) ?? ReadBool(node.Properties.GetValueOrDefault("current")) ?? false
            : ReadBool(node.Properties.GetValueOrDefault("current")) ?? false;

        return new CameraNodeInfo(
            scenePath,
            nodePath,
            type,
            Fov: ReadDouble(node.Properties.GetValueOrDefault("fov")),
            Size: ReadDouble(node.Properties.GetValueOrDefault("size")),
            Near: ReadDouble(node.Properties.GetValueOrDefault("near")),
            Far: ReadDouble(node.Properties.GetValueOrDefault("far")),
            Projection: type == CameraNodeType.Camera3D ? ReadProjection(node.Properties.GetValueOrDefault("projection")) : CameraProjection.Unsupported,
            Current: current);
    }

    /// <summary>
    /// Checks whether near/far clip values are either absent or logically valid.
    /// </summary>
    /// <param name="info">Camera descriptor to inspect.</param>
    /// <returns><see langword="true" /> when valid; otherwise, <see langword="false" />.</returns>
    private static bool HasValidNearFar(CameraNodeInfo info)
    {
        if (!info.Near.HasValue || !info.Far.HasValue)
        {
            return true;
        }

        return info.Near.Value > 0 && info.Far.Value > info.Near.Value;
    }

    /// <summary>
    /// Checks whether the camera parent path exists in the scene graph.
    /// </summary>
    /// <param name="scene">Scene model containing the node.</param>
    /// <param name="node">Node whose parent is evaluated.</param>
    /// <returns><see langword="true" /> when the parent is valid; otherwise, <see langword="false" />.</returns>
    private static bool HasValidParent(GodotScene scene, GodotNode node)
    {
        if (string.IsNullOrWhiteSpace(node.Parent) || node.Parent == ".")
        {
            return true;
        }

        var parentPath = NormalizeNodePath(node.Parent);
        var paths = BuildPathIndex(scene);
        return paths.ContainsKey(parentPath);
    }

    /// <summary>
    /// Builds a lookup table from node path to node data.
    /// </summary>
    /// <param name="scene">Scene to index.</param>
    /// <returns>Dictionary keyed by normalized node path.</returns>
    private static Dictionary<string, GodotNode> BuildPathIndex(GodotScene scene)
    {
        var index = new Dictionary<string, GodotNode>(Comparer);
        foreach (var node in scene.Nodes)
        {
            var nodePath = ComputeNodePath(node);
            if (nodePath.Length > 0)
            {
                index[nodePath] = node;
            }
        }

        return index;
    }

    /// <summary>
    /// Computes a normalized node path for a scene node.
    /// </summary>
    /// <param name="node">Node to resolve.</param>
    /// <returns>Normalized node path.</returns>
    private static string ComputeNodePath(GodotNode node)
    {
        var parent = NormalizeNodePath(node.Parent);
        if (string.IsNullOrWhiteSpace(parent))
        {
            return NormalizeNodePath(node.Name);
        }

        return NormalizeNodePath($"{parent}/{node.Name}");
    }

    /// <summary>
    /// Validates that a parent path exists, or root insertion is allowed.
    /// </summary>
    /// <param name="scene">Scene being modified.</param>
    /// <param name="pathIndex">Precomputed path index.</param>
    /// <param name="parentPath">Candidate parent path.</param>
    /// <returns><see langword="true" /> when insertion parent is valid; otherwise, <see langword="false" />.</returns>
    private static bool ParentExists(GodotScene scene, IReadOnlyDictionary<string, GodotNode> pathIndex, string parentPath)
    {
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            return scene.Nodes.Count > 0;
        }

        return pathIndex.ContainsKey(parentPath);
    }

    /// <summary>
    /// Normalizes an input project path into a safe <c>res://</c> directory path.
    /// </summary>
    /// <param name="rootPath">Incoming project-relative or absolute path.</param>
    /// <returns>Normalized <c>res://</c> path.</returns>
    private string NormalizeDirectoryToResPath(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return "res://";
        }

        if (Path.IsPathRooted(rootPath))
        {
            var fullPath = Path.GetFullPath(rootPath);
            pathResolver.EnsureInsideProject(fullPath);
            return pathResolver.ToResPath(fullPath);
        }

        var absolute = pathResolver.ResolveResPath(rootPath);
        return pathResolver.ToResPath(absolute);
    }

    /// <summary>
    /// Determines whether a node type is a supported camera node.
    /// </summary>
    /// <param name="node">Node to evaluate.</param>
    /// <returns><see langword="true" /> when the node is Camera2D or Camera3D.</returns>
    private static bool IsCameraNode(GodotNode node)
        => Comparer.Equals(node.Type, "Camera2D") || Comparer.Equals(node.Type, "Camera3D");

    /// <summary>
    /// Splits a normalized node path into parent path and terminal node name.
    /// </summary>
    /// <param name="nodePath">Normalized node path.</param>
    /// <returns>A tuple containing parent path and node name.</returns>
    private static (string ParentPath, string NodeName) SplitNodePath(string nodePath)
    {
        var segments = nodePath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return (string.Empty, string.Empty);
        }

        if (segments.Length == 1)
        {
            return (string.Empty, segments[0]);
        }

        var parent = string.Join('/', segments[..^1]);
        var node = segments[^1];
        return (parent, node);
    }

    /// <summary>
    /// Normalizes a node path for stable indexing and comparisons.
    /// </summary>
    /// <param name="path">Node path candidate.</param>
    /// <returns>Normalized path without leading root markers.</returns>
    private static string NormalizeNodePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == ".")
        {
            return string.Empty;
        }

        var normalized = path.Replace('\\', '/').Trim();
        if (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        return string.Join('/', normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    /// <summary>
    /// Attempts to parse a floating-point scene property value.
    /// </summary>
    /// <param name="value">Raw property value.</param>
    /// <returns>Parsed numeric value when successful; otherwise <see langword="null" />.</returns>
    private static double? ReadDouble(string? value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    /// <summary>
    /// Attempts to parse a boolean scene property value.
    /// </summary>
    /// <param name="value">Raw property value.</param>
    /// <returns>Parsed boolean value when successful; otherwise <see langword="null" />.</returns>
    private static bool? ReadBool(string? value)
    {
        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return null;
    }

    /// <summary>
    /// Parses a projection code from serialized scene property data.
    /// </summary>
    /// <param name="value">Raw projection property value.</param>
    /// <returns>Projection enum value, or <see cref="CameraProjection.Unsupported" />.</returns>
    private static CameraProjection ReadProjection(string? value)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var projectionCode))
        {
            return CameraProjection.Unsupported;
        }

        return projectionCode switch
        {
            0 => CameraProjection.Perspective,
            1 => CameraProjection.Orthographic,
            2 => CameraProjection.Frustum,
            _ => CameraProjection.Unsupported
        };
    }

    /// <summary>
    /// Converts a boxed primitive value into a non-negative number.
    /// </summary>
    /// <param name="value">Input value to convert.</param>
    /// <param name="numericValue">Parsed numeric value when successful.</param>
    /// <returns><see langword="true" /> when conversion succeeds and value is non-negative.</returns>
    private static bool TryGetNumber(object? value, out double numericValue)
    {
        numericValue = default;
        return value switch
        {
            byte b => (numericValue = b) >= 0,
            sbyte sb => (numericValue = sb) >= 0,
            short s => (numericValue = s) >= 0,
            ushort us => (numericValue = us) >= 0,
            int i => (numericValue = i) >= 0,
            uint ui => (numericValue = ui) >= 0,
            long l => (numericValue = l) >= 0,
            ulong ul => (numericValue = ul) >= 0,
            float f => (numericValue = f) >= 0,
            double d => (numericValue = d) >= 0,
            decimal dec => (numericValue = (double)dec) >= 0,
            string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                => (numericValue = parsed) >= 0,
            _ => false
        };
    }

    /// <summary>
    /// Converts a boxed primitive value into a boolean.
    /// </summary>
    /// <param name="value">Input value to convert.</param>
    /// <param name="boolValue">Parsed boolean value when successful.</param>
    /// <returns><see langword="true" /> when conversion succeeds.</returns>
    private static bool TryGetBoolean(object? value, out bool boolValue)
    {
        boolValue = default;
        return value switch
        {
            bool b => (boolValue = b) || !b,
            string text when bool.TryParse(text, out var parsed) => (boolValue = parsed) || !parsed,
            _ => false
        };
    }

    /// <summary>
    /// Parses a projection value from numeric or textual input.
    /// </summary>
    /// <param name="value">Input projection value.</param>
    /// <param name="projection">Parsed projection result.</param>
    /// <returns><see langword="true" /> when parsing succeeds.</returns>
    private static bool TryParseProjection(object? value, out CameraProjection projection)
    {
        projection = CameraProjection.Unsupported;
        return value switch
        {
            int i => TryParseProjectionCode(i, out projection),
            long l when l is >= int.MinValue and <= int.MaxValue => TryParseProjectionCode((int)l, out projection),
            string text => TryParseProjectionString(text, out projection),
            _ => false
        };
    }

    /// <summary>
    /// Parses a projection from its integer code.
    /// </summary>
    /// <param name="code">Projection code.</param>
    /// <param name="projection">Parsed projection value.</param>
    /// <returns><see langword="true" /> when the code maps to a supported projection.</returns>
    private static bool TryParseProjectionCode(int code, out CameraProjection projection)
    {
        projection = code switch
        {
            0 => CameraProjection.Perspective,
            1 => CameraProjection.Orthographic,
            2 => CameraProjection.Frustum,
            _ => CameraProjection.Unsupported
        };

        return projection != CameraProjection.Unsupported;
    }

    /// <summary>
    /// Parses a projection from textual content or numeric text.
    /// </summary>
    /// <param name="value">Projection text value.</param>
    /// <param name="projection">Parsed projection value.</param>
    /// <returns><see langword="true" /> when parsing succeeds.</returns>
    private static bool TryParseProjectionString(string value, out CameraProjection projection)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
        {
            return TryParseProjectionCode(code, out projection);
        }

        projection = normalized switch
        {
            "perspective" => CameraProjection.Perspective,
            "orthographic" => CameraProjection.Orthographic,
            "frustum" => CameraProjection.Frustum,
            _ => CameraProjection.Unsupported
        };

        return projection != CameraProjection.Unsupported;
    }
}