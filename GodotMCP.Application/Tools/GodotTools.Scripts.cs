using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using ModelContextProtocol.Server;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    /// <summary>
    /// Backward-compatible overload for the single-path contract.
    /// </summary>
    public static Task<ToolResult> CreateScriptAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        string path,
        string language,
        string baseType,
        string className,
        CancellationToken cancellationToken = default)
        => CreateScriptAsync(fileService, pathResolver, pathResolver.ProjectRoot, ToProjectFileName(path, pathResolver), language, baseType, className, cancellationToken);

    /// <summary>
    /// Creates a script file with basic boilerplate in GDScript or C#.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Script file name or relative path under projectPath.</param>
    /// <param name="language">Script language token.</param>
    /// <param name="baseType">Base Godot type to extend.</param>
    /// <param name="className">Script class name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing creation status.</returns>
    [McpServerTool(Name = "create_script"), Description("Create a new GDScript or C# script file with basic boilerplate.")]
    public static async Task<ToolResult> CreateScriptAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Script file name or relative path under projectPath."), Required] string fileName,
        [Description("Script language ('gd' for GDScript, 'cs' for C#)."), Required] string language,
        [Description("Base Godot type to extend (e.g., Node, Node2D)."), Required] string baseType,
        [Description("Name of the script class."), Required] string className,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(projectPath) || IsBlank(fileName) || IsBlank(language) || IsBlank(baseType) || IsBlank(className))
        {
            return Invalid("projectPath, fileName, language, baseType and className are required.");
        }

        string path;
        try
        {
            path = ResolveProjectFilePath(pathResolver, projectPath, fileName);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
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
    /// <summary>
    /// Attaches an existing script resource to a node in a scene.
    /// </summary>
    public static Task<ToolResult> AttachScriptAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        string scenePath,
        string nodeName,
        string scriptPath,
        CancellationToken cancellationToken = default)
        => AttachScriptAsync(fileService, pathResolver, sceneSerializer, pathResolver.ProjectRoot, ToProjectFileName(scenePath, pathResolver), nodeName, ToProjectFileName(scriptPath, pathResolver), cancellationToken);

    [McpServerTool(Name = "attach_script"), Description("Attach an existing script resource to a node in a scene.")]
    public static async Task<ToolResult> AttachScriptAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Scene file name or relative path under projectPath."), Required] string fileName,
        [Description("Name of the target node."), Required] string nodeName,
        [Description("Script file name or relative path under projectPath."), Required] string scriptFileName,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(projectPath) || IsBlank(fileName) || IsBlank(scriptFileName) || IsBlank(nodeName))
        {
            return Invalid("projectPath, fileName, scriptFileName and nodeName are required.");
        }

        string scenePath;
        string scriptPath;
        try
        {
            scenePath = ResolveProjectFilePath(pathResolver, projectPath, fileName);
            scriptPath = ResolveProjectFilePath(pathResolver, projectPath, scriptFileName);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        var scene = sceneSerializer.Deserialize(await fileService.ReadAsync(scenePath, cancellationToken).ConfigureAwait(false));
        var extId = (scene.ExternalResources.Count + 1).ToString();
        scene.ExternalResources.Add(new ExtResource { Id = extId, Path = pathResolver.ToGodotResPath(scriptPath), Type = "Script" });

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
    /// <summary>
    /// Performs lightweight static validation for script files.
    /// </summary>
    public static Task<ToolResult> ValidateScriptAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        IGodotCliService godotCliService,
        string scriptPath,
        bool isCSharp,
        CancellationToken cancellationToken = default)
        => ValidateScriptAsync(fileService, pathResolver, godotCliService, pathResolver.ProjectRoot, ToProjectFileName(scriptPath, pathResolver), isCSharp, cancellationToken);

    [McpServerTool(Name = "validate_script"), Description("Perform static validation on a Godot script file.")]
    public static async Task<ToolResult> ValidateScriptAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        IGodotCliService godotCliService,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Script file name or relative path under projectPath."), Required] string fileName,
        [Description("Set to true if the script is C#, false for GDScript."), Required] bool isCSharp,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(projectPath) || IsBlank(fileName))
        {
            return Invalid("projectPath and fileName are required.");
        }

        string scriptPath;
        try
        {
            scriptPath = ResolveProjectFilePath(pathResolver, projectPath, fileName);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
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
