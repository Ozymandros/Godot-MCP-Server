using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    [McpServerTool(Name = "generate_import_file"), Description("Generate a Godot .import file for a given asset path.")]
    public static async Task<ToolResult> GenerateImportFileAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        IImportFileGenerator importFileGenerator,
        [Description("Project path (res://...) to the asset."), Required] string assetPath, 
        [Description("The Godot importer (e.g., texture, wav)."), Required] string importer, 
        [Description("The resource type (e.g., Texture2D, AudioStreamWAV)."), Required] string type, 
        [Description("Optional importer parameters."), Required, MinLength(1)] Dictionary<string, string>? parameters = null, 
        CancellationToken cancellationToken = default)
    {
        if (!IsValidResPath(pathResolver, assetPath) || IsBlank(importer) || IsBlank(type))
        {
            return Invalid("assetPath, importer and type are required and must be valid.");
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

    [McpServerTool(Name = "reimport_asset"), Description("Trigger a headless Godot reimport of an asset.")]
    public static async Task<ToolResult> ReimportAssetAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        IGodotCliService godotCliService,
        [Description("Project path (res://...) to the asset to reimport."), Required] string assetPath, 
        CancellationToken cancellationToken = default)
    {
        if (!IsValidResPath(pathResolver, assetPath))
        {
            return Invalid("assetPath must be a valid project-relative path.");
        }

        if (!fileService.Exists($"{assetPath}.import"))
        {
            return new ToolResult(false, "Missing .import file for asset.");
        }

        return await godotCliService.RunAsync($"--headless --quit --path \"{pathResolver.ProjectRoot}\"", cancellationToken).ConfigureAwait(false);
    }

    [McpServerTool(Name = "create_texture"), Description("Create a dummy texture file and its .import configuration.")]
    public static async Task<ToolResult> CreateTextureAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        IImportFileGenerator importFileGenerator,
        [Description("Project path (res://...) for the new texture."), Required] string texturePath, 
        CancellationToken cancellationToken = default)
    {
        if (!IsValidResPath(pathResolver, texturePath))
        {
            return Invalid("texturePath must be a valid project-relative path.");
        }

        await fileService.WriteAsync(texturePath, string.Empty, cancellationToken).ConfigureAwait(false);
        return await GenerateImportFileAsync(fileService, pathResolver, importFileGenerator, texturePath, "texture", "Texture2D", new Dictionary<string, string>
        {
            ["compress/mode"] = "\"lossy\"",
            ["detect_3d/compress_to"] = "\"vrct\""
        }, cancellationToken).ConfigureAwait(false);
    }

    [McpServerTool(Name = "create_audio"), Description("Create a dummy audio file and its .import configuration.")]
    public static async Task<ToolResult> CreateAudioAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        IImportFileGenerator importFileGenerator,
        [Description("Project path (res://...) for the new audio file."), Required] string audioPath, 
        CancellationToken cancellationToken = default)
    {
        if (!IsValidResPath(pathResolver, audioPath))
        {
            return Invalid("audioPath must be a valid project-relative path.");
        }

        await fileService.WriteAsync(audioPath, string.Empty, cancellationToken).ConfigureAwait(false);
        return await GenerateImportFileAsync(fileService, pathResolver, importFileGenerator, audioPath, "wav", "AudioStreamWAV", new Dictionary<string, string>
        {
            ["force/8_bit"] = "false",
            ["edit/trim"] = "true"
        }, cancellationToken).ConfigureAwait(false);
    }
}
