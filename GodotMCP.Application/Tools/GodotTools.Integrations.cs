using GodotMCP.Core.Models;
using StreamJsonRpc;

namespace GodotMCP.Application.Tools;

public partial class GodotTools
{
    [JsonRpcMethod("discover_integrations")]
    public ToolResult DiscoverIntegrations()
    {
        var entries = integrationInspector.Discover();
        var data = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            data[entry.Name] = $"{entry.Profile}|{entry.GodotVersionRange}|{entry.Source}";
        }

        return new ToolResult(true, $"Discovered {entries.Count} integration(s).", data);
    }

    // attach_script is implemented in GodotTools.Scripts.cs; that RPC will prefer
    // the operations runner when available and fall back to text-based edits.

    [JsonRpcMethod("update_resource_uids")]
    public async Task<ToolResult> UpdateResourceUidsAsync(string[] paths, CancellationToken cancellationToken = default)
    {
        if (paths is null || paths.Length == 0)
            return Invalid("paths are required.");

        if (godotOperationsRunner is not null)
        {
            var payload = new Dictionary<string, object>
            {
                ["schemaVersion"] = "1.0",
                ["requestId"] = Guid.NewGuid().ToString(),
                ["payload"] = new Dictionary<string, object>
                {
                    ["paths"] = paths
                }
            };
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            return await godotOperationsRunner.RunOperationAsync("update_uids", json, cancellationToken).ConfigureAwait(false);
        }

        return new ToolResult(false, "Operations runner not available.", SuggestedRemediation: "Enable GODOT_PATH or register an IGodotOperationsRunner in DI to run UID updates.");
    }

    // reimport_asset is implemented in GodotTools.Import.cs and prefers the
    // operations runner when available; kept here for compatibility notes.



    [JsonRpcMethod("enable_plugin")]
    public async Task<ToolResult> EnablePluginAsync(string pluginName, bool enabled, CancellationToken cancellationToken = default)
    {
        if (IsBlank(pluginName))
        {
            return Invalid("pluginName is required.");
        }

        if (enabled)
        {
            await projectConfigService.SetValueAsync("editor_plugins", pluginName, "true", cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await projectConfigService.RemoveKeyAsync("editor_plugins", pluginName, cancellationToken).ConfigureAwait(false);
        }

        return new ToolResult(true, $"Plugin '{pluginName}' {(enabled ? "enabled" : "disabled")}.");
    }

    [JsonRpcMethod("install_integration")]
    public async Task<ToolResult> InstallIntegrationAsync(
        string integrationName,
        string source,
        IntegrationProfile profile,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(integrationName) || IsBlank(source))
        {
            return Invalid("integrationName and source are required.");
        }

        var safeName = integrationName.Replace(' ', '_');
        var pluginDir = $"res://addons/{safeName}";
        fileService.EnsureDirectory(pluginDir);

        var pluginCfgPath = $"{pluginDir}/plugin.cfg";
        var pluginCfg = $$"""
[plugin]
name="{{integrationName}}"
description="Installed by GodotMCP from {{source}}"
author="GodotMCP"
version="1.0.0"
script="plugin.gd"
""";
        await fileService.WriteAsync(pluginCfgPath, pluginCfg, cancellationToken).ConfigureAwait(false);
        await EnablePluginAsync(safeName, true, cancellationToken).ConfigureAwait(false);

        return new ToolResult(true, $"Integration '{integrationName}' installed as {profile}.", new Dictionary<string, string>
        {
            ["name"] = safeName,
            ["source"] = source,
            ["profile"] = profile.ToString()
        });
    }

    [JsonRpcMethod("list_integration_compatibility")]
    public ToolResult ListIntegrationCompatibility()
    {
        var entries = integrationInspector.Discover();
        var data = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var maintenance = entry.IsMaintained ? "active" : "stale";
            data[entry.Name] = $"godot={entry.GodotVersionRange};platforms={entry.PlatformSupport};maintenance={maintenance};source={entry.Source}";
        }

        return new ToolResult(true, $"Compatibility listed for {entries.Count} integration(s).", data);
    }

    [JsonRpcMethod("verify_integration_health")]
    public ToolResult VerifyIntegrationHealth(string integrationName)
    {
        if (IsBlank(integrationName))
        {
            return Invalid("integrationName is required.");
        }

        var item = integrationInspector.Discover().FirstOrDefault(x => x.Name.Equals(integrationName, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return new ToolResult(false, $"Integration '{integrationName}' not found.", SuggestedRemediation: "Run discover_integrations and verify plugin is installed in addons.");
        }

        return new ToolResult(true, $"Integration '{integrationName}' looks healthy.");
    }
}
