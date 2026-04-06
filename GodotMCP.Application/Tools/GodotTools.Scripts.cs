using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using ModelContextProtocol.Server;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    /// <summary>
    /// Creates a script file with basic boilerplate in GDScript or C#.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="path">Destination script path.</param>
    /// <param name="language">Script language token.</param>
    /// <param name="baseType">Base Godot type to extend.</param>
    /// <param name="className">Script class name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing creation status.</returns>
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

    /// <summary>
    /// Attaches an existing script resource to a node in a scene.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="sceneSerializer">Scene serializer used for parsing and writing.</param>
    /// <param name="scenePath">Scene file path.</param>
    /// <param name="nodeName">Target node name.</param>
    /// <param name="scriptPath">Script resource path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing attachment status.</returns>
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

    /// <summary>
    /// Performs lightweight static validation for script files.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="godotCliService">Godot CLI service for GDScript validation.</param>
    /// <param name="scriptPath">Script path to validate.</param>
    /// <param name="isCSharp">Whether the script is C#.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing validation status.</returns>
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
