using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using System.Reflection;

namespace GodotMCP.Infrastructure.Process;

/// <summary>
/// Runs bundled GDScript operations by extracting an embedded script and
/// invoking the Godot CLI with <c>--script</c>.
/// </summary>
public sealed class GodotOperationsRunner(IGodotCliService godotCliService, IPathResolver pathResolver) : IGodotOperationsRunner
{
    private const string EmbeddedScriptResourceName = "GodotMCP.Infrastructure.Scripts.godot_operations.gd";

    /// <summary>
    /// Execute a named operation by writing a temporary JSON payload and
    /// invoking the embedded GDScript via Godot CLI.
    /// </summary>
    public async Task<ToolResult> RunOperationAsync(string operationName, string payloadJson, CancellationToken cancellationToken = default)
    {
        // Extract embedded script to temp
        var assembly = Assembly.GetExecutingAssembly();
        await using var resStream = assembly.GetManifestResourceStream(EmbeddedScriptResourceName) ?? Stream.Null;
        if (resStream == Stream.Null)
        {
            return new ToolResult(false, "Bundled operations script not found.");
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "godot-mcp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var scriptPath = Path.Combine(tempDir, "godot_operations.gd");
        await using (var outFs = File.Create(scriptPath))
        {
            await resStream.CopyToAsync(outFs, cancellationToken).ConfigureAwait(false);
        }

        var payloadPath = Path.Combine(tempDir, "payload.json");
        await File.WriteAllTextAsync(payloadPath, payloadJson, cancellationToken).ConfigureAwait(false);

        // Run godot with --script <scriptPath> <operationName> <payloadPath>
        var args = $"--headless --quit --script \"{scriptPath}\" {operationName} \"{payloadPath}\"";
        var result = await godotCliService.RunAsync(args, cancellationToken).ConfigureAwait(false);

        // Cleanup best-effort (do not fail operation if cleanup fails)
        try { Directory.Delete(tempDir, true); } catch { }

        return result;
    }
}
