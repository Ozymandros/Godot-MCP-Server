using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;

namespace GodotMCP.Infrastructure.Integrations;

/// <summary>
/// Inspects a project for plugin/addon integrations and returns discovered
/// metadata for each integration.
/// </summary>
public sealed class IntegrationInspector(IPathResolver pathResolver) : IIntegrationInspector
{
    /// <summary>
    /// Discover installed integrations by looking for <c>plugin.cfg</c> files
    /// under the project's <c>addons</c> directory.
    /// </summary>
    public IReadOnlyList<IntegrationMetadata> Discover()
    {
        var addonsPath = Path.Combine(pathResolver.ProjectRoot, "addons");
        if (!Directory.Exists(addonsPath))
        {
            return Array.Empty<IntegrationMetadata>();
        }

        var metadata = new List<IntegrationMetadata>();
        foreach (var pluginCfg in Directory.EnumerateFiles(addonsPath, "plugin.cfg", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(Path.GetDirectoryName(pluginCfg) ?? pluginCfg);
            metadata.Add(new IntegrationMetadata
            {
                Name = name,
                Profile = IntegrationProfile.ProjectLocalPlugin,
                Source = pathResolver.ToResPath(pluginCfg),
                GodotVersionRange = "4.x",
                PlatformSupport = "unknown",
                IsMaintained = true
            });
        }

        return metadata;
    }
}
