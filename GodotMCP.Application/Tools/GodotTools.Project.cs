using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
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
    private static async Task SetProjectConfigValueAsync(string baseDir, string section, string key, string value, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(baseDir, "project.godot");
        var text = File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false) : string.Empty;
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
        var sectionHeader = $"[{section}]";
        var sectionLine = lines.FindIndex(l => l.Trim() == sectionHeader);
        var keyLine = $"{key}={value}";
        if (sectionLine < 0)
        {
            if (lines.Count > 0 && !string.IsNullOrEmpty(lines[^1]))
            {
                lines.Add(string.Empty);
            }

            lines.Add(sectionHeader);
            lines.Add(keyLine);
        }
        else
        {
            // locate end of section
            var insertAt = sectionLine + 1;
            while (insertAt < lines.Count && !lines[insertAt].StartsWith("[", StringComparison.Ordinal))
            {
                insertAt++;
            }

            var replaced = false;
            for (var i = sectionLine + 1; i < insertAt; i++)
            {
                if (lines[i].TrimStart().StartsWith($"{key}=", StringComparison.Ordinal))
                {
                    lines[i] = keyLine;
                    replaced = true;
                    break;
                }
            }

            if (!replaced)
            {
                lines.Insert(insertAt, keyLine);
            }
        }

        await File.WriteAllTextAsync(path, string.Join(Environment.NewLine, lines), cancellationToken).ConfigureAwait(false);
    }

    // Helper: remove a key from a project.godot located at baseDir.
    private static async Task RemoveProjectConfigKeyAsync(string baseDir, string section, string key, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(baseDir, "project.godot");
        var text = File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false) : string.Empty;
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
        var sectionHeader = $"[{section}]";
        var sectionLine = lines.FindIndex(l => l.Trim() == sectionHeader);
        if (sectionLine < 0)
        {
            return;
        }

        var start = sectionLine + 1;
        var end = lines.Count;
        for (var i = start; i < lines.Count; i++)
        {
            if (lines[i].StartsWith("[", StringComparison.Ordinal))
            {
                end = i;
                break;
            }
        }

        for (var i = start; i < end; i++)
        {
            if (lines[i].TrimStart().StartsWith($"{key}=", StringComparison.Ordinal))
            {
                lines.RemoveAt(i);
                break;
            }
        }

        await File.WriteAllTextAsync(path, string.Join(Environment.NewLine, lines), cancellationToken).ConfigureAwait(false);
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
