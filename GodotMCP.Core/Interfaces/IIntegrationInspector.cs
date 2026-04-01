using GodotMCP.Core.Models;

namespace GodotMCP.Core.Interfaces;

/// <summary>
/// Inspects project directories to discover installed integrations/plugins.
/// </summary>
public interface IIntegrationInspector
{
    /// <summary>Discover installed integrations and return metadata for each.</summary>
    IReadOnlyList<IntegrationMetadata> Discover();
}
