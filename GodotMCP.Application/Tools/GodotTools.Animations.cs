using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using GodotMCP.Core.SceneGraph;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;

namespace GodotMCP.Application.Tools;

/// <summary>
/// Provides static methods for Godot animation-related MCP server tools.
/// </summary>
public static partial class GodotTools
{
    /// <summary>
    /// Adds an <c>AnimationPlayer</c> node to a scene under a target parent path.
    /// </summary>
    /// <param name="sceneGraphService">Scene graph service for validated inserts.</param>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="sceneSerializer">Scene serializer (reserved for MCP host compatibility).</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Scene file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="parentPath">Parent node path where the AnimationPlayer is inserted.</param>
    /// <param name="nodeName">AnimationPlayer node name.</param>
    /// <param name="root_type">Root node type used when the scene file is bootstrapped.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing mutation status.</returns>
    [McpServerTool(Name = "add_animation_player"), Description("Append an AnimationPlayer node under a validated parent path (same rules as scene.add_node).")]
    public static async Task<ToolResult> AddAnimationPlayerAsync(
        ISceneGraphService sceneGraphService,
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Scene file name or relative path under projectPath (under scenes/)."), Required] string fileName,
        [Description("Parent node path (e.g. '.', 'Player')."), Required] string parentPath,
        [Description("Name for the new AnimationPlayer node."), Required] string nodeName = "AnimationPlayer",
        [Description("Root node type when the scene file is bootstrapped.")] string root_type = "Node",
        CancellationToken cancellationToken = default)
    {
        _ = sceneSerializer;
        if (IsBlank(nodeName) || IsBlank(parentPath))
        {
            return Invalid("parentPath and nodeName are required.");
        }
        string scenePath;
        try
        {
            scenePath = await EnsureSceneReadyAsync(fileService, pathResolver, projectPath, fileName, root_type, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message, "Use projectPath + /scenes/ + fileName (with .tscn extension).");
        }

        var result = await sceneGraphService
            .AddNodeAsync(new SceneGraphAddNodeRequest(scenePath, parentPath, "AnimationPlayer", nodeName), cancellationToken)
            .ConfigureAwait(false);

        return ToToolResult(result);
    }

