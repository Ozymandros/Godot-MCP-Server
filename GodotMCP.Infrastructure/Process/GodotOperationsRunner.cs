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

        // Run godot with --path <projectRoot> --script <scriptPath> <operationName> <payloadPath>
        var projectArg = $"--path \"{pathResolver.ProjectRoot}\"";
        var args = $"--headless --quit {projectArg} --script \"{scriptPath}\" {operationName} \"{payloadPath}\"";
        var rawResult = await godotCliService.RunAsync(args, cancellationToken).ConfigureAwait(false);

        // Cleanup best-effort (do not fail operation if cleanup fails)
        try { Directory.Delete(tempDir, true); } catch { }

        // Try to parse stdout as a JSON response envelope emitted by the GDScript.
        var stdout = rawResult.Data is not null && rawResult.Data.TryGetValue("stdout", out var outText) ? outText : string.Empty;
        var stderr = rawResult.Data is not null && rawResult.Data.TryGetValue("stderr", out var errText) ? errText : string.Empty;

        if (string.IsNullOrWhiteSpace(stdout))
        {
            // No stdout; return failure with captured stderr
            return new ToolResult(false, "No response from Godot operations script.", new Dictionary<string, string>
            {
                ["stdout"] = stdout ?? string.Empty,
                ["stderr"] = stderr ?? string.Empty
            });
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(stdout);
            var root = doc.RootElement;
            var success = root.GetProperty("success").GetBoolean();
            var message = root.TryGetProperty("message", out var m) ? m.GetString() ?? string.Empty : string.Empty;
            var data = new Dictionary<string, string>(StringComparer.Ordinal);
            if (root.TryGetProperty("data", out var d) && d.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var prop in d.EnumerateObject())
                {
                    data[prop.Name] = prop.Value.ToString() ?? string.Empty;
                }
            }

            // Include stderr for debugging
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                data["stderr"] = stderr ?? string.Empty;
            }

            return new ToolResult(success, message, data);
        }
        catch (System.Text.Json.JsonException)
        {
            return new ToolResult(false, "Invalid JSON response from Godot operations script.", new Dictionary<string, string>
            {
                ["stdout"] = stdout ?? string.Empty,
                ["stderr"] = stderr ?? string.Empty
            });
        }
    }
}
