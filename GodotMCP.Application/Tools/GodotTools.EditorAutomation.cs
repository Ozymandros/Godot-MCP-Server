using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using ModelContextProtocol.Server;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    /// <summary>
    /// Executes a raw Godot headless CLI command.
    /// </summary>
    /// <param name="godotCliService">Godot CLI service.</param>
    /// <param name="arguments">Raw command line arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result from command execution.</returns>
    [McpServerTool(Name = "run_editor_command"), Description("Execute a headless Godot CLI command with custom arguments.")]
    public static Task<ToolResult> RunEditorCommandAsync(
        IGodotCliService godotCliService,
        [Description("Raw command line arguments."), Required] string arguments,
        CancellationToken cancellationToken = default)
        => godotCliService.RunAsync(arguments, cancellationToken);

    /// <summary>
    /// Writes a simple export preset configuration file for a target platform.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="presetName">Export preset name.</param>
    /// <param name="platform">Godot export platform.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result describing update status.</returns>
    [McpServerTool(Name = "manage_export_presets"), Description("Modify Godot export_presets.cfg to include a specific target platform.")]
    public static async Task<ToolResult> ManageExportPresetsAsync(
        IGodotFileService fileService,
        [Description("Name of the export preset."), Required] string presetName,
        [Description("Godot target platform (e.g., Windows Desktop)."), Required] string platform,
        CancellationToken cancellationToken = default)
    {
        var content = $$"""
[preset.0]
name="{{presetName}}"
platform="{{platform}}"
runnable=true
""";
        await fileService.WriteAsync("res://export_presets.cfg", content, cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, "Export preset updated.");
    }
}
