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

        const string mainSceneRelativePath = "scenes/Main.tscn";
        const string mainSceneResPath = "res://scenes/Main.tscn";

        var content = $$"""
; Engine configuration file.
; It's best edited using the editor UI and not directly.

config_version=5

[application]

config/name="{{projectName}}"
run/main_scene="{{mainSceneResPath}}"

[dotnet]
project/assembly_name="{{projectName}}"
""";
        try
        {
            Directory.CreateDirectory(Path.Combine(baseDir, "scenes"));
            Directory.CreateDirectory(Path.Combine(baseDir, "scripts"));
            Directory.CreateDirectory(Path.Combine(baseDir, "addons"));
            await File.WriteAllTextAsync(projectFilePath, content, cancellationToken).ConfigureAwait(false);
            await WriteDefaultMainSceneAsync(baseDir, mainSceneRelativePath, cancellationToken).ConfigureAwait(false);
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

        const string mainSceneRelativePath = "scenes/Main.tscn";
        const string mainSceneResPath = "res://scenes/Main.tscn";

        var content = "; Engine configuration file.\n; It's best edited using the editor UI and not directly.\n\nconfig_version=5\n\n[application]\n\nconfig/name=\"" + projectName + "\"\nrun/main_scene=\"" + mainSceneResPath + "\"\n\n[dotnet]\nproject/assembly_name=\"" + projectName + "\"\n";

        try
        {
            Directory.CreateDirectory(Path.Combine(baseDir, "scenes"));
            Directory.CreateDirectory(Path.Combine(baseDir, "scripts"));
            Directory.CreateDirectory(Path.Combine(baseDir, "addons"));
            await File.WriteAllTextAsync(projectFilePath, content, cancellationToken).ConfigureAwait(false);
            await WriteDefaultMainSceneAsync(baseDir, mainSceneRelativePath, cancellationToken).ConfigureAwait(false);
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

    private static Task WriteDefaultMainSceneAsync(string baseDir, string sceneRelativePath, CancellationToken cancellationToken = default)
    {
        var scenePath = Path.Combine(baseDir, sceneRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var mainSceneText = """
[gd_scene format=3]

[node name="Main" type="Node2D"]
""";
        return File.WriteAllTextAsync(scenePath, mainSceneText, cancellationToken);
    }

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
    /// Creates a Main-first endless runner project structure with Main scene, Level, ObstacleContainer, and optional UI scaffold.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Path resolver for project paths.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="projectName">Project display name.</param>
    /// <param name="language">Script language ('gd' for GDScript, 'cs' for C#).</param>
    /// <param name="gameType">Game dimension ('2d' or '3d').</param>
    /// <param name="includeUi">Whether to include CanvasLayer HUD with score Label and restart Button.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing project creation status.</returns>
    [McpServerTool(Name = "initialize_project"), Description("Create a Main-first endless runner project structure with Main scene, Level, ObstacleContainer, and optional UI scaffold.")]
    public static async Task<ToolResult> InitializeProjectAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("The name of the Godot project.")] string? projectName = null,
        [Description("Script language ('gd' for GDScript, 'cs' for C#). Defaults to 'gd'.")] string language = "gd",
        [Description("Game dimension ('2d' or '3d'). Defaults to '2d'.")] string gameType = "2d",
        [Description("Include CanvasLayer HUD with score Label and restart Button.")] bool includeUi = false,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(projectPath))
        {
            return Invalid("projectPath is required.");
        }

        var lang = language.Trim().ToLowerInvariant();
        var dim = gameType.Trim().ToLowerInvariant();
        if (lang != "gd" && lang != "cs")
        {
            return Invalid("language must be 'gd' (GDScript) or 'cs' (C#).");
        }
        if (dim != "2d" && dim != "3d")
        {
            return Invalid("gameType must be '2d' or '3d'.");
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

        var effectiveName = string.IsNullOrWhiteSpace(projectName) ? Path.GetFileName(baseDir) : projectName;
        if (string.IsNullOrWhiteSpace(effectiveName))
        {
            effectiveName = "New Godot Project";
        }

        var mainSceneRelativePath = "scenes/Main.tscn";
        var mainSceneResPath = "res://scenes/Main.tscn";
        var mainScriptPath = lang == "gd" ? "scripts/Main.gd" : "scripts/Main.cs";

        var projectFilePath = Path.Combine(baseDir, "project.godot");
        var existingProject = File.Exists(projectFilePath);

        if (existingProject)
        {
            var text = await File.ReadAllTextAsync(projectFilePath, cancellationToken).ConfigureAwait(false);
            if (!text.Contains("run/main_scene"))
            {
                await SetProjectConfigValueAsync(baseDir, "application", "run/main_scene", $"\"{mainSceneResPath}\"", cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            var dotnetSection = lang == "cs" ? $"\n[dotnet]\nproject/assembly_name=\"{effectiveName}\"" : "";
            var content = $"""
                ; Engine configuration file.
                ; It's best edited using the editor UI and not directly.

                config_version=5

                [application]

                config/name="{effectiveName}"
                run/main_scene="{mainSceneResPath}"{dotnetSection}
                """;
            Directory.CreateDirectory(Path.Combine(baseDir, "scenes"));
            Directory.CreateDirectory(Path.Combine(baseDir, "scripts"));
            Directory.CreateDirectory(Path.Combine(baseDir, "addons"));
            await File.WriteAllTextAsync(projectFilePath, content, cancellationToken).ConfigureAwait(false);
        }

        var mainScenePath = Path.Combine(baseDir, mainSceneRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var rootType = dim == "2d" ? "Node2D" : "Node3D";
        var levelType = dim == "2d" ? "Node2D" : "Node3D";
        var obstacleType = dim == "2d" ? "Node2D" : "Node3D";

        string mainSceneContent;
        if (dim == "2d")
        {
            mainSceneContent = """
                [gd_scene load_steps=2 format=3]

                [ext_resource type="Script" path="res://scripts/Main.gd" id="1"]

                [node name="Main" type="Node2D"]
                script = ExtResource("1")

                [node name="Level" type="Node2D" parent="."]

                [node name="ObstacleContainer" type="Node2D" parent="."]
                """;
        }
        else
        {
            mainSceneContent = """
                [gd_scene load_steps=2 format=3]

                [ext_resource type="Script" path="res://scripts/Main.cs" id="1"]

                [node name="Main" type="Node3D"]
                script = ExtResource("1")

                [node name="Level" type="Node3D" parent="."]

                [node name="ObstacleContainer" type="Node3D" parent="."]
                """;
        }

        if (includeUi)
        {
            mainSceneContent += "\n\n[node name=\"Hud\" type=\"CanvasLayer\" parent=\".\"]";
        }

        if (!File.Exists(mainScenePath))
        {
            await File.WriteAllTextAsync(mainScenePath, mainSceneContent, cancellationToken).ConfigureAwait(false);
        }

        var mainScriptFullPath = Path.Combine(baseDir, mainScriptPath.Replace('/', Path.DirectorySeparatorChar));
        var scriptContent = GenerateMainScript(lang, dim, includeUi);
        if (!File.Exists(mainScriptFullPath))
        {
            await File.WriteAllTextAsync(mainScriptFullPath, scriptContent, cancellationToken).ConfigureAwait(false);
        }

        if (includeUi)
        {
            var uiManagerPath = Path.Combine(baseDir, (lang == "gd" ? "scripts/UiManager.gd" : "scripts/UiManager.cs"));
            var uiContent = GenerateUiManagerScript(lang, dim);
            if (!File.Exists(uiManagerPath))
            {
                await File.WriteAllTextAsync(uiManagerPath, uiContent, cancellationToken).ConfigureAwait(false);
            }
        }

        var metaPath = Path.Combine(baseDir, ".gdmcp-meta.json");
        if (!File.Exists(metaPath))
        {
            var meta = new Dictionary<string, string>
            {
                ["language"] = lang,
                ["gameType"] = dim
            };
            await File.WriteAllTextAsync(metaPath, System.Text.Json.JsonSerializer.Serialize(meta), cancellationToken).ConfigureAwait(false);
        }

        return new ToolResult(true, $"Project initialized: {effectiveName} ({dim}, {lang}). Main.tscn created with Level and ObstacleContainer.");
    }

    /// <summary>
    /// Creates an actor scene and optionally instantiates it into Main.tscn.
    /// </summary>
    [McpServerTool(Name = "create_actor"), Description("Create an actor scene and optionally instantiate it into Main.tscn. Supports player camera logic.")]
    public static async Task<ToolResult> CreateActorAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Actor name (used for scene file and node name)."), Required] string actorName,
        [Description("Actor role: 'player' for player-controlled, 'enemy' for AI, 'npc' for non-player character.")] string role = "enemy",
        [Description("Script language ('gd' or 'cs'). Defaults to 'gd' or project metadata.")] string? language = null,
        [Description("Game dimension ('2d' or '3d'). Defaults to '2d' or project metadata.")] string? gameType = null,
        [Description("Whether to create a script for this actor.")] bool createScript = true,
        [Description("Whether to instantiate this actor into Main.tscn.")] bool addToMain = true,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(projectPath) || IsBlank(actorName))
        {
            return Invalid("projectPath and actorName are required.");
        }

        var (lang, dim, foundMeta) = await LoadProjectMetadataAsync(pathResolver, fileService, projectPath, language, gameType, cancellationToken).ConfigureAwait(false);
        if (!foundMeta)
        {
            lang ??= "gd";
            dim ??= "2d";
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

        var actorsDir = Path.Combine(baseDir, "scenes", "actors");
        Directory.CreateDirectory(actorsDir);

        var actorFileName = $"{actorName}.tscn";
        var actorScenePath = Path.Combine(actorsDir, actorFileName);
        var nodeType = dim == "2d" ? "CharacterBody2D" : "CharacterBody3D";

        var sceneContent = new System.Text.StringBuilder();
        sceneContent.AppendLine("[gd_scene load_steps=2 format=3]");

        if (createScript)
        {
            var scriptExt = lang == "gd" ? "gd" : "cs";
            var scriptPath = $"res://scenes/actors/{actorName}.{scriptExt}";
            sceneContent.AppendLine($@"[ext_resource type=""Script"" path=""{scriptPath}"" id=""1""]");
        }

        var cameraType = dim == "2d" ? "Camera2D" : "Camera3D";
        if (role == "player")
        {
            sceneContent.AppendLine();
            sceneContent.AppendLine($"[node name=\"{actorName}\" type=\"{nodeType}\"]");
            if (createScript)
            {
                sceneContent.AppendLine("script = ExtResource(\"1\")");
            }
            sceneContent.AppendLine();
            sceneContent.AppendLine($"[node name=\"{cameraType}\" type=\"{cameraType}\" parent=\".\"]");
        }
        else
        {
            sceneContent.AppendLine();
            sceneContent.AppendLine($"[node name=\"{actorName}\" type=\"{nodeType}\"]");
            if (createScript)
            {
                sceneContent.AppendLine("script = ExtResource(\"1\")");
            }
        }

        await File.WriteAllTextAsync(actorScenePath, sceneContent.ToString(), cancellationToken).ConfigureAwait(false);

        if (createScript)
        {
            var scriptsDir = Path.Combine(baseDir, "scenes", "actors");
            Directory.CreateDirectory(scriptsDir);
            var scriptPath = Path.Combine(scriptsDir, $"{actorName}.{(lang == "gd" ? "gd" : "cs")}");
            var scriptContent = GenerateActorScript(lang!, dim!, role, actorName);
            await File.WriteAllTextAsync(scriptPath, scriptContent, cancellationToken).ConfigureAwait(false);
        }

        if (addToMain)
        {
            var mainScenePath = Path.Combine(baseDir, "scenes", "Main.tscn");
            if (!File.Exists(mainScenePath))
            {
                return new ToolResult(true, $"Actor scene created at {actorScenePath}. Main.tscn not found - actor not added to Main.");
            }

            var mainContent = await File.ReadAllTextAsync(mainScenePath, cancellationToken).ConfigureAwait(false);
            var actorInstance = $"[node name=\"{actorName}\" type=\"{nodeType}\" parent=\"Level\"]";
            if (!mainContent.Contains(actorInstance))
            {
                mainContent += $"\n{actorInstance}\n";
                await File.WriteAllTextAsync(mainScenePath, mainContent, cancellationToken).ConfigureAwait(false);
            }
        }

        return new ToolResult(true, $"Actor '{actorName}' created at {actorScenePath}." + (addToMain ? " Added to Main.tscn/Level." : ""));
    }

    private static string GenerateActorScript(string language, string gameType, string role, string actorName)
    {
        var className = char.ToUpper(actorName[0]) + actorName[1..];
        if (language == "gd")
        {
            var baseType = gameType == "2d" ? "CharacterBody2D" : "CharacterBody3D";
            var moveFunc = gameType == "2d"
                ? "func _physics_process(delta: float) -> void:\n    move_and_slide()"
                : "func _physics_process(delta: float) -> void:\n    move_and_slide()";

            var cameraFollow = role == "player"
                ? "\nfunc _process(delta: float) -> void:\n    if has_node(\"Camera2D\"):\n        $Camera2D.position = position"
                : "";

            return $"extends {baseType}\nclass_name {className}\n\n{moveFunc}{cameraFollow}\n";
        }
        else
        {
            var baseType = gameType == "2d" ? "CharacterBody2D" : "CharacterBody3D";
            var cameraFollow = role == "player"
                ? "\n    public override void _Process(double delta)\n    {\n        var camera = GetNodeOrNull<Camera2D>(\"Camera2D\");\n        if (camera != null) camera.Position = Position;\n    }"
                : "";

            return $$"""
                using Godot;

                public partial class {{className}} : {{baseType}}
                {
                    public override void _PhysicsProcess(double delta)
                    {
                        MoveAndSlide();
                    }{{cameraFollow}}
                }
                """;
        }
    }

    private static async Task<(string? lang, string? dim, bool found)> LoadProjectMetadataAsync(
        IPathResolver pathResolver,
        IGodotFileService fileService,
        string projectPath,
        string? explicitLang,
        string? explicitDim,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(explicitLang) || !string.IsNullOrEmpty(explicitDim))
        {
            return (explicitLang, explicitDim, false);
        }

        try
        {
            var baseDir = NormalizeProjectPath(pathResolver, projectPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var metaPath = Path.Combine(baseDir, ".gdmcp-meta.json");
            if (File.Exists(metaPath))
            {
                var content = await File.ReadAllTextAsync(metaPath, cancellationToken).ConfigureAwait(false);
                using var doc = System.Text.Json.JsonDocument.Parse(content);
                var root = doc.RootElement;
                var lang = root.TryGetProperty("language", out var l) ? l.GetString() : null;
                var dim = root.TryGetProperty("gameType", out var d) ? d.GetString() : null;
                return (lang, dim, true);
            }
        }
        catch
        {
        }
        return (null, null, false);
    }

    /// <summary>
    /// Creates a spawnable obstacle/scene with Area2D/Area3D, collision shape, and off-screen cleanup notifier.
    /// </summary>
    [McpServerTool(Name = "create_spawnable"), Description("Create a spawnable obstacle scene with Area2D/Area3D, collision shape, and off-screen cleanup. Injects PackedScene export and signal wiring into Main script.")]
    public static async Task<ToolResult> CreateSpawnableAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Spawnable name (used for scene file and export variable)."), Required] string spawnableName,
        [Description("Script language ('gd' or 'cs'). Defaults to project metadata.")] string? language = null,
        [Description("Game dimension ('2d' or '3d'). Defaults to project metadata.")] string? gameType = null,
        [Description("Whether to create a script for this spawnable.")] bool createScript = true,
        [Description("Whether to add PackedScene export and signal to Main script.")] bool wireToMain = true,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(projectPath) || IsBlank(spawnableName))
        {
            return Invalid("projectPath and spawnableName are required.");
        }

        var (lang, dim, foundMeta) = await LoadProjectMetadataAsync(pathResolver, fileService, projectPath, language, gameType, cancellationToken).ConfigureAwait(false);
        if (!foundMeta)
        {
            lang ??= "gd";
            dim ??= "2d";
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

        var spawnablesDir = Path.Combine(baseDir, "scenes", "spawnables");
        Directory.CreateDirectory(spawnablesDir);

        var spawnableFileName = $"{spawnableName}.tscn";
        var spawnableScenePath = Path.Combine(spawnablesDir, spawnableFileName);

        var areaType = dim == "2d" ? "Area2D" : "Area3D";
        var shapeType = dim == "2d" ? "CollisionShape2D" : "CollisionShape3D";
        var shapeResource = dim == "2d" ? "RectangleShape2D" : "BoxShape3D";
        var notifierType = dim == "2d" ? "VisibleOnScreenNotifier2D" : "Area3D";

        var sceneContent = new System.Text.StringBuilder();
        sceneContent.AppendLine("[gd_scene load_steps=3 format=3]");

        if (createScript)
        {
            var scriptExt = lang == "gd" ? "gd" : "cs";
            var scriptPath = $"res://scenes/spawnables/{spawnableName}.{scriptExt}";
            sceneContent.AppendLine($@"[ext_resource type=""Script"" path=""{scriptPath}"" id=""1""]");
        }

        sceneContent.AppendLine();
        sceneContent.AppendLine($"[node name=\"{spawnableName}\" type=\"{areaType}\"]");
        if (createScript)
        {
            sceneContent.AppendLine("script = ExtResource(\"1\")");
        }

        sceneContent.AppendLine();
        sceneContent.AppendLine($"[node name=\"{shapeType}\" type=\"{shapeType}\" parent=\".\"]");
        sceneContent.AppendLine($"[sub_resource type=\"{shapeResource}\" id=\"1\"]");

        if (dim == "2d")
        {
            sceneContent.AppendLine();
            sceneContent.AppendLine($"[node name=\"{notifierType}\" type=\"{notifierType}\" parent=\".\"]");
        }
        else
        {
            sceneContent.AppendLine();
            sceneContent.AppendLine("[node name=\"DespawnArea\" type=\"Area3D\" parent=\".\"]");
            sceneContent.AppendLine("[node name=\"CollisionShape3D\" type=\"CollisionShape3D\" parent=\"DespawnArea\"]");
            sceneContent.AppendLine("[sub_resource type=\"BoxShape3D\" id=\"2\"]");
        }

        await File.WriteAllTextAsync(spawnableScenePath, sceneContent.ToString(), cancellationToken).ConfigureAwait(false);

        if (createScript)
        {
            var scriptsDir = Path.Combine(baseDir, "scenes", "spawnables");
            Directory.CreateDirectory(scriptsDir);
            var scriptPath = Path.Combine(scriptsDir, $"{spawnableName}.{(lang == "gd" ? "gd" : "cs")}");
            var scriptContent = GenerateSpawnableScript(lang!, dim!, spawnableName);
            await File.WriteAllTextAsync(scriptPath, scriptContent, cancellationToken).ConfigureAwait(false);
        }

        if (wireToMain)
        {
            var mainScriptPath = Path.Combine(baseDir, "scripts", $"Main.{(lang == "gd" ? "gd" : "cs")}");
            if (File.Exists(mainScriptPath))
            {
                var mainContent = await File.ReadAllTextAsync(mainScriptPath, cancellationToken).ConfigureAwait(false);
                var exportVar = $"var {spawnableName}_scene: PackedScene";
                if (!mainContent.Contains(exportVar))
                {
                    if (lang == "gd")
                    {
                        var insertPos = mainContent.IndexOf("extends");
                        if (insertPos >= 0)
                        {
                            insertPos = mainContent.IndexOf('\n', insertPos) + 1;
                            mainContent = mainContent.Insert(insertPos, $"\n@export var {spawnableName}_scene: PackedScene\n");
                        }
                    }
                    else
                    {
                        var insertPos = mainContent.IndexOf("public partial class");
                        if (insertPos >= 0)
                        {
                            insertPos = mainContent.IndexOf('\n', insertPos) + 1;
                            mainContent = mainContent.Insert(insertPos, $"    [Export] public PackedScene {spawnableName}Scene;\n");
                        }
                    }
                    await File.WriteAllTextAsync(mainScriptPath, mainContent, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        return new ToolResult(true, $"Spawnable '{spawnableName}' created at {spawnableScenePath}." + (wireToMain ? $" PackedScene export added to Main script." : ""));
    }

    private static string GenerateSpawnableScript(string language, string gameType, string spawnableName)
    {
        var className = char.ToUpper(spawnableName[0]) + spawnableName[1..];
        if (language == "gd")
        {
            var baseType = gameType == "2d" ? "Area2D" : "Area3D";
            var onScreenExit = gameType == "2d"
                ? "func _on_visible_on_screen_exited() -> void:\n    queue_free()"
                : """
                func _on_despawn_area_entered(area: Area3D) -> void:
                    if area.name == "DespawnArea":
                        queue_free()
                """;

            var signalConnect = gameType == "2d"
                ? "$VisibleOnScreenNotifier2D.screen_exited.connect(_on_visible_on_screen_exited)"
                : "$DespawnArea.area_entered.connect(_on_despawn_area_entered)";

            return $"""
                extends {baseType}
                class_name {className}

                func _ready() -> void:
                    {signalConnect}

                {onScreenExit}
                """;
        }
        else
        {
            var baseType = gameType == "2d" ? "Area2D" : "Area3D";
            var onScreenExit = gameType == "2d"
                ? "    private void _OnVisibleOnScreenExited()\n    {\n        QueueFree();\n    }"
                : """
                private void _OnDespawnAreaEntered(Area3D area)
                {
                    if (area.Name == "DespawnArea") QueueFree();
                }
                """;

            var signalConnect = gameType == "2d"
                ? "GetNode<VisibleOnScreenNotifier2D>(\"VisibleOnScreenNotifier2D\").ScreenExited += _OnVisibleOnScreenExited;"
                : "GetNode<Area3D>(\"DespawnArea\").AreaEntered += _OnDespawnAreaEntered;";

            return "using Godot;\n\n" +
                $"public partial class {className} : {baseType}\n" +
                "{\n" +
                "    public override void _Ready()\n" +
                "    {\n" +
                $"        {signalConnect}\n" +
                "    }\n" +
                $"{onScreenExit}\n" +
                "}\n";
        }
    }

    private static string GenerateMainScript(string language, string gameType, bool includeUi)
    {
        if (language == "gd")
        {
            if (includeUi)
            {
                return """
                extends Node2D

                var score: int = 0

                func _ready() -> void:
                    $ObstacleContainer.connect("obstacle_hit", _on_obstacle_hit)
                    if has_node("Hud"):
                        $Hud/CanvasLayer/ScoreLabel.text = "Score: 0"

                func _on_obstacle_hit(obstacle: Node) -> void:
                    score += 1
                    if has_node("Hud"):
                        $Hud/CanvasLayer/ScoreLabel.text = "Score: " + str(score)

                func _on_restart_pressed() -> void:
                    get_tree().reload_current_scene()

                func add_spawnable(spawnable_scene: PackedScene) -> void:
                    var instance = spawnable_scene.instantiate()
                    $ObstacleContainer.add_child(instance)

                func get_level() -> Node2D:
                    return $Level as Node2D
                """;
            }
            return """
                extends Node2D

                func _ready() -> void:
                    $ObstacleContainer.connect("obstacle_hit", _on_obstacle_hit)

                func _on_obstacle_hit(obstacle: Node) -> void:
                    pass

                func add_spawnable(spawnable_scene: PackedScene) -> void:
                    var instance = spawnable_scene.instantiate()
                    $ObstacleContainer.add_child(instance)

                func get_level() -> Node2D:
                    return $Level as Node2D
                """;
        }
        else
        {
            if (includeUi)
            {
                return """
                using Godot;

                public partial class Main : Node2D
                {
                    private int _score = 0;

                    public override void _Ready()
                    {
                        var obstacleContainer = GetNode<Node>("ObstacleContainer");
                        obstacleContainer.Connect("obstacle_hit", Callable.From(_OnObstacleHit));
                        var hud = GetNodeOrNull<Node2D>("Hud");
                        if (hud != null)
                        {
                            var label = hud.GetNodeOrNull<Label>("CanvasLayer/ScoreLabel");
                            if (label != null) label.Text = "Score: 0";
                        }
                    }

                    private void _OnObstacleHit(Node obstacle)
                    {
                        _score++;
                        var hud = GetNodeOrNull<Node2D>("Hud");
                        if (hud != null)
                        {
                            var label = hud.GetNodeOrNull<Label>("CanvasLayer/ScoreLabel");
                            if (label != null) label.Text = $"Score: {_score}";
                        }
                    }

                    public void _OnRestartPressed()
                    {
                        GetTree().ReloadCurrentScene();
                    }

                    public void AddSpawnable(PackedScene spawnableScene)
                    {
                        var instance = spawnableScene.Instantiate();
                        GetNode<Node>("ObstacleContainer").AddChild(instance);
                    }

                    public Node2D GetLevel()
                    {
                        return GetNode<Node2D>("Level");
                    }
                }
                """;
            }
            return """
                using Godot;

                public partial class Main : Node2D
                {
                    public override void _Ready()
                    {
                        var obstacleContainer = GetNode<Node>("ObstacleContainer");
                        obstacleContainer.Connect("obstacle_hit", Callable.From(_OnObstacleHit));
                    }

                    private void _OnObstacleHit(Node obstacle)
                    {
                    }

                    public void AddSpawnable(PackedScene spawnableScene)
                    {
                        var instance = spawnableScene.Instantiate();
                        GetNode<Node>("ObstacleContainer").AddChild(instance);
                    }

                    public Node2D GetLevel()
                    {
                        return GetNode<Node2D>("Level");
                    }
                }
                """;
        }
    }

    private static string GenerateUiManagerScript(string language, string gameType)
    {
        if (language == "gd")
        {
            return """
            extends CanvasLayer

            @onready var score_label: Label = $ScoreLabel
            @onready var restart_button: Button = $RestartButton

            func _ready() -> void:
                restart_button.pressed.connect(_on_restart_pressed)

            func update_score(value: int) -> void:
                score_label.text = "Score: " + str(value)

            func _on_restart_pressed() -> void:
                get_tree().reload_current_scene()
            """;
        }
        else
        {
            return """
            using Godot;

            public partial class UiManager : CanvasLayer
            {
                private Label _scoreLabel;
                private Button _restartButton;

                public override void _Ready()
                {
                    _scoreLabel = GetNode<Label>("ScoreLabel");
                    _restartButton = GetNode<Button>("RestartButton");
                    _restartButton.Pressed += _OnRestartPressed;
                }

                public void UpdateScore(int value)
                {
                    _scoreLabel.Text = $"Score: {value}";
                }

                private void _OnRestartPressed()
                {
                    GetTree().ReloadCurrentScene();
                }
            }
            """;
        }
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
