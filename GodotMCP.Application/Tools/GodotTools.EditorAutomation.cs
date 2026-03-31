using GodotMCP.Core.Models;
using StreamJsonRpc;

namespace GodotMCP.Application.Tools;

public partial class GodotTools
{
    [JsonRpcMethod("run_editor_command")]
    public Task<ToolResult> RunEditorCommandAsync(string arguments, CancellationToken cancellationToken = default)
        => godotCliService.RunAsync(arguments, cancellationToken);

    [JsonRpcMethod("manage_export_presets")]
    public async Task<ToolResult> ManageExportPresetsAsync(string presetName, string platform, CancellationToken cancellationToken = default)
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
