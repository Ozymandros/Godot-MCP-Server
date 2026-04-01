using GodotMCP.Core.Models;

namespace GodotMCP.Core.Interfaces;

public interface IIntegrationInspector
{
    IReadOnlyList<IntegrationMetadata> Discover();
}