    /// <summary>
    /// Adds an animation sub-resource to an existing AnimationPlayer.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="sceneSerializer">Scene serializer used for parsing and writing.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Scene file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="playerNodePath">AnimationPlayer node path (for example <c>AnimationPlayer</c> or <c>UI/AnimationPlayer</c>).</param>
    /// <param name="animName">Animation name.</param>
    /// <param name="length">Animation duration in seconds.</param>
    /// <param name="loop">Whether the animation should loop.</param>
    /// <param name="root_type">Root node type used when the scene file is bootstrapped.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing mutation status.</returns>
    [McpServerTool(Name = "add_animation"), Description("Create and add an animation sub-resource to an AnimationPlayer in a scene.")]
    public static async Task<ToolResult> AddAnimationAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Scene file name or relative path under projectPath."), Required] string fileName,
        [Description("AnimationPlayer node path (e.g. 'AnimationPlayer', 'UI/AnimationPlayer')."), Required] string playerNodePath,
        [Description("The name for the new animation (e.g., 'fade_out')."), Required] string animName,
        [Description("Duration of the animation in seconds."), Required] float length,
        [Description("Whether the animation loops."), Required] bool loop = false,
        [Description("Root node type when the scene file is bootstrapped.")] string root_type = "Node",
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(playerNodePath) || IsBlank(animName))
        {
            return Invalid("playerNodePath and animName are required.");
        }
        string scenePath;
        try
        {
            scenePath = await EnsureSceneReadyAsync(fileService, pathResolver, projectPath, fileName, root_type, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message, "Use projectPath + /scenes/ + fileName (with .tscn extension).");
        }

        var sceneText = await fileService.ReadAsync(scenePath, cancellationToken).ConfigureAwait(false);
        var scene = sceneSerializer.Deserialize(sceneText);
        var index = SceneNodePathIndex.Build(scene);
        if (!SceneNodePathIndex.TryGetNode(index, playerNodePath, out var playerNode) || playerNode is null)
        {
            return new ToolResult(false, $"AnimationPlayer node '{playerNodePath}' not found in scene.");
        }

        if (!string.Equals(playerNode.Type, "AnimationPlayer", StringComparison.Ordinal))
        {
            return new ToolResult(false, $"Node '{playerNodePath}' is not an AnimationPlayer (found type '{playerNode.Type}').");
        }

        // Create animation sub-resource
        var animId = $"Animation_{Guid.NewGuid().ToString("N")[..8]}";
        var animSub = new SubResource { Id = animId, Type = "Animation" };
        animSub.Properties["resource_name"] = $"\"{animName}\"";
        animSub.Properties["length"] = length.ToString("0.0#", CultureInfo.InvariantCulture);
        if (loop)
        {
            animSub.Properties["loop_mode"] = "1";
        }
        scene.SubResources.Add(animSub);

        // Link animation to player (using AnimationLibrary "")
        // Godot usually uses an AnimationLibrary sub-resource to hold animations.
        // For simplicity, we'll check if a library already exists or create one.

        var libId = "AnimationLibrary_default";
        var libSub = scene.SubResources.FirstOrDefault(s => s.Type == "AnimationLibrary");
        if (libSub == null)
        {
            libSub = new SubResource { Id = libId, Type = "AnimationLibrary" };
            scene.SubResources.Add(libSub);
            playerNode.Properties["libraries"] = $$"""{ "": SubResource("{{libId}}") }""";
        }
        else
        {
            libId = libSub.Id;
        }

        // Add to library. Format: _data = { "anim_name": SubResource("id") }
        // We need to parse existing _data if any, or just overwrite for now (simpler).
        // Real implementation should parse the Godot dictionary.
        if (libSub.Properties.TryGetValue("_data", out var currentData))
        {
            // Append to existing: { "a": res, "b": res } -> { "a": res, "b": res, "new": res }
            var inner = currentData.Trim().Trim('{', '}').Trim();
            libSub.Properties["_data"] = $"{{ {inner}, \"{animName}\": SubResource(\"{animId}\") }}";
        }
        else
        {
            libSub.Properties["_data"] = $"{{ \"{animName}\": SubResource(\"{animId}\") }}";
        }

        await fileService.WriteAsync(scenePath, sceneSerializer.Serialize(scene), cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, $"Animation '{animName}' added to {playerNodePath}. Use 'add_animation_track' to add keys.");
    }

    /// <summary>
    /// Adds a track with optional key points to an existing animation resource.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="sceneSerializer">Scene serializer used for parsing and writing.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Scene file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="animName">Target animation resource name.</param>
    /// <param name="targetPath">NodePath target expression for the track.</param>
    /// <param name="trackType">Track type identifier.</param>
    /// <param name="keys">Optional key points for the track.</param>
    /// <param name="root_type">Root node type used when the scene file is bootstrapped.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing mutation status.</returns>
    [McpServerTool(Name = "add_animation_track"), Description("Add a property track with keys to an existing animation inside a scene.")]
    public static async Task<ToolResult> AddAnimationTrackAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Scene file name or relative path under projectPath."), Required] string fileName,
        [Description("The resource_name of the animation."), Required] string animName,
        [Description("Target node path relative to AnimationPlayer (e.g., 'Sprite2D:position')."), Required] string targetPath,
        [Description("Track type: 'value', 'method', 'bezier', 'audio'. Default 'value'."), Required] string trackType = "value",
        [Description("Array of key points: {Time, Value, Transition}."), Required, MinLength(1)] List<KeyPoint>? keys = null,
        [Description("Root node type when the scene file is bootstrapped.")] string root_type = "Node",
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(animName) || IsBlank(targetPath))
        {
            return Invalid("animName and targetPath are required.");
        }
        string scenePath;
        try
        {
            scenePath = await EnsureSceneReadyAsync(fileService, pathResolver, projectPath, fileName, root_type, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message, "Use projectPath + /scenes/ + fileName (with .tscn extension).");
        }

        var sceneText = await fileService.ReadAsync(scenePath, cancellationToken).ConfigureAwait(false);
        var scene = sceneSerializer.Deserialize(sceneText);

        var animSub = scene.SubResources.FirstOrDefault(s => s.Type == "Animation" && s.Properties.GetValueOrDefault("resource_name") == $"\"{animName}\"");
        if (animSub == null)
        {
            return new ToolResult(false, $"Animation resource '{animName}' not found in scene.");
        }

        // Find next track index
        int trackIdx = 0;
        while (animSub.Properties.Keys.Any(k => k.StartsWith($"tracks/{trackIdx}/")))
        {
            trackIdx++;
        }

        var prefix = $"tracks/{trackIdx}/";
        animSub.Properties[$"{prefix}type"] = $"\"{trackType}\"";
        animSub.Properties[$"{prefix}imported"] = "false";
        animSub.Properties[$"{prefix}enabled"] = "true";
        animSub.Properties[$"{prefix}path"] = $"NodePath(\"{targetPath}\")";
        animSub.Properties[$"{prefix}interp"] = "1";
        animSub.Properties[$"{prefix}loop_wrap"] = "true";

        if (keys != null && keys.Count > 0)
        {
            animSub.Properties[$"{prefix}keys"] = GetTrackKeysGodotString(keys);
        }

        await fileService.WriteAsync(scenePath, sceneSerializer.Serialize(scene), cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, $"Track for '{targetPath}' added to animation '{animName}' at index {trackIdx}.");
    }

    /// <summary>
    /// Builds Godot's serialized dictionary payload for track keys.
    /// </summary>
    /// <param name="keys">Track key points to encode.</param>
    /// <returns>Serialized Godot dictionary string for <c>tracks/*/keys</c>.</returns>
    private static string GetTrackKeysGodotString(List<KeyPoint> keys)
    {
        var times = string.Join(", ", keys.Select(k => k.Time.ToString("0.0#", CultureInfo.InvariantCulture)));
        var trans = string.Join(", ", keys.Select(k => k.Transition.ToString("0.0#", CultureInfo.InvariantCulture)));
        var values = string.Join(", ", keys.Select(k => k.Value));
        var update = keys.FirstOrDefault()?.Update ?? 0;

        return $$"""
{
"times": PackedFloat32Array({{times}}),
"transitions": PackedFloat32Array({{trans}}),
"update": {{update}},
"values": [{{values}}]
}
""";
    }
}
