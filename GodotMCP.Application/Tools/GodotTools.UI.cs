using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text.Json;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using ModelContextProtocol.Server;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    /// <summary>
    /// Lists UI control nodes in a scene.
    /// </summary>
    /// <param name="uiService">UI service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Scene file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing a list of UI controls.</returns>
    [McpServerTool(Name = "ui.list_controls"), Description("List UI controls in a scene under projectPath/scenes/ (same contract as scene.add_node).")]
    public static async Task<ToolResult> UiListControlsAsync(
        IUiService uiService,
        IGodotFileService fileService,
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Scene file name under projectPath/scenes/."), Required] string fileName,
        [Description("Root node type when the scene file is bootstrapped.")] string root_type = "Control",
        CancellationToken cancellationToken = default)
    {
        string scenePath;
        try
        {
            scenePath = await EnsureSceneReadyAsync(fileService, pathResolver, projectPath, fileName, root_type.Trim(), cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message, "Use projectPath + /scenes/ + fileName (with .tscn extension).");
        }

        var controls = await uiService.ListControlsAsync(scenePath, cancellationToken).ConfigureAwait(false);
        var dto = controls.Select(MapControl).ToList();
        return new ToolResult(true, $"Listed {dto.Count} UI control(s).", dto);
    }

    /// <summary>
    /// Adds a UI control node under a parent path.
    /// </summary>
    /// <param name="uiService">UI service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Scene file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="parentNodePath">Parent node path where control is added.</param>
    /// <param name="controlType">Godot control type to add.</param>
    /// <param name="controlName">Control name.</param>
    /// <param name="properties">Optional initial control properties.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing operation status and optional control payload.</returns>
    [McpServerTool(Name = "ui.add_control"), Description("Add a UI control node under a parent path in projectPath/scenes/ (same contract as scene.add_node).")]
    public static async Task<ToolResult> UiAddControlAsync(
        IUiService uiService,
        IGodotFileService fileService,
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Scene file name under projectPath/scenes/."), Required] string fileName,
        [Description("Parent node path (for example: ., UI, UI/HUD)."), Required] string parentNodePath,
        [Description("Control type (for example: Control, Button, Label, PanelContainer)."), Required] string controlType,
        [Description("Control name."), Required] string controlName,
        [Description("Optional initial property map. Values must be primitive JSON values.")] Dictionary<string, JsonElement>? properties = null,
        [Description("Root node type when the scene file is bootstrapped.")] string root_type = "Control",
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(parentNodePath) || IsBlank(controlType) || IsBlank(controlName))
        {
            return Invalid("projectPath, fileName, parentNodePath, controlType, and controlName are required.");
        }
        string scenePath;
        try
        {
            scenePath = await EnsureSceneReadyAsync(fileService, pathResolver, projectPath, fileName, root_type.Trim(), cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message, "Use projectPath + /scenes/ + fileName (with .tscn extension).");
        }

        string? errorMessage = null;
        var normalizedProperties = properties is null
            ? null
            : NormalizeUiProperties(properties, out errorMessage);
        if (properties is not null && normalizedProperties is null)
        {
            return Invalid(errorMessage!);
        }

        var result = await uiService
            .AddControlAsync(new UiAddControlRequest(scenePath, parentNodePath, controlType, controlName, normalizedProperties), cancellationToken)
            .ConfigureAwait(false);

        return ToUiToolResult(result);
    }

    /// <summary>
    /// Applies a layout preset to a control node.
    /// </summary>
    /// <param name="uiService">UI service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Scene file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="controlNodePath">Control node path to update.</param>
    /// <param name="preset">Layout preset name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing operation status and optional control payload.</returns>
    [McpServerTool(Name = "ui.set_layout_preset"), Description("Apply a layout preset to a UI control in projectPath/scenes/. Supported presets: full_rect, top_left, center.")]
    public static async Task<ToolResult> UiSetLayoutPresetAsync(
        IUiService uiService,
        IGodotFileService fileService,
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Scene file name under projectPath/scenes/."), Required] string fileName,
        [Description("Control node path to update."), Required] string controlNodePath,
        [Description("Preset name: full_rect, top_left, or center."), Required] string preset,
        [Description("Root node type when the scene file is bootstrapped.")] string root_type = "Control",
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(controlNodePath) || IsBlank(preset))
        {
            return Invalid("projectPath, fileName, controlNodePath, and preset are required.");
        }
        string scenePath;
        try
        {
            scenePath = await EnsureSceneReadyAsync(fileService, pathResolver, projectPath, fileName, root_type.Trim(), cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message, "Use projectPath + /scenes/ + fileName (with .tscn extension).");
        }

        var result = await uiService
            .SetLayoutPresetAsync(new UiSetLayoutRequest(scenePath, controlNodePath, preset), cancellationToken)
            .ConfigureAwait(false);

        return ToUiToolResult(result);
    }

    /// <summary>
    /// Updates selected UI control properties.
    /// </summary>
    /// <param name="uiService">UI service abstraction.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">Scene file name or relative path under <paramref name="projectPath"/>.</param>
    /// <param name="controlNodePath">Control node path to update.</param>
    /// <param name="properties">Property updates with primitive JSON values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing operation status and optional control payload.</returns>
    [McpServerTool(Name = "ui.set_control_properties"), Description("Update selected properties for a UI control node in projectPath/scenes/ (same contract as scene.add_node).")]
    public static async Task<ToolResult> UiSetControlPropertiesAsync(
        IUiService uiService,
        IGodotFileService fileService,
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Scene file name under projectPath/scenes/."), Required] string fileName,
        [Description("Control node path to update."), Required] string controlNodePath,
        [Description("Property map to update. Values must be primitive JSON values."), Required] Dictionary<string, JsonElement>? properties,
        [Description("Root node type when the scene file is bootstrapped.")] string root_type = "Control",
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(controlNodePath))
        {
            return Invalid("projectPath, fileName and controlNodePath are required.");
        }
        string scenePath;
        try
        {
            scenePath = await EnsureSceneReadyAsync(fileService, pathResolver, projectPath, fileName, root_type.Trim(), cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message, "Use projectPath + /scenes/ + fileName (with .tscn extension).");
        }

        if (properties is null || properties.Count == 0)
        {
            return Invalid("properties must contain at least one entry.");
        }

        var normalizedProperties = NormalizeUiProperties(properties, out var errorMessage);
        if (normalizedProperties is null)
        {
            return Invalid(errorMessage!);
        }

        var result = await uiService
            .SetPropertiesAsync(new UiSetPropertiesRequest(scenePath, controlNodePath, normalizedProperties), cancellationToken)
            .ConfigureAwait(false);

        return ToUiToolResult(result);
    }

    /// <summary>
    /// Sets up a CanvasLayer HUD with score Label and restart Button, plus UiManager script.
    /// </summary>
    [McpServerTool(Name = "setup_ui"), Description("Set up a CanvasLayer HUD with score Label and restart Button, plus UiManager script. Wires to Main script for score/restart updates.")]
    public static async Task<ToolResult> SetupUiAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Script language ('gd' or 'cs'). Defaults to project metadata.")] string? language = null,
        [Description("Game dimension ('2d' or '3d'). Defaults to project metadata.")] string? gameType = null,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(projectPath))
        {
            return Invalid("projectPath is required.");
        }

        var (lang, dim, foundMeta) = await LoadUiProjectMetadataAsync(pathResolver, fileService, projectPath, language, gameType, cancellationToken).ConfigureAwait(false);
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

        var mainScenePath = Path.Combine(baseDir, "scenes", "Main.tscn");
        if (!File.Exists(mainScenePath))
        {
            return new ToolResult(false, "Main.tscn not found. Run initialize_project first.");
        }

        var mainContent = await File.ReadAllTextAsync(mainScenePath, cancellationToken).ConfigureAwait(false);
        var canvasLayerNode = "[node name=\"Hud\" type=\"CanvasLayer\" parent=\".\"]";
        if (!mainContent.Contains("name=\"Hud\""))
        {
            mainContent += $"\n\n{canvasLayerNode}\n";
            await File.WriteAllTextAsync(mainScenePath, mainContent, cancellationToken).ConfigureAwait(false);
        }

        var scriptsDir = Path.Combine(baseDir, "scripts");
        Directory.CreateDirectory(scriptsDir);

        var uiManagerScriptPath = Path.Combine(scriptsDir, $"UiManager.{(lang == "gd" ? "gd" : "cs")}");
        var uiManagerContent = UiGenerateUiManagerScript(lang!, dim!);
        await File.WriteAllTextAsync(uiManagerScriptPath, uiManagerContent, cancellationToken).ConfigureAwait(false);

        var mainScriptPath = Path.Combine(scriptsDir, $"Main.{(lang == "gd" ? "gd" : "cs")}");
        if (File.Exists(mainScriptPath))
        {
            var mainScriptContent = await File.ReadAllTextAsync(mainScriptPath, cancellationToken).ConfigureAwait(false);
            var hasRestartHandler = mainScriptContent.Contains("_on_restart_pressed") || mainScriptContent.Contains("_OnRestartPressed");
            if (!hasRestartHandler)
            {
                if (lang == "gd")
                {
                    var insertPos = mainScriptContent.IndexOf("func get_level");
                    if (insertPos >= 0)
                    {
                        var funcEnd = mainScriptContent.IndexOf('\n', insertPos);
                        mainScriptContent = mainScriptContent.Insert(funcEnd + 1, "\n    _on_restart_pressed():\n        get_tree().reload_current_scene()\n");
                    }
                }
                else
                {
                    var insertPos = mainScriptContent.LastIndexOf("}");
                    if (insertPos >= 0)
                    {
                        mainScriptContent = mainScriptContent.Insert(insertPos, "\n    public void _OnRestartPressed()\n    {\n        GetTree().ReloadCurrentScene();\n    }\n");
                    }
                }
                await File.WriteAllTextAsync(mainScriptPath, mainScriptContent, cancellationToken).ConfigureAwait(false);
            }
        }

        return new ToolResult(true, $"UI scaffold created: CanvasLayer/Hud, UiManager script, and Main script wired for restart.");
    }

    private static async Task<(string? lang, string? dim, bool found)> LoadUiProjectMetadataAsync(
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

    private static string UiGenerateUiManagerScript(string language, string gameType)
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
    /// Normalizes and validates UI property updates from JSON payload.
    /// </summary>
    /// <param name="properties">Incoming JSON property payload.</param>
    /// <param name="errorMessage">Validation error text when normalization fails.</param>
    /// <returns>Normalized property map when valid; otherwise, <see langword="null"/>.</returns>
    private static Dictionary<string, object?>? NormalizeUiProperties(Dictionary<string, JsonElement> properties, out string? errorMessage)
    {
        errorMessage = null;
        var normalized = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in properties)
        {
            if (IsBlank(key))
            {
                errorMessage = "Property keys must be non-empty strings.";
                return null;
            }

            var primitive = ToPrimitiveValue(value);
            if (primitive is null)
            {
                errorMessage = $"Property '{key}' must be a primitive JSON value (string, number, or boolean).";
                return null;
            }

            normalized[key] = primitive;
        }

        return normalized;
    }
    /// <summary>
    /// Converts a UI mutation result into an MCP tool result payload.
    /// </summary>
    /// <param name="result">UI mutation result from the domain layer.</param>
    /// <returns>Transport-friendly tool result.</returns>
    private static ToolResult ToUiToolResult(UiMutationResult result)
    {
        var dto = result.Control is null ? null : MapControl(result.Control);
        return new ToolResult(result.Success, result.Message, dto);
    }

    /// <summary>
    /// Maps a domain UI control model into a transport DTO.
    /// </summary>
    /// <param name="control">Domain control model.</param>
    /// <returns>Mapped UI control DTO.</returns>
    private static UiControlDto MapControl(UiControlInfo control)
        => new(
            control.Name,
            control.Type,
            control.NodePath,
            control.Parent,
            control.Properties.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal));
}

/// <summary>
/// Transport payload describing a UI control node.
/// </summary>
/// <param name="Name">Control node name.</param>
/// <param name="Type">Control node type.</param>
/// <param name="NodePath">Resolved node path.</param>
/// <param name="Parent">Resolved parent node path.</param>
/// <param name="Properties">Control property map.</param>
public sealed record UiControlDto(
    string Name,
    string Type,
    string NodePath,
    string Parent,
    Dictionary<string, string> Properties);
