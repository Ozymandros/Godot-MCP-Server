using GodotMCP.Core.Models;

namespace GodotMCP.Core.Interfaces;

/// <summary>
/// Queries the official Godot Engine documentation search API (Read the Docs, docs.godotengine.org).
/// </summary>
public interface IGodotEngineDocumentationClient
{
    /// <summary>
    /// Searches Godot documentation using the public JSON search endpoint.
    /// </summary>
    /// <param name="query">Search terms.</param>
    /// <param name="version">Documentation version (e.g. stable, latest).</param>
    /// <param name="maxResults">Maximum hits to return (clamped by the implementation).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<GodotEngineDocumentationSearchResponse> SearchAsync(
        string query,
        string version,
        int maxResults,
        CancellationToken cancellationToken = default);
}
