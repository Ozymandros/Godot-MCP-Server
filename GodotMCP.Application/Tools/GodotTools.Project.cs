using GodotMCP.Core.Models;
using StreamJsonRpc;

namespace GodotMCP.Application.Tools;

public partial class GodotTools
{
    /// <summary>
    /// Create a minimal Godot project file (<c>project.godot</c>) and create standard
    /// project folders (<c>scenes</c>, <c>scripts</c>, <c>addons</c>).
    /// </summary>
    [JsonRpcMethod("create_godot_project")]
    public async Task<ToolResult> CreateGodotProjectAsync(string projectName, CancellationToken cancellationToken = default)
    {
        if (IsBlank(projectName))
        {
            return Invalid("projectName is required.");
        }

        if (fileService.ProjectExists())
        {
            return new ToolResult(false, "A project.godot already exists.");
        }

        var content = $$"""
; Engine configuration file.
; It's best edited using the editor UI and not directly.

config_version=5

[application]

config/name="{{projectName}}"
run/main_scene=""

[dotnet]
project/assembly_name="{{projectName}}"
""";
        await fileService.WriteAsync("res://project.godot", content, cancellationToken).ConfigureAwait(false);
        fileService.EnsureDirectory("res://scenes");
        fileService.EnsureDirectory("res://scripts");
        fileService.EnsureDirectory("res://addons");
        return new ToolResult(true, $"Project '{projectName}' created.");
    }

    /// <summary>
    /// Read project information (name and main scene) from the project's configuration.
    /// </summary>
    [JsonRpcMethod("get_project_info")]
    public async Task<ToolResult> GetProjectInfoAsync(CancellationToken cancellationToken = default)
    {
        if (!fileService.ProjectExists())
        {
            return new ToolResult(false, "No project.godot found.");
        }

        var name = await projectConfigService.GetValueAsync("application", "config/name", cancellationToken).ConfigureAwait(false);
        var mainScene = await projectConfigService.GetValueAsync("application", "run/main_scene", cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, "Project info loaded.", new Dictionary<string, string>
        {
            ["name"] = name,
            ["main_scene"] = mainScene
        });
    }

    /// <summary>
    /// Configure or remove an autoload (singleton) entry in the project configuration.
    /// </summary>
    [JsonRpcMethod("configure_autoload")]
    public async Task<ToolResult> ConfigureAutoloadAsync(string key, string value, bool enabled, CancellationToken cancellationToken = default)
    {
        if (IsBlank(key) || IsBlank(value))
        {
            return Invalid("Autoload key and value are required.");
        }

        if (enabled)
        {
            await projectConfigService.SetValueAsync("autoload", key, $"\"{value}\"", cancellationToken).ConfigureAwait(false);
            return new ToolResult(true, $"Autoload '{key}' added.");
        }

        await projectConfigService.RemoveKeyAsync("autoload", key, cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, $"Autoload '{key}' removed.");
    }

    /// <summary>
    /// Enable a plugin in the project editor settings by adding it to the <c>editor_plugins</c>
    /// section in <c>project.godot</c>.
    /// </summary>
    [JsonRpcMethod("add_plugin")]
    public async Task<ToolResult> AddPluginAsync(string pluginName, CancellationToken cancellationToken = default)
    {
        if (IsBlank(pluginName))
        {
            return Invalid("pluginName is required.");
        }

        await projectConfigService.SetValueAsync("editor_plugins", $"{pluginName}", "true", cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, $"Plugin '{pluginName}' enabled in project config.");
    }
}
