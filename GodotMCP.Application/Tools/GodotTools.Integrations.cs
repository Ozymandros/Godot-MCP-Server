using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using ModelContextProtocol.Server;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    /// <summary>
    /// Discovers installed integrations under the project addons directory.
    /// </summary>
    /// <param name="integrationInspector">Integration inspector service.</param>
    /// <returns>Tool result containing discovered integration metadata.</returns>
    [McpServerTool(Name = "discover_integrations"), Description("Scan the project addons directory to find installed Godot integrations.")]
    public static ToolResult DiscoverIntegrations(IIntegrationInspector integrationInspector)
    {
        var entries = integrationInspector.Discover();
        var data = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            data[entry.Name] = $"{entry.Profile}|{entry.GodotVersionRange}|{entry.Source}";
        }

        return new ToolResult(true, $"Discovered {entries.Count} integration(s).", data);
    }

    /// <summary>
    /// Enables or disables an editor plugin entry by addon folder name.
    /// </summary>
    /// <param name="projectConfigService">Project configuration service.</param>
    /// <param name="pluginName">Addon folder name.</param>
    /// <param name="enabled">Whether plugin should be enabled.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing mutation status.</returns>
    [McpServerTool(Name = "enable_plugin"), Description("Enable or disable a specific Godot editor plugin by its folder name.")]
    public static async Task<ToolResult> EnablePluginAsync(
        IProjectConfigService projectConfigService,
        [Description("The addon folder name."), Required] string pluginName, 
        [Description("Set to true to enable, false to disable."), Required] bool enabled, 
        CancellationToken cancellationToken = default)
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

    /// <summary>
    /// Installs an integration stub into addons and enables it in project config.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="projectConfigService">Project configuration service.</param>
    /// <param name="integrationName">Human-friendly integration name.</param>
    /// <param name="source">Integration source URL or identifier.</param>
    /// <param name="profile">Integration profile category.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing install status and metadata.</returns>
    [McpServerTool(Name = "install_integration"), Description("Stub an installation of a new Godot integration/addon.")]
    public static async Task<ToolResult> InstallIntegrationAsync(
        IGodotFileService fileService,
        IProjectConfigService projectConfigService,
        [Description("The human-readable name of the integration."), Required] string integrationName, 
        [Description("Source URL or identifier for the integration."), Required] string source, 
        [Description("The category of the integration."), Required] IntegrationProfile profile, 
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
        await EnablePluginAsync(projectConfigService, safeName, true, cancellationToken).ConfigureAwait(false);

        return new ToolResult(true, $"Integration '{integrationName}' installed as {profile}.", new Dictionary<string, string>
        {
            ["name"] = safeName,
            ["source"] = source,
            ["profile"] = profile.ToString()
        });
    }

    /// <summary>
    /// Lists compatibility and maintenance metadata for discovered integrations.
    /// </summary>
    /// <param name="integrationInspector">Integration inspector service.</param>
    /// <returns>Tool result containing compatibility records.</returns>
    [McpServerTool(Name = "list_integration_compatibility"), Description("Check compatibility and maintenance status for all discovered integrations.")]
    public static ToolResult ListIntegrationCompatibility(IIntegrationInspector integrationInspector)
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

    /// <summary>
    /// Verifies a specific integration can be discovered and appears healthy.
    /// </summary>
    /// <param name="integrationInspector">Integration inspector service.</param>
    /// <param name="integrationName">Integration name to verify.</param>
    /// <returns>Tool result indicating health status.</returns>
    [McpServerTool(Name = "verify_integration_health"), Description("Validate that a specific integration is correctly installed and recognized.")]
    public static ToolResult VerifyIntegrationHealth(
        IIntegrationInspector integrationInspector,
        [Description("The name of the integration."), Required] string integrationName)
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
