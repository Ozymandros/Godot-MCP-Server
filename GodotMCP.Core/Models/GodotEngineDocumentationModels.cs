namespace GodotMCP.Core.Models;

/// <summary>
/// Result of a Godot Engine documentation search (Read the Docs API shape, simplified).
/// </summary>
public sealed class GodotEngineDocumentationSearchResponse
{
    public int Count { get; init; }

    public string? NextPageUrl { get; init; }

    public IReadOnlyList<GodotEngineDocumentationHit> Results { get; init; } = [];
}

/// <summary>
/// One search hit from docs.godotengine.org.
/// </summary>
public sealed class GodotEngineDocumentationHit
{
    public string? Type { get; init; }

    public string? Title { get; init; }

    /// <summary>
    /// Site path, e.g. /en/stable/classes/class_node2d.html
    /// </summary>
    public string? Path { get; init; }

    public string? Version { get; init; }

    /// <summary>
    /// Short excerpts from section blocks when available.
    /// </summary>
    public IReadOnlyList<string> Snippets { get; init; } = [];

    /// <summary>
    /// Absolute URL to the documentation page on docs.godotengine.org.
    /// </summary>
    public string? AbsoluteUrl { get; init; }
}
