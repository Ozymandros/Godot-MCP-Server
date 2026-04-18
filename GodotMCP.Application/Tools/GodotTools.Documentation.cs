using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using GodotMCP.Core.Models;
using ModelContextProtocol.Server;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    private const int MaxManifestMatches = 40;
    private const int MaxManifestExcerpt = 25;
    private const int MaxMarkdownFiles = 15;
    private const int MaxMarkdownLinesPerFile = 8;

    [McpServerTool(Name = "query_system_documentation"), Description(
        "Search generated DocFX output (manifest.json under _site) and/or conceptual Markdown under docs/. Use after building docs (dotnet docfx docs/docfx.json). Repository root is resolved via repository_root, GODOT_MCP_REPO_ROOT, or by walking up from the current directory until docs/docfx.json is found.")]
    public static Task<ToolResult> QuerySystemDocumentationAsync(
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("Substring matched case-insensitively against manifest titles, summaries, and HTML paths, and against Markdown lines. Leave empty to list an excerpt from manifest only (Markdown scan skipped).")]
        string query = "",
        [Description("Optional absolute path to the Godot MCP git repository root (folder containing docs/docfx.json).")]
        string? repository_root = null,
        [Description("manifest: search _site/manifest.json only. markdown: search docs/**/*.md only. both: run both.")]
        string source = "both")
    {
        if (IsBlank(projectPath))
        {
            return Task.FromResult(Invalid("projectPath is required."));
        }

        if (!TryResolveRepositoryRoot(repository_root, out var root, out var resolveError))
        {
            return Task.FromResult(new ToolResult(false, resolveError ?? "Could not resolve repository root."));
        }

        var normalizedSource = source.Trim().ToLowerInvariant();
        if (normalizedSource is not ("manifest" or "markdown" or "both"))
        {
            return Task.FromResult(new ToolResult(false, "source must be 'manifest', 'markdown', or 'both'.", SuggestedRemediation: "Use manifest, markdown, or both."));
        }

        var q = query.Trim();
        var includeManifest = normalizedSource is "manifest" or "both";
        var includeMarkdown = normalizedSource is "markdown" or "both";
        if (string.IsNullOrEmpty(q))
        {
            includeMarkdown = false;
        }

        var data = new Dictionary<string, object?>
        {
            ["repository_root"] = root
        };

        if (includeManifest)
        {
            var manifestPath = Path.Combine(root, "_site", "manifest.json");
            if (!File.Exists(manifestPath))
            {
                data["manifest"] = new Dictionary<string, object?>
                {
                    ["found"] = false,
                    ["path"] = manifestPath,
                    ["hint"] = "Build documentation first: dotnet docfx docs/docfx.json"
                };
            }
            else if (!IsStrictChildPath(root, manifestPath))
            {
                return Task.FromResult(new ToolResult(false, "Resolved manifest path escapes repository root."));
            }
            else
            {
                data["manifest"] = SearchManifest(manifestPath, q);
            }
        }

        if (includeMarkdown)
        {
            var docsDir = Path.Combine(root, "docs");
            if (!Directory.Exists(docsDir))
            {
                data["markdown"] = "docs/ directory not found.";
            }
            else if (!IsStrictChildPath(root, docsDir))
            {
                return Task.FromResult(new ToolResult(false, "Resolved docs path escapes repository root."));
            }
            else
            {
                data["markdown"] = SearchMarkdownUnderDocs(docsDir, root, q);
            }
        }

        return Task.FromResult(new ToolResult(true, "Documentation query completed.", data));
    }

    private static bool TryResolveRepositoryRoot(string? repositoryRoot, out string root, out string? error)
    {
        error = null;
        root = "";

        if (!string.IsNullOrWhiteSpace(repositoryRoot))
        {
            var candidate = Path.GetFullPath(repositoryRoot);
            if (!File.Exists(Path.Combine(candidate, "docs", "docfx.json")))
            {
                error = $"docs/docfx.json not found under '{candidate}'.";
                return false;
            }

            root = candidate;
            return true;
        }

        var fromEnv = Environment.GetEnvironmentVariable("GODOT_MCP_REPO_ROOT");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            var candidate = Path.GetFullPath(fromEnv);
            if (File.Exists(Path.Combine(candidate, "docs", "docfx.json")))
            {
                root = candidate;
                return true;
            }
        }

        for (var dir = new DirectoryInfo(Path.GetFullPath(Environment.CurrentDirectory)); dir is not null; dir = dir.Parent)
        {
            var docfxPath = Path.Combine(dir.FullName, "docs", "docfx.json");
            if (File.Exists(docfxPath))
            {
                root = dir.FullName;
                return true;
            }
        }

        error = "Could not locate docs/docfx.json. Set repository_root, define GODOT_MCP_REPO_ROOT, or run from within the repository tree.";
        return false;
    }

    private static bool IsStrictChildPath(string repositoryRoot, string path)
    {
        var root = Path.GetFullPath(repositoryRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var full = Path.GetFullPath(path);
        return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
               || string.Equals(full, root, StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, object?> SearchManifest(string manifestPath, string query)
    {
        var json = File.ReadAllText(manifestPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!root.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array)
        {
            return new Dictionary<string, object?>
            {
                ["found"] = true,
                ["path"] = manifestPath,
                ["error"] = "manifest.json missing 'files' array."
            };
        }

        var q = query.Trim();
        var matches = new List<Dictionary<string, string?>>();
        var excerpt = new List<Dictionary<string, string?>>();

        foreach (var file in files.EnumerateArray())
        {
            var title = TryGetString(file, "Title");
            var summary = TryGetString(file, "Summary");
            var htmlPath = TryGetHtmlRelativePath(file);
            var blob = $"{title} {StripHtml(summary)} {htmlPath}";

            if (string.IsNullOrEmpty(q))
            {
                if (excerpt.Count < MaxManifestExcerpt && (!string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(htmlPath)))
                {
                    excerpt.Add(new Dictionary<string, string?>
                    {
                        ["title"] = title,
                        ["html_path"] = htmlPath,
                        ["summary"] = Truncate(StripHtml(summary), 200)
                    });
                }
            }
            else if (blob.Contains(q, StringComparison.OrdinalIgnoreCase) && matches.Count < MaxManifestMatches)
            {
                matches.Add(new Dictionary<string, string?>
                {
                    ["title"] = title,
                    ["html_path"] = htmlPath,
                    ["summary"] = Truncate(StripHtml(summary), 280)
                });
            }
        }

        return new Dictionary<string, object?>
        {
            ["found"] = true,
            ["path"] = manifestPath,
            ["file_count"] = files.GetArrayLength(),
            ["query"] = q,
            ["matches"] = matches,
            ["excerpt"] = string.IsNullOrEmpty(q) ? excerpt : null
        };
    }

    private static List<Dictionary<string, object?>> SearchMarkdownUnderDocs(string docsDir, string repositoryRoot, string query)
    {
        var results = new List<Dictionary<string, object?>>();
        foreach (var file in Directory.EnumerateFiles(docsDir, "*.md", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}api{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!IsStrictChildPath(repositoryRoot, file))
            {
                continue;
            }

            if (results.Count >= MaxMarkdownFiles)
            {
                break;
            }

            var lines = new List<Dictionary<string, object?>>();
            var lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                if (line.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    lines.Add(new Dictionary<string, object?>
                    {
                        ["line"] = lineNumber,
                        ["text"] = Truncate(line.Trim(), 500)
                    });
                    if (lines.Count >= MaxMarkdownLinesPerFile)
                    {
                        break;
                    }
                }
            }

            if (lines.Count > 0)
            {
                results.Add(new Dictionary<string, object?>
                {
                    ["path"] = Path.GetRelativePath(repositoryRoot, file),
                    ["matches"] = lines
                });
            }
        }

        return results;
    }

    private static string? TryGetString(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static string? TryGetHtmlRelativePath(JsonElement file)
    {
        if (!file.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!output.TryGetProperty(".html", out var html) || html.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!html.TryGetProperty("relative_path", out var rel) || rel.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return rel.GetString();
    }

    private static string StripHtml(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return "";
        }

        var noTags = Regex.Replace(html, "<.*?>", string.Empty);
        return WebUtilityHtmlDecode(noTags);
    }

    private static string WebUtilityHtmlDecode(string s)
    {
        try
        {
            return System.Net.WebUtility.HtmlDecode(s);
        }
        catch
        {
            return s;
        }
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s))
        {
            return "";
        }

        return s.Length <= max ? s : s[..max] + "…";
    }
}
