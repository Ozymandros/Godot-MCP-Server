using GodotMCP.Core.Models;
using StreamJsonRpc;

namespace GodotMCP.Application.Tools;

/// <summary>
/// Godot import-related tools for generating .import files, reimporting assets, and creating new texture and audio assets with default import settings.
/// </summary>
public partial class GodotTools
{
    /// <summary>Generate a .import file for a given asset and importer settings.</summary>
    [JsonRpcMethod("generate_import_file")]
    public async Task<ToolResult> GenerateImportFileAsync(
        string assetPath,
        string importer,
        string type,
        Dictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidResPath(assetPath) || IsBlank(importer) || IsBlank(type))
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

    /// <summary>Reimport an asset using the project's .import file, preferring engine-backed reimport when available.</summary>
    [JsonRpcMethod("reimport_asset")]
    public async Task<ToolResult> ReimportAssetAsync(string assetPath, CancellationToken cancellationToken = default)
    {
        if (!IsValidResPath(assetPath))
        {
            return Invalid("assetPath must be a valid project-relative path.");
        }

        if (!fileService.Exists($"{assetPath}.import"))
        {
            return new ToolResult(false, "Missing .import file for asset.");
        }

        // Prefer engine-backed reimport via operations runner when available
        if (godotOperationsRunner is not null)
        {
            var payload = new Dictionary<string, object>
            {
                ["schemaVersion"] = "1.0",
                ["requestId"] = Guid.NewGuid().ToString(),
                ["payload"] = new Dictionary<string, object>
                {
                    ["assetPath"] = assetPath
                }
            };
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            return await godotOperationsRunner.RunOperationAsync("reimport_asset", json, cancellationToken).ConfigureAwait(false);
        }

        return await godotCliService.RunAsync($"--headless --quit --path \"{pathResolver.ProjectRoot}\"", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Create a texture asset and an accompanying .import file.</summary>
    [JsonRpcMethod("create_texture")]
    public async Task<ToolResult> CreateTextureAsync(string texturePath, CancellationToken cancellationToken = default)
    {
        if (!IsValidResPath(texturePath))
        {
            return Invalid("texturePath must be a valid project-relative path.");
        }

        await fileService.WriteAsync(texturePath, string.Empty, cancellationToken).ConfigureAwait(false);
        return await GenerateImportFileAsync(texturePath, "texture", "Texture2D", new Dictionary<string, string>
        {
            ["compress/mode"] = "\"lossy\"",
            ["detect_3d/compress_to"] = "\"vrct\""
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Create an audio asset and its .import file.</summary>
    [JsonRpcMethod("create_audio")]
    public async Task<ToolResult> CreateAudioAsync(string audioPath, CancellationToken cancellationToken = default)
    {
        if (!IsValidResPath(audioPath))
        {
            return Invalid("audioPath must be a valid project-relative path.");
        }

        await fileService.WriteAsync(audioPath, string.Empty, cancellationToken).ConfigureAwait(false);
        return await GenerateImportFileAsync(audioPath, "wav", "AudioStreamWAV", new Dictionary<string, string>
        {
            ["force/8_bit"] = "false",
            ["edit/trim"] = "true"
        }, cancellationToken).ConfigureAwait(false);
    }
}
