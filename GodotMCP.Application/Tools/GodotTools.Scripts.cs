using GodotMCP.Core.Models;
using StreamJsonRpc;

namespace GodotMCP.Application.Tools;

public partial class GodotTools
{
    /// <summary>
    /// Create a new script file at the specified path using the requested language.
    /// If language is empty or "preferred" the project preference will be used.
    /// </summary>
    [JsonRpcMethod("create_script")]
    public async Task<ToolResult> CreateScriptAsync(
        string path,
        string language,
        string baseType,
        string className,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(path) || IsBlank(baseType) || IsBlank(className))
        {
            return Invalid("path, baseType and className are required.");
        }
        if (!IsValidResPath(path))
        {
            return Invalid("path must be a valid project-relative path.");
        }

        // If the caller provided an explicit sentinel value of "preferred" or an empty
        // language, resolve the project-level preferred script language from the
        // project configuration service. This allows callers to simply request the
        // preferred language without knowing project settings.
        if (string.IsNullOrWhiteSpace(language) || string.Equals(language, "preferred", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var pref = await projectConfigService.GetValueAsync("scripts", "defaultLanguage", cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(pref)) language = pref!;
            }
            catch
            {
                // ignore and fall back to default below
            }
        }

        // Normalize language values and default to GDScript
        var lang = (language ?? string.Empty).Trim();
        if (string.Equals(lang, "c#", StringComparison.OrdinalIgnoreCase) || string.Equals(lang, "csharp", StringComparison.OrdinalIgnoreCase) || string.Equals(lang, "c-sharp", StringComparison.OrdinalIgnoreCase))
            lang = "csharp";
        else if (string.Equals(lang, "gd", StringComparison.OrdinalIgnoreCase) || string.Equals(lang, "gdscript", StringComparison.OrdinalIgnoreCase))
            lang = "gd";
        else if (string.IsNullOrEmpty(lang))
            lang = "gd";

        string content;
        if (string.Equals(lang, "gd", StringComparison.OrdinalIgnoreCase))
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

    /// <summary>Return the project's preferred scripting language (gd or csharp).</summary>
    [JsonRpcMethod("get_preferred_script_language")]
    public async Task<ToolResult> GetPreferredScriptLanguageAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var pref = await projectConfigService.GetValueAsync("scripts", "defaultLanguage", cancellationToken).ConfigureAwait(false);
            return new ToolResult(true, "Preferred script language retrieved.", new Dictionary<string, string>
            {
                ["language"] = string.IsNullOrWhiteSpace(pref) ? "gd" : pref!
            });
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"Failed to read preferred language: {ex.Message}");
        }
    }

    /// <summary>Set the project's preferred scripting language to "gd" or "csharp".</summary>
    [JsonRpcMethod("set_preferred_script_language")]
    public async Task<ToolResult> SetPreferredScriptLanguageAsync(string language, CancellationToken cancellationToken = default)
    {
        if (IsBlank(language)) return Invalid("language is required.");
        // Normalize common inputs
        var normalized = string.Equals(language, "c#", StringComparison.OrdinalIgnoreCase) || string.Equals(language, "csharp", StringComparison.OrdinalIgnoreCase)
            ? "csharp"
            : "gd";

        try
        {
            await projectConfigService.SetValueAsync("scripts", "defaultLanguage", normalized, cancellationToken).ConfigureAwait(false);
            return new ToolResult(true, $"Preferred script language set to {normalized}.");
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"Failed to set preferred language: {ex.Message}");
        }
    }

    /// <summary>Attach an existing script resource to a node in a scene.</summary>
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

    /// <summary>Validate a script file. For C# uses basic heuristics; for GDScript invokes Godot check-only.</summary>
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
