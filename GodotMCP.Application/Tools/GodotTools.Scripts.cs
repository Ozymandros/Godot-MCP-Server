using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using GodotMCP.Core.SceneGraph;
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
        => CreateScriptAsync(
            fileService,
            pathResolver,
            pathResolver.ProjectRoot,
            ToProjectFileName(path, pathResolver),
            language,
            baseType,
            className,
            rawContent: null,
            sceneSerializer: null,
            linkSceneFileName: null,
            linkNodePath: null,
            link_root_type: "Node",
            cancellationToken);

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
    /// <param name="rawContent">Raw script content. If provided, written verbatim instead of generated boilerplate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing creation status.</returns>
    [McpServerTool(Name = "create_script"), Description("Create a new GDScript or C# script under projectPath. Optional linkSceneFileName + linkNodePath (with sceneSerializer) attach the script to a scene under projectPath/scenes/ after creation.")]
    public static async Task<ToolResult> CreateScriptAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Script file name or relative path under projectPath."), Required] string fileName,
        [Description("Script language ('gd' for GDScript, 'cs' for C#)."), Required] string language,
        [Description("Base Godot type to extend (e.g., Node, Node2D)."), Required] string baseType,
        [Description("Name of the script class."), Required] string className,
        [Description("Raw script content. If provided, written verbatim instead of generated boilerplate.")] string? rawContent = null,
        [Description("Required when linkSceneFileName and linkNodePath are set.")] ISceneSerializer? sceneSerializer = null,
        [Description("Scene file name under projectPath/scenes/; set together with linkNodePath to attach after create.")] string? linkSceneFileName = null,
        [Description("Node path in the scene; set together with linkSceneFileName to attach after create.")] string? linkNodePath = null,
        [Description("Bootstrap root type when linkSceneFileName targets a missing scene.")] string link_root_type = "Node",
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(projectPath) || IsBlank(fileName) || IsBlank(language) || (string.IsNullOrWhiteSpace(rawContent) && (IsBlank(baseType) || IsBlank(className))))
        {
            return Invalid("projectPath, fileName, language and either rawContent or baseType+className are required.");
        }

        var linkScene = linkSceneFileName?.Trim();
        var linkNode = linkNodePath?.Trim();
        var hasLinkScene = !string.IsNullOrEmpty(linkScene);
        var hasLinkNode = !string.IsNullOrEmpty(linkNode);
        if (hasLinkScene != hasLinkNode)
        {
            return Invalid("linkSceneFileName and linkNodePath must both be provided together, or both omitted.");
        }

        if (hasLinkScene && sceneSerializer is null)
        {
            return Invalid("sceneSerializer is required when linkSceneFileName and linkNodePath are set.");
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

        if (!string.IsNullOrWhiteSpace(rawContent))
        {
            await fileService.WriteAsync(path, rawContent, cancellationToken).ConfigureAwait(false);
        }
        else
        {
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
        }

        if (!hasLinkScene)
        {
            return new ToolResult(true, $"Script created at {path}.");
        }

        var attach = await AttachExtResourceToSceneNodeAsync(
                fileService,
                pathResolver,
                sceneSerializer!,
                projectPath,
                linkScene!,
                linkNode!,
                fileName,
                "script",
                "Script",
                link_root_type.Trim(),
                cancellationToken)
            .ConfigureAwait(false);

        if (!attach.Success)
        {
            return attach;
        }

        return new ToolResult(true, $"Script created at {path} and attached to '{linkNode}' in scene '{linkScene}'.", attach.Data);
    }

    /// <summary>
    /// Write arbitrary text content to a project file.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">File name or relative path under projectPath.</param>
    /// <param name="content">Raw file content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result indicating success or failure.</returns>
    [McpServerTool(Name = "write_file"), Description("Write arbitrary text content to a project file.")]
    public static async Task<ToolResult> WriteFileAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("File name or relative path under projectPath."), Required] string fileName,
        [Description("Raw file content."), Required] string content,
        CancellationToken cancellationToken = default)
    {
        string path;
        try
        {
            path = ResolveProjectFilePath(pathResolver, projectPath, fileName);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        await fileService.WriteAsync(path, content, cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, $"Wrote file at {path}.");
    }

    /// <summary>
    /// Attaches an existing script resource to a node in a scene.
    /// </summary>
    public static Task<ToolResult> AttachScriptAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        string scenePath,
        string nodePath,
        string scriptPath,
        CancellationToken cancellationToken = default)
        => AttachScriptAsync(
            fileService,
            pathResolver,
            sceneSerializer,
            pathResolver.ProjectRoot,
            ToProjectFileName(scenePath, pathResolver),
            nodePath,
            ToProjectFileName(scriptPath, pathResolver),
            root_type: "Node",
            cancellationToken);

    /// <summary>
    /// Attaches an existing script resource to a node in a scene.
    /// </summary>
    [McpServerTool(Name = "attach_script"), Description("Attach an existing script resource to a node in a scene under projectPath/scenes/ (same contract as scene.add_node).")]
    public static Task<ToolResult> AttachScriptAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Scene file name under projectPath/scenes/."), Required] string fileName,
        [Description("Target node path in the scene (e.g. Player, Root/Player, Player/CameraRig)."), Required] string nodePath,
        [Description("Script file name or relative path under projectPath."), Required] string scriptFileName,
        [Description("Bootstrap root type when the scene file is bootstrapped.")] string root_type = "Node",
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(projectPath) || IsBlank(fileName) || IsBlank(scriptFileName) || IsBlank(nodePath))
        {
            return Task.FromResult(Invalid("projectPath, fileName, scriptFileName and nodePath are required."));
        }

        return AttachExtResourceToSceneNodeAsync(
            fileService,
            pathResolver,
            sceneSerializer,
            projectPath,
            fileName,
            nodePath,
            scriptFileName,
            "script",
            "Script",
            root_type.Trim(),
            cancellationToken);
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

    /// <summary>
    /// Performs static validation on a Godot script file.
    /// </summary>
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
