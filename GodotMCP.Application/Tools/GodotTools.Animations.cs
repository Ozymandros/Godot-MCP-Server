using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    [McpServerTool(Name = "add_animation_player"), Description("Append an AnimationPlayer node to a scene.")]
    public static async Task<ToolResult> AddAnimationPlayerAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        [Description("Project path (res://...) to the scene file."), Required] string scenePath, 
        [Description("The hierarchy path of the parent node (e.g., '.', 'Root')."), Required] string parentPath, 
        [Description("Name for the new AnimationPlayer node."), Required] string nodeName = "AnimationPlayer", 
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(nodeName) || IsBlank(parentPath))
        {
            return Invalid("parentPath and nodeName are required.");
        }
        if (!IsValidResPath(pathResolver, scenePath))
        {
            return Invalid("scenePath must be a valid project-relative path.");
        }

        var sceneText = await fileService.ReadAsync(scenePath, cancellationToken).ConfigureAwait(false);
        var scene = sceneSerializer.Deserialize(sceneText);
        
        scene.Nodes.Add(new GodotNode
        {
            Name = nodeName,
            Type = "AnimationPlayer",
            Parent = parentPath
        });
        
        await fileService.WriteAsync(scenePath, sceneSerializer.Serialize(scene), cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, $"AnimationPlayer '{nodeName}' added to {scenePath}.");
    }

    [McpServerTool(Name = "add_animation"), Description("Create and add an animation sub-resource to an AnimationPlayer in a scene.")]
    public static async Task<ToolResult> AddAnimationAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        [Description("Project path (res://...) to the scene file."), Required] string scenePath, 
        [Description("The name of the AnimationPlayer node."), Required] string playerNodeName, 
        [Description("The name for the new animation (e.g., 'fade_out')."), Required] string animName, 
        [Description("Duration of the animation in seconds."), Required] float length, 
        [Description("Whether the animation loops."), Required] bool loop = false, 
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(playerNodeName) || IsBlank(animName))
        {
            return Invalid("playerNodeName and animName are required.");
        }
        if (!IsValidResPath(pathResolver, scenePath))
        {
            return Invalid("scenePath must be a valid project-relative path.");
        }

        var sceneText = await fileService.ReadAsync(scenePath, cancellationToken).ConfigureAwait(false);
        var scene = sceneSerializer.Deserialize(sceneText);

        var playerNode = scene.Nodes.FirstOrDefault(n => n.Name == playerNodeName && n.Type == "AnimationPlayer");
        if (playerNode == null)
        {
            return new ToolResult(false, $"AnimationPlayer node '{playerNodeName}' not found in scene.");
        }

        // Create animation sub-resource
        var animId = $"Animation_{Guid.NewGuid().ToString("N")[..8]}";
        var animSub = new SubResource { Id = animId, Type = "Animation" };
        animSub.Properties["resource_name"] = $"\"{animName}\"";
        animSub.Properties["length"] = length.ToString("0.0#");
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
        return new ToolResult(true, $"Animation '{animName}' added to {playerNodeName}. Use 'add_animation_track' to add keys.");
    }

    [McpServerTool(Name = "add_animation_track"), Description("Add a property track with keys to an existing animation inside a scene.")]
    public static async Task<ToolResult> AddAnimationTrackAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        [Description("Project path (res://...) to the scene file."), Required] string scenePath, 
        [Description("The resource_name of the animation."), Required] string animName, 
        [Description("Target node path relative to AnimationPlayer (e.g., 'Sprite2D:position')."), Required] string targetPath, 
        [Description("Track type: 'value', 'method', 'bezier', 'audio'. Default 'value'."), Required] string trackType = "value", 
        [Description("Array of key points: {Time, Value, Transition}."), Required, MinLength(1)] List<KeyPoint>? keys = null, 
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(animName) || IsBlank(targetPath))
        {
            return Invalid("animName and targetPath are required.");
        }
        if (!IsValidResPath(pathResolver, scenePath))
        {
            return Invalid("scenePath must be a valid project-relative path.");
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

    private static string GetTrackKeysGodotString(List<KeyPoint> keys)
    {
        var times = string.Join(", ", keys.Select(k => k.Time.ToString("0.0#")));
        var trans = string.Join(", ", keys.Select(k => k.Transition.ToString("0.0#")));
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
