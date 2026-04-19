using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Text.Json;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using ModelContextProtocol.Server;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    [McpServerTool(Name = "query_godot_engine_documentation"), Description(
        "Search the official Godot Engine manual and class reference on docs.godotengine.org (Read the Docs JSON search). Requires network access. Use version stable or latest; results include titles, paths, short excerpts, and absolute URLs.")]
    public static async Task<ToolResult> QueryGodotEngineDocumentationAsync(
        IGodotEngineDocumentationClient godotEngineDocs,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Search query (class names, topics, method names, etc.).")]
        string query,
        [Description("Documentation branch: stable, latest, or a specific version label supported by Read the Docs (default stable).")]
        string version = "stable",
        [Description("Maximum number of hits to return (1–40, default 12).")]
        int max_results = 12)
    {
        if (IsBlank(projectPath))
        {
            return Invalid("projectPath is required.");
        }

        if (IsBlank(query))
        {
            return Invalid("query is required.", "Try a class name (e.g. Node2D), a topic, or a method name.");
        }

        var v = string.IsNullOrWhiteSpace(version) ? "stable" : version.Trim();
        if (v.Length > 64 || v.Contains("://", StringComparison.Ordinal))
        {
            return Invalid("Invalid version value.");
        }

        var limit = Math.Clamp(max_results, 1, 40);

        try
        {
            var response = await godotEngineDocs.SearchAsync(query.Trim(), v, limit, CancellationToken.None)
                .ConfigureAwait(false);

            var rows = response.Results.Select(h => new Dictionary<string, object?>
            {
                ["type"] = h.Type,
                ["title"] = h.Title,
                ["path"] = h.Path,
                ["version"] = h.Version,
                ["url"] = h.AbsoluteUrl,
                ["snippets"] = h.Snippets.ToArray()
            }).ToList();

            var data = new Dictionary<string, object?>
            {
                ["source"] = "https://docs.godotengine.org (Read the Docs search API)",
                ["query"] = query.Trim(),
                ["version"] = v,
                ["total_count"] = response.Count,
                ["next_page"] = response.NextPageUrl,
                ["hits"] = rows
            };

            return new ToolResult(true, "Godot Engine documentation search completed.", data);
        }
        catch (HttpRequestException ex)
        {
            return new ToolResult(false, $"Could not reach Godot documentation: {ex.Message}",
                SuggestedRemediation: "Check network access and that docs.godotengine.org is reachable.");
        }
        catch (TaskCanceledException ex)
        {
            return new ToolResult(false, $"Godot documentation request timed out: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return new ToolResult(false, $"Unexpected response from documentation API: {ex.Message}");
        }
    }
}
