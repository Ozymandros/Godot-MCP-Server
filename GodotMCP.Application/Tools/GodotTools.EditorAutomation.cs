using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    [McpServerTool(Name = "run_editor_command"), Description("Execute a headless Godot CLI command with custom arguments.")]
    public static Task<ToolResult> RunEditorCommandAsync(
        IGodotCliService godotCliService,
        [Description("Raw command line arguments.")] string arguments, 
        CancellationToken cancellationToken = default)
        => godotCliService.RunAsync(arguments, cancellationToken);

    [McpServerTool(Name = "manage_export_presets"), Description("Modify Godot export_presets.cfg to include a specific target platform.")]
    public static async Task<ToolResult> ManageExportPresetsAsync(
        IGodotFileService fileService,
        [Description("Name of the export preset.")] string presetName, 
        [Description("Godot target platform (e.g., Windows Desktop).")] string platform, 
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
