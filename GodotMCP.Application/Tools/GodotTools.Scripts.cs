using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    [McpServerTool(Name = "create_script"), Description("Create a new GDScript or C# script file with basic boilerplate.")]
    public static async Task<ToolResult> CreateScriptAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        [Description("Project path (res://...) for the new script."), Required] string path, 
        [Description("Script language ('gd' for GDScript, 'cs' for C#)."), Required] string language, 
        [Description("Base Godot type to extend (e.g., Node, Node2D)."), Required] string baseType, 
        [Description("Name of the script class."), Required] string className, 
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(path) || IsBlank(language) || IsBlank(baseType) || IsBlank(className))
        {
            return Invalid("path, language, baseType and className are required.");
        }
        if (!IsValidResPath(pathResolver, path))
        {
            return Invalid("path must be a valid project-relative path.");
        }

        string content;
        if (string.Equals(language, "gd", StringComparison.OrdinalIgnoreCase))
        {
            content = $"extends {baseType}{Environment.NewLine}class_name {className}{Environment.NewLine}";
        }
        else
        {
            content = $$"""
using Godot;

public partial class {{className}} : {{baseType}}
{
    public override void _Ready()
    {
    }
}
""";
        }

        await fileService.WriteAsync(path, content, cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, $"Script created at {path}.");
    }

    [McpServerTool(Name = "attach_script"), Description("Attach an existing script resource to a node in a scene.")]
    public static async Task<ToolResult> AttachScriptAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        [Description("Project path (res://...) to the scene file."), Required] string scenePath, 
        [Description("Name of the target node."), Required] string nodeName, 
        [Description("Project path (res://...) to the script to attach."), Required] string scriptPath, 
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(nodeName) || !IsValidResPath(pathResolver, scenePath) || !IsValidResPath(pathResolver, scriptPath))
        {
            return Invalid("scenePath, scriptPath and nodeName are required and must be valid.");
        }

        var scene = sceneSerializer.Deserialize(await fileService.ReadAsync(scenePath, cancellationToken).ConfigureAwait(false));
        var extId = (scene.ExternalResources.Count + 1).ToString();
        scene.ExternalResources.Add(new ExtResource { Id = extId, Path = scriptPath, Type = "Script" });

        var node = scene.Nodes.FirstOrDefault(n => n.Name == nodeName);
        if (node is null)
        {
            return new ToolResult(false, $"Node '{nodeName}' not found.");
        }

        node.Properties["script"] = $"ExtResource(\"{extId}\")";
        await fileService.WriteAsync(scenePath, sceneSerializer.Serialize(scene), cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, $"Script '{scriptPath}' attached to '{nodeName}'.");
    }

    [McpServerTool(Name = "validate_script"), Description("Perform static validation on a Godot script file.")]
    public static async Task<ToolResult> ValidateScriptAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        IGodotCliService godotCliService,
        [Description("Project path (res://...) to the script file."), Required] string scriptPath, 
        [Description("Set to true if the script is C#, false for GDScript."), Required] bool isCSharp, 
        CancellationToken cancellationToken = default)
    {
        if (!IsValidResPath(pathResolver, scriptPath))
        {
            return Invalid("scriptPath must be a valid project-relative path.");
        }

        if (isCSharp)
        {
            var content = await fileService.ReadAsync(scriptPath, cancellationToken).ConfigureAwait(false);
            var ok = content.Contains("class ", StringComparison.Ordinal);
            return ok ? new ToolResult(true, "Basic C# script validation passed.") : new ToolResult(false, "C# script does not contain a class declaration.");
        }

        return await godotCliService.RunAsync($"--headless --check-only --script {scriptPath}", cancellationToken).ConfigureAwait(false);
    }
}
