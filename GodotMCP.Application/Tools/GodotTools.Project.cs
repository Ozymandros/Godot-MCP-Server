using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using ModelContextProtocol.Server;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    /// <summary>
    /// Creates a new Godot project structure and minimal <c>project.godot</c> file.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="projectName">Project display name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing project creation status.</returns>
    [McpServerTool(Name = "create_godot_project"), Description("Create a new Godot 4.x project at the current working directory.")]
    public static async Task<ToolResult> CreateGodotProjectAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("The name of the Godot project."), Required] string projectName,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(projectName) || IsBlank(projectPath))
        {
            return Invalid("projectPath and projectName are required.");
        }
        try
        {
            _ = NormalizeProjectPath(pathResolver, projectPath);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
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
        await fileService.WriteAsync(pathResolver.ResolvePath("project.godot"), content, cancellationToken).ConfigureAwait(false);
        fileService.EnsureDirectory(pathResolver.ResolvePath("scenes"));
        fileService.EnsureDirectory(pathResolver.ResolvePath("scripts"));
        fileService.EnsureDirectory(pathResolver.ResolvePath("addons"));
        return new ToolResult(true, $"Project '{projectName}' created.");
    }

    /// <summary>
    /// Reads basic project configuration values from <c>project.godot</c>.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="projectConfigService">Project configuration service.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing project name and main scene fields.</returns>
    [McpServerTool(Name = "get_project_info"), Description("Retrieve basic configuration from project.godot.")]
    public static async Task<ToolResult> GetProjectInfoAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        IProjectConfigService projectConfigService,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _ = NormalizeProjectPath(pathResolver, projectPath);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

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
    /// Adds or removes an autoload singleton entry in project configuration.
    /// </summary>
    /// <param name="projectConfigService">Project configuration service.</param>
    /// <param name="key">Autoload key.</param>
    /// <param name="value">Autoload resource path.</param>
    /// <param name="enabled">Whether to add or remove the key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing mutation status.</returns>
    [McpServerTool(Name = "configure_autoload"), Description("Enable or disable a singleton autoload in project.godot.")]
    public static async Task<ToolResult> ConfigureAutoloadAsync(
        IPathResolver pathResolver,
        IProjectConfigService projectConfigService,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("The autoload unique key."), Required] string key,
        [Description("Script or scene path (absolute or project-relative) stored in project.godot."), Required] string value,
        [Description("Set to true to add, false to remove."), Required] bool enabled,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(projectPath) || IsBlank(key) || IsBlank(value))
        {
            return Invalid("projectPath, key and value are required.");
        }
        try
        {
            _ = NormalizeProjectPath(pathResolver, projectPath);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        if (enabled)
        {
            var trimmed = value.Trim().Trim('"');
            var toolSingleton = trimmed.StartsWith("*", StringComparison.Ordinal);
            var pathPart = toolSingleton ? trimmed[1..] : trimmed;
            var godotRef = pathResolver.ToGodotResPath(pathResolver.ResolvePath(pathPart));
            var quoted = toolSingleton ? $"\"*{godotRef}\"" : $"\"{godotRef}\"";
            await projectConfigService.SetValueAsync("autoload", key, quoted, cancellationToken).ConfigureAwait(false);
            return new ToolResult(true, $"Autoload '{key}' added.");
        }

        await projectConfigService.RemoveKeyAsync("autoload", key, cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, $"Autoload '{key}' removed.");
    }

    /// <summary>
    /// Enables an editor plugin entry in project configuration.
    /// </summary>
    /// <param name="projectConfigService">Project configuration service.</param>
    /// <param name="pluginName">Plugin folder name under the <c>addons</c> directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing plugin enablement status.</returns>
    [McpServerTool(Name = "add_plugin"), Description("Register an editor plugin in project.godot.")]
    public static async Task<ToolResult> AddPluginAsync(
        IPathResolver pathResolver,
        IProjectConfigService projectConfigService,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("The folder name of the plugin under addons/."), Required] string pluginName,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(projectPath) || IsBlank(pluginName))
        {
            return Invalid("projectPath and pluginName are required.");
        }
        try
        {
            _ = NormalizeProjectPath(pathResolver, projectPath);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        await projectConfigService.SetValueAsync("editor_plugins", $"{pluginName}", "true", cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, $"Plugin '{pluginName}' enabled in project config.");
    }
}
