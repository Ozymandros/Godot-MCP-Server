using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text.Json;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using GodotMCP.Core.ProjectSettings;
using ModelContextProtocol.Server;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    /// <summary>
    /// Creates a new Godot project structure and minimal <c>project.godot</c> file.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Path resolver for project paths.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
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
        string baseDir;
        try
        {
            baseDir = NormalizeProjectPath(pathResolver, projectPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        var projectFilePath = Path.Combine(baseDir, "project.godot");
        if (File.Exists(projectFilePath))
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
        try
        {
            Directory.CreateDirectory(Path.Combine(baseDir, "scenes"));
            Directory.CreateDirectory(Path.Combine(baseDir, "scripts"));
            Directory.CreateDirectory(Path.Combine(baseDir, "addons"));
            await File.WriteAllTextAsync(projectFilePath, content, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"Failed to create project files: {ex.Message}");
        }
        return new ToolResult(true, $"Project '{projectName}' created.");
    }

    // Helper: create a minimal project.godot at the specified baseDir.
    private static async Task<ToolResult> CreateProjectFileAtAsync(string baseDir, string projectName, CancellationToken cancellationToken = default)
    {
        var projectFilePath = Path.Combine(baseDir, "project.godot");
        if (File.Exists(projectFilePath))
        {
            return new ToolResult(false, "A project.godot already exists.");
        }

        var content = "; Engine configuration file.\n; It's best edited using the editor UI and not directly.\n\nconfig_version=5\n\n[application]\n\nconfig/name=\"" + projectName + "\"\nrun/main_scene=\"\"\n\n[dotnet]\nproject/assembly_name=\"" + projectName + "\"\n";

        try
        {
            Directory.CreateDirectory(Path.Combine(baseDir, "scenes"));
            Directory.CreateDirectory(Path.Combine(baseDir, "scripts"));
            Directory.CreateDirectory(Path.Combine(baseDir, "addons"));
            await File.WriteAllTextAsync(projectFilePath, content, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"Failed to create project files: {ex.Message}");
        }

        return new ToolResult(true, $"Project '{projectName}' created.");
    }

    // Helper: set or insert a key value inside a project.godot located at baseDir.
    private static Task SetProjectConfigValueAsync(string baseDir, string section, string key, string value, CancellationToken cancellationToken = default) =>
        ProjectGodotMerger.SetSectionKeyAsync(baseDir, section, key, value, cancellationToken);

    // Helper: remove a key from a project.godot located at baseDir.
    private static Task RemoveProjectConfigKeyAsync(string baseDir, string section, string key, CancellationToken cancellationToken = default) =>
        ProjectGodotMerger.RemoveSectionKeyAsync(baseDir, section, key, cancellationToken);

    /// <summary>
    /// Reads basic project configuration values from <c>project.godot</c>.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Path resolver for project paths.</param>
    /// <param name="projectConfigService">Project configuration service.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
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
        string baseDir;
        try
        {
            baseDir = NormalizeProjectPath(pathResolver, projectPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        var projectFile = Path.Combine(baseDir, "project.godot");
        if (!File.Exists(projectFile))
        {
            var defaultName = Path.GetFileName(baseDir);
            if (string.IsNullOrWhiteSpace(defaultName))
            {
                defaultName = "New Godot Project";
            }

            var created = await CreateGodotProjectAsync(fileService, pathResolver, projectPath, defaultName, cancellationToken).ConfigureAwait(false);
            if (!created.Success)
            {
                return created;
            }
        }

        var text = await File.ReadAllTextAsync(projectFile, cancellationToken).ConfigureAwait(false);
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        string name = string.Empty;
        string mainScene = string.Empty;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "[application]")
            {
                for (var j = i + 1; j < lines.Length; j++)
                {
                    var line = lines[j].Trim();
                    if (line.StartsWith("[", StringComparison.Ordinal))
                    {
                        break;
                    }

                    if (line.StartsWith("config/name=", StringComparison.Ordinal))
                    {
                        name = line[("config/name=".Length)..].Trim().Trim('"');
                    }

                    if (line.StartsWith("run/main_scene=", StringComparison.Ordinal))
                    {
                        mainScene = line[("run/main_scene=".Length)..].Trim().Trim('"');
                    }
                }

                break;
            }
        }

        return new ToolResult(true, "Project info loaded.", new Dictionary<string, string>
        {
            ["name"] = name,
            ["main_scene"] = mainScene
        });
    }

    /// <summary>
    /// Adds or removes an autoload singleton entry in project configuration.
    /// </summary>
    /// <param name="pathResolver">Path resolver for project paths.</param>
    /// <param name="projectConfigService">Project configuration service.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
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
        string baseDir;
        try
        {
            baseDir = NormalizeProjectPath(pathResolver, projectPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        var projectFile = Path.Combine(baseDir, "project.godot");
        if (!File.Exists(projectFile))
        {
            var defaultName = Path.GetFileName(baseDir);
            if (string.IsNullOrWhiteSpace(defaultName))
            {
                defaultName = "New Godot Project";
            }

            var createResult = await CreateProjectFileAtAsync(baseDir, defaultName, cancellationToken).ConfigureAwait(false);
            if (!createResult.Success)
            {
                return createResult;
            }
        }

        if (enabled)
        {
            var trimmed = value.Trim().Trim('"');
            var toolSingleton = trimmed.StartsWith("*", StringComparison.Ordinal);
            var pathPart = toolSingleton ? trimmed[1..] : trimmed;
            var godotRef = pathResolver.ToGodotResPath(pathResolver.ResolvePath(pathPart));
            var quoted = toolSingleton ? $"\"*{godotRef}\"" : $"\"{godotRef}\"";
            await SetProjectConfigValueAsync(baseDir, "autoload", key, quoted, cancellationToken).ConfigureAwait(false);
            return new ToolResult(true, $"Autoload '{key}' added.");
        }

        await RemoveProjectConfigKeyAsync(baseDir, "autoload", key, cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, $"Autoload '{key}' removed.");
    }

    /// <summary>
    /// Adds a single-key input action to an empty <c>[input]</c> section (or creates the section).
    /// </summary>
    /// <param name="pathResolver">Path resolver for project paths.</param>
    /// <param name="projectConfigService">Unused; keeps MCP DI signature consistent.</param>
    /// <param name="projectPath">Project directory.</param>
    /// <param name="actionName">Input map action name.</param>
    /// <param name="physical_key_code">Godot <c>physical_keycode</c> integer (e.g. 32 for Space).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result.</returns>
    [McpServerTool(Name = "project.add_input_action_key"), Description("Add one input map action with a physical key; only when [input] is empty or missing (avoids corrupting existing maps).")]
    public static async Task<ToolResult> AddInputActionKeyAsync(
        IPathResolver pathResolver,
        IProjectConfigService projectConfigService,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Action name (e.g. jump)."), Required] string actionName,
        [Description("Godot physical_keycode (see Godot @GlobalScope KEY_*)."), Required] int physical_key_code,
        CancellationToken cancellationToken = default)
    {
        _ = projectConfigService;
        if (IsBlank(projectPath) || IsBlank(actionName))
        {
            return Invalid("projectPath and actionName are required.");
        }

        string baseDir;
        try
        {
            baseDir = NormalizeProjectPath(pathResolver, projectPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        var projectFile = Path.Combine(baseDir, "project.godot");
        if (!File.Exists(projectFile))
        {
            return new ToolResult(false, "project.godot not found. Create a project first.");
        }

        var text = await File.ReadAllTextAsync(projectFile, cancellationToken).ConfigureAwait(false);
        if (!ProjectInputMapEditor.TryAppendPhysicalKeyAction(text, actionName, physical_key_code, out var updated, out var message))
        {
            return new ToolResult(false, message);
        }

        await File.WriteAllTextAsync(projectFile, updated, cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, message);
    }

    [McpServerTool(Name = "project.input_list_actions"), Description("List input actions and their events from project.godot [input].")]
    public static async Task<ToolResult> ProjectInputListActionsAsync(
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        CancellationToken cancellationToken = default)
    {
        string baseDir;
        try
        {
            baseDir = NormalizeProjectPath(pathResolver, projectPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        var projectFile = Path.Combine(baseDir, "project.godot");
        if (!File.Exists(projectFile))
        {
            return new ToolResult(false, "project.godot not found. Create a project first.");
        }

        var text = await File.ReadAllTextAsync(projectFile, cancellationToken).ConfigureAwait(false);
        if (!ProjectInputMapEditor.TryListActions(text, out var actions, out var message))
        {
            return new ToolResult(false, message);
        }

        var dto = actions.Select(a => new
        {
            name = a.Name,
            deadzone = a.Deadzone,
            events = a.Events.Select(e => new
            {
                eventType = e.EventType,
                canonicalKey = e.CanonicalKey,
                serializedType = e.SerializedType,
                fields = e.SerializedFields
            }).ToList()
        }).ToList();

        return new ToolResult(true, $"Listed {dto.Count} input action(s).", dto);
    }

    [McpServerTool(Name = "project.input_add_action"), Description("Add an input action in project.godot [input].")]
    public static async Task<ToolResult> ProjectInputAddActionAsync(
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Action name (e.g. jump)."), Required] string actionName,
        [Description("Action deadzone value.")] double deadzone = 0.5,
        CancellationToken cancellationToken = default)
    {
        return await EditInputMapAsync(pathResolver, projectPath, cancellationToken, (string text, out string updated, out string message)
            => ProjectInputMapEditor.TryAddAction(text, actionName, deadzone, overwriteIfExists: false, out updated, out message)).ConfigureAwait(false);
    }

    [McpServerTool(Name = "project.input_update_action"), Description("Update deadzone for an existing input action.")]
    public static async Task<ToolResult> ProjectInputUpdateActionAsync(
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Action name (e.g. jump)."), Required] string actionName,
        [Description("Updated deadzone value.")] double deadzone,
        CancellationToken cancellationToken = default)
    {
        return await EditInputMapAsync(pathResolver, projectPath, cancellationToken, (string text, out string updated, out string message)
            => ProjectInputMapEditor.TryUpdateDeadzone(text, actionName, deadzone, out updated, out message)).ConfigureAwait(false);
    }

    [McpServerTool(Name = "project.input_remove_action"), Description("Remove an input action from project.godot [input].")]
    public static async Task<ToolResult> ProjectInputRemoveActionAsync(
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Action name to remove."), Required] string actionName,
        CancellationToken cancellationToken = default)
    {
        return await EditInputMapAsync(pathResolver, projectPath, cancellationToken, (string text, out string updated, out string message)
            => ProjectInputMapEditor.TryRemoveAction(text, actionName, out updated, out message)).ConfigureAwait(false);
    }

    [McpServerTool(Name = "project.input_add_event"), Description("Add an input event (key, mouse_button, joypad_button, joypad_motion) to an action.")]
    public static async Task<ToolResult> ProjectInputAddEventAsync(
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Action name to edit."), Required] string actionName,
        [Description("Event type: key, mouse_button, joypad_button, joypad_motion."), Required] string eventType,
        [Description("Event payload object. key: physical_key_code|key_code + modifiers; mouse_button: button_index; joypad_button: button_index; joypad_motion: axis + axis_value.")]
        Dictionary<string, JsonElement>? eventPayload = null,
        [Description("Allow duplicate events on the same action.")] bool allowDuplicate = false,
        CancellationToken cancellationToken = default)
    {
        if (!TryBuildProjectInputEvent(eventType, eventPayload, out var evt, out var error))
        {
            return Invalid(error);
        }

        return await EditInputMapAsync(pathResolver, projectPath, cancellationToken, (string text, out string updated, out string message)
            => ProjectInputMapEditor.TryAddEvent(text, actionName, evt!, allowDuplicate, out updated, out message)).ConfigureAwait(false);
    }

    [McpServerTool(Name = "project.input_remove_event"), Description("Remove an input event from an action using explicit event fields.")]
    public static async Task<ToolResult> ProjectInputRemoveEventAsync(
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Action name to edit."), Required] string actionName,
        [Description("Event type: key, mouse_button, joypad_button, joypad_motion."), Required] string eventType,
        [Description("Event payload object matching the event identity to remove.")] Dictionary<string, JsonElement>? eventPayload = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryBuildProjectInputEvent(eventType, eventPayload, out var evt, out var error))
        {
            return Invalid(error);
        }

        return await EditInputMapAsync(pathResolver, projectPath, cancellationToken, (string text, out string updated, out string message)
            => ProjectInputMapEditor.TryRemoveEvent(text, actionName, evt!, out updated, out message)).ConfigureAwait(false);
    }

    private static bool TryBuildProjectInputEvent(
        string eventType,
        Dictionary<string, JsonElement>? payload,
        out ProjectInputEvent? inputEvent,
        out string error)
    {
        inputEvent = null;
        error = string.Empty;
        payload ??= new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var kind = eventType.Trim().ToLowerInvariant();

        bool Bool(string key, bool d = false)
            => payload.TryGetValue(key, out var j) && j.ValueKind is JsonValueKind.True or JsonValueKind.False ? j.GetBoolean() : d;

        int Int(string key, int d = 0)
            => payload.TryGetValue(key, out var j) && j.ValueKind == JsonValueKind.Number && j.TryGetInt32(out var v) ? v : d;

        double Double(string key, double d = 0)
            => payload.TryGetValue(key, out var j) && j.ValueKind == JsonValueKind.Number && j.TryGetDouble(out var v) ? v : d;

        switch (kind)
        {
            case "key":
                inputEvent = ProjectInputEvent.Key(
                    physicalKeyCode: Int("physical_key_code", 0),
                    keyCode: Int("key_code", 0),
                    shift: Bool("shift", false),
                    alt: Bool("alt", false),
                    ctrl: Bool("ctrl", false),
                    meta: Bool("meta", false));
                return true;
            case "mouse_button":
                inputEvent = ProjectInputEvent.MouseButton(Int("button_index", 1), Bool("double_click", false));
                return true;
            case "joypad_button":
                inputEvent = ProjectInputEvent.JoypadButton(Int("button_index", 0));
                return true;
            case "joypad_motion":
                inputEvent = ProjectInputEvent.JoypadMotion(Int("axis", 0), Double("axis_value", 1));
                return true;
            default:
                error = "eventType must be one of: key, mouse_button, joypad_button, joypad_motion.";
                return false;
        }
    }

    private delegate bool InputMapEditDelegate(string text, out string updated, out string message);

    private static async Task<ToolResult> EditInputMapAsync(
        IPathResolver pathResolver,
        string projectPath,
        CancellationToken cancellationToken,
        InputMapEditDelegate edit)
    {
        if (IsBlank(projectPath))
        {
            return Invalid("projectPath is required.");
        }

        string baseDir;
        try
        {
            baseDir = NormalizeProjectPath(pathResolver, projectPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        var projectFile = Path.Combine(baseDir, "project.godot");
        if (!File.Exists(projectFile))
        {
            return new ToolResult(false, "project.godot not found. Create a project first.");
        }

        var text = await File.ReadAllTextAsync(projectFile, cancellationToken).ConfigureAwait(false);
        if (!edit(text, out var updated, out var message))
        {
            return new ToolResult(false, message);
        }

        await File.WriteAllTextAsync(projectFile, updated, cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, message);
    }

    /// <summary>
    /// Enables an editor plugin entry in project configuration.
    /// </summary>
    /// <param name="pathResolver">Path resolver for project paths.</param>
    /// <param name="projectConfigService">Project configuration service.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
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

        string baseDir;
        try
        {
            baseDir = NormalizeProjectPath(pathResolver, projectPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        var projectFile = Path.Combine(baseDir, "project.godot");
        if (!File.Exists(projectFile))
        {
            var defaultName = Path.GetFileName(baseDir);
            if (string.IsNullOrWhiteSpace(defaultName))
            {
                defaultName = "New Godot Project";
            }

            var createResult = await CreateProjectFileAtAsync(baseDir, defaultName, cancellationToken).ConfigureAwait(false);
            if (!createResult.Success)
            {
                return createResult;
            }
        }

        await SetProjectConfigValueAsync(baseDir, "editor_plugins", pluginName, "true", cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, $"Plugin '{pluginName}' enabled in project config.");
    }
}
