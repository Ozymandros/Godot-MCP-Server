using GodotMCP.Core.Models;

namespace GodotMCP.Core.Interfaces;

/// <summary>
/// Discovers integration metadata from the project addons ecosystem.
/// </summary>
public interface IIntegrationInspector
{
    /// <summary>
    /// Discovers integration entries.
    /// </summary>
    /// <returns>Discovered integration metadata collection.</returns>
    IReadOnlyList<IntegrationMetadata> Discover();
}
