using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using ModelContextProtocol.Server;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    /// <summary>
    /// Generates a Godot <c>.import</c> file for an asset path.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="importFileGenerator">Import file generator.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Asset file name or relative path under <c>projectPath</c>.</param>
    /// <param name="importer">Importer identifier.</param>
    /// <param name="type">Target Godot resource type.</param>
    /// <param name="parameters">Optional import parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing generation status.</returns>
    [McpServerTool(Name = "generate_import_file"), Description("Generate a Godot .import file for a given asset path.")]
    public static async Task<ToolResult> GenerateImportFileAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        IImportFileGenerator importFileGenerator,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Asset file name or relative path under projectPath."), Required] string fileName,
        [Description("The Godot importer (e.g., texture, wav)."), Required] string importer,
        [Description("The resource type (e.g., Texture2D, AudioStreamWAV)."), Required] string type,
        [Description("Optional importer parameters (e.g. compress/mode for textures).")] Dictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(importer) || IsBlank(type))
        {
            return Invalid("projectPath, fileName, importer and type are required.");
        }

        string assetPath;
        try
        {
            assetPath = ResolveProjectFilePath(pathResolver, projectPath, fileName);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        var model = new ImportFileModel
        {
            AssetPath = assetPath,
            Importer = importer,
            Type = type
        };
        if (parameters is not null)
        {
            foreach (var parameter in parameters)
            {
                model.Parameters[parameter.Key] = parameter.Value;
            }
        }

        var importPath = $"{assetPath}.import";
        await fileService.WriteAsync(importPath, importFileGenerator.Generate(model), cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, $"Generated {importPath}.");
    }

    /// <summary>
    /// Requests a headless Godot reimport pass for an asset.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="godotCliService">Godot CLI service.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Asset file name or relative path under projectPath.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing reimport command status.</returns>
    [McpServerTool(Name = "reimport_asset"), Description("Trigger a headless Godot reimport of an asset.")]
    public static async Task<ToolResult> ReimportAssetAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        IGodotCliService godotCliService,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Asset file name or relative path under projectPath."), Required] string fileName,
        CancellationToken cancellationToken = default)
    {
        string assetPath;
        try
        {
            assetPath = ResolveProjectFilePath(pathResolver, projectPath, fileName);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        if (!fileService.Exists($"{assetPath}.import"))
        {
            return new ToolResult(false, "Missing .import file for asset.");
        }

        return await godotCliService.RunAsync($"--headless --quit --path \"{pathResolver.ProjectRoot}\"", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a placeholder texture file and matching import configuration.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="importFileGenerator">Import file generator.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Texture file name or relative path under projectPath.</param>
    /// <param name="rawContent">Raw texture file content (optional). If binary, encode as base64; content will be written as provided.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing creation status.</returns>
    [McpServerTool(Name = "create_texture"), Description("Create a dummy texture file and its .import configuration. Optional linkMaterialFileName (with resourcePipelineService) calls resource.assign_texture after create.")]
    public static async Task<ToolResult> CreateTextureAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        IImportFileGenerator importFileGenerator,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Texture file name or relative path under projectPath."), Required] string fileName,
        [Description("Raw texture file content (optional). If binary, encode as base64 and note that file will be written as provided.")] string? rawContent = null,
        IResourcePipelineService? resourcePipelineService = null,
        [Description("Material .tres path under projectPath; requires resourcePipelineService when set.")] string? linkMaterialFileName = null,
        [Description("Material root property to assign (e.g. albedo_texture).")] string linkMaterialPropertyKey = "albedo_texture",
        CancellationToken cancellationToken = default)
    {
        string texturePath;
        try
        {
            texturePath = ResolveProjectFilePath(pathResolver, projectPath, fileName);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        if (!string.IsNullOrWhiteSpace(rawContent))
        {
            await fileService.WriteAsync(texturePath, rawContent, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await fileService.WriteAsync(texturePath, string.Empty, cancellationToken).ConfigureAwait(false);
        }

        var importResult = await GenerateImportFileAsync(fileService, pathResolver, importFileGenerator, projectPath, fileName, "texture", "Texture2D", new Dictionary<string, string>
        {
            ["compress/mode"] = "\"lossy\"",
            ["detect_3d/compress_to"] = "\"vrct\""
        }, cancellationToken).ConfigureAwait(false);

        if (!importResult.Success)
        {
            return importResult;
        }

        if (string.IsNullOrWhiteSpace(linkMaterialFileName))
        {
            return importResult;
        }

        if (resourcePipelineService is null)
        {
            return Invalid("resourcePipelineService is required when linkMaterialFileName is set.");
        }

        return await ResourceAssignTextureAsync(
                resourcePipelineService,
                pathResolver,
                projectPath,
                linkMaterialFileName.Trim(),
                fileName,
                linkMaterialPropertyKey,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a placeholder audio file and matching import configuration.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="importFileGenerator">Import file generator.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Audio file name or relative path under projectPath.</param>
    /// <param name="rawContent">Raw audio file content (optional). If binary, encode as base64; content will be written as provided.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing creation status.</returns>
    [McpServerTool(Name = "create_audio"), Description("Create a dummy audio file and its .import configuration. Optional linkSceneFileName + linkNodePath (with sceneSerializer) set stream on a node under projectPath/scenes/ after create.")]
    public static async Task<ToolResult> CreateAudioAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        IImportFileGenerator importFileGenerator,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Audio file name or relative path under projectPath."), Required] string fileName,
        [Description("Raw audio file content (optional). If binary, encode as base64 and note that file will be written as provided.")] string? rawContent = null,
        ISceneSerializer? sceneSerializer = null,
        [Description("Scene file name under projectPath/scenes/; set with linkNodePath and sceneSerializer to link after create.")] string? linkSceneFileName = null,
        [Description("Node path (e.g. AudioStreamPlayer).")] string? linkNodePath = null,
        [Description("Node property to set (usually stream).")] string linkStreamProperty = "stream",
        [Description("Bootstrap root type when link scene is missing.")] string link_root_type = "Node",
        CancellationToken cancellationToken = default)
    {
        string audioPath;
        try
        {
            audioPath = ResolveProjectFilePath(pathResolver, projectPath, fileName);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        if (!string.IsNullOrWhiteSpace(rawContent))
        {
            await fileService.WriteAsync(audioPath, rawContent, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await fileService.WriteAsync(audioPath, string.Empty, cancellationToken).ConfigureAwait(false);
        }

        var importResult = await GenerateImportFileAsync(fileService, pathResolver, importFileGenerator, projectPath, fileName, "wav", "AudioStreamWAV", new Dictionary<string, string>
        {
            ["force/8_bit"] = "false",
            ["edit/trim"] = "true"
        }, cancellationToken).ConfigureAwait(false);

        if (!importResult.Success)
        {
            return importResult;
        }

        var linkScene = linkSceneFileName?.Trim();
        var linkNode = linkNodePath?.Trim();
        var hasScene = !string.IsNullOrEmpty(linkScene);
        var hasNode = !string.IsNullOrEmpty(linkNode);
        if (hasScene != hasNode)
        {
            return Invalid("linkSceneFileName and linkNodePath must both be provided together, or both omitted.");
        }

        if (!hasScene)
        {
            return importResult;
        }

        if (sceneSerializer is null)
        {
            return Invalid("sceneSerializer is required when linkSceneFileName and linkNodePath are set.");
        }

        return await AttachExtResourceToSceneNodeAsync(
                fileService,
                pathResolver,
                sceneSerializer,
                projectPath,
                linkScene!,
                linkNode!,
                fileName,
                linkStreamProperty.Trim(),
                "AudioStreamWAV",
                link_root_type.Trim(),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
