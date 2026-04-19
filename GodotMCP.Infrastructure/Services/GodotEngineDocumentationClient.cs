using System.Net;
using System.Text.Json;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;

namespace GodotMCP.Infrastructure.Services;

/// <summary>
/// Calls the Read the Docs search JSON API for project <c>godot</c> on docs.godotengine.org.
/// </summary>
public sealed class GodotEngineDocumentationClient(HttpClient http) : IGodotEngineDocumentationClient
{
    private const string DocsBase = "https://docs.godotengine.org";
    private const int MaxResultsCap = 40;
    private const int SnippetMaxLength = 480;

    /// <inheritdoc />
    public async Task<GodotEngineDocumentationSearchResponse> SearchAsync(
        string query,
        string version,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        var q = query.Trim();
        var v = string.IsNullOrWhiteSpace(version) ? "stable" : version.Trim();
        var limit = Math.Clamp(maxResults, 1, MaxResultsCap);

        var uri =
            $"{DocsBase}/_/api/v2/search/?format=json&project=godot&q={Uri.EscapeDataString(q)}&version={Uri.EscapeDataString(v)}";

        using var response = await http.GetAsync(uri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var count = root.TryGetProperty("count", out var c) && c.ValueKind == JsonValueKind.Number
            ? c.GetInt32()
            : 0;

        string? next = null;
        if (root.TryGetProperty("next", out var nextEl) && nextEl.ValueKind == JsonValueKind.String)
        {
            next = nextEl.GetString();
        }

        var hits = new List<GodotEngineDocumentationHit>();
        if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in results.EnumerateArray())
            {
                if (hits.Count >= limit)
                {
                    break;
                }

                hits.Add(MapHit(item));
            }
        }

        return new GodotEngineDocumentationSearchResponse
        {
            Count = count,
            NextPageUrl = next,
            Results = hits
        };
    }

    private GodotEngineDocumentationHit MapHit(JsonElement item)
    {
        var type = TryGetString(item, "type");
        var title = TryGetString(item, "title");
        var path = TryGetString(item, "path");
        var ver = TryGetString(item, "version");

        var snippets = new List<string>();
        if (item.TryGetProperty("blocks", out var blocks) && blocks.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in blocks.EnumerateArray())
            {
                if (snippets.Count >= 3)
                {
                    break;
                }

                var content = TryGetString(block, "content");
                if (!string.IsNullOrWhiteSpace(content))
                {
                    snippets.Add(Truncate(WebUtility.HtmlDecode(StripSimpleTags(content)), SnippetMaxLength));
                }
            }
        }

        string? absolute = null;
        if (!string.IsNullOrEmpty(path))
        {
            absolute = path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? path
                : DocsBase.TrimEnd('/') + path;
        }

        return new GodotEngineDocumentationHit
        {
            Type = type,
            Title = title,
            Path = path,
            Version = ver,
            Snippets = snippets,
            AbsoluteUrl = absolute
        };
    }

    private static string? TryGetString(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static string StripSimpleTags(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return html;
        }

        var s = html;
        var i = 0;
        while (i < s.Length)
        {
            var open = s.IndexOf('<', i);
            if (open < 0)
            {
                break;
            }

            var close = s.IndexOf('>', open);
            if (close < 0)
            {
                break;
            }

            s = s.Remove(open, close - open + 1);
            i = open;
        }

        return s;
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "…";
}
