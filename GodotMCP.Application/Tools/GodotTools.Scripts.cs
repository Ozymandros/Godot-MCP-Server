using GodotMCP.Core.Models;
using StreamJsonRpc;

namespace GodotMCP.Application.Tools;

public partial class GodotTools
{
    [JsonRpcMethod("create_script")]
    public async Task<ToolResult> CreateScriptAsync(
        string path,
        string language,
        string baseType,
        string className,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(path) || IsBlank(language) || IsBlank(baseType) || IsBlank(className))
        {
            return Invalid("path, language, baseType and className are required.");
        }
        if (!IsValidResPath(path))
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

    [JsonRpcMethod("attach_script")]
    public async Task<ToolResult> AttachScriptAsync(
        string scenePath,
        string nodeName,
        string scriptPath,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(nodeName) || !IsValidResPath(scenePath) || !IsValidResPath(scriptPath))
        {
            return Invalid("scenePath, scriptPath and nodeName are required and must be valid.");
        }

        // Prefer engine-backed attach when operations runner is available
        if (godotOperationsRunner is not null)
        {
            var payload = new Dictionary<string, object>
            {
                ["schemaVersion"] = "1.0",
                ["requestId"] = Guid.NewGuid().ToString(),
                ["payload"] = new Dictionary<string, object>
                {
                    ["scenePath"] = scenePath,
                    ["nodeName"] = nodeName,
                    ["scriptPath"] = scriptPath
                }
            };
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            return await godotOperationsRunner.RunOperationAsync("attach_script", json, cancellationToken).ConfigureAwait(false);
        }

        // Fallback to text-based attach
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

    [JsonRpcMethod("validate_script")]
    public async Task<ToolResult> ValidateScriptAsync(
        string scriptPath,
        bool isCSharp,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidResPath(scriptPath))
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
